/**
 * api-client.js – Centralised fetch wrapper for the BPO Platform REST API.
 *
 * Features:
 *  • Automatic base-URL prefixing from BpoConfig.apiBaseUrl
 *  • Automatic JWT Bearer token injection via BpoAuth.getToken()
 *  • JSON response parsing with structured error objects
 *  • File upload (multipart/form-data) helper
 *  • Global loading indicator (adds/removes CSS class on <body>)
 *
 * Depends on: app-config.js, auth.js
 */

window.BpoApi = (function () {
  const baseUrl = () => window.BpoConfig.apiBaseUrl;

  /* ── Internal fetch helper ────────────────────────────────────────────── */

  async function _request(method, path, { body, formData, query } = {}) {
    let url = `${baseUrl()}${path}`;
    if (query) {
      const params = new URLSearchParams(
        Object.fromEntries(Object.entries(query).filter(([, v]) => v !== null && v !== undefined))
      );
      if ([...params].length) url += `?${params}`;
    }

    const token = await window.BpoAuth.getToken();
    const headers = {};
    if (token) headers['Authorization'] = `Bearer ${token}`;
    if (body)  headers['Content-Type']  = 'application/json';

    document.body.classList.add('api-loading');
    try {
      const resp = await fetch(url, {
        method,
        headers,
        body: formData ? formData : (body ? JSON.stringify(body) : undefined),
      });

      if (resp.status === 204) return null; // No Content
      const data = await resp.json().catch(() => null);

      if (!resp.ok) {
        const msg = data?.title || data?.message || `HTTP ${resp.status}`;
        const err = new Error(msg);
        err.status = resp.status;
        err.data   = data;
        throw err;
      }
      return data;
    } finally {
      document.body.classList.remove('api-loading');
    }
  }

  /* ── Public methods ───────────────────────────────────────────────────── */

  const get    = (path, opts) => _request('GET',    path, opts);
  const post   = (path, opts) => _request('POST',   path, opts);
  const put    = (path, opts) => _request('PUT',    path, opts);
  const patch  = (path, opts) => _request('PATCH',  path, opts);
  const del    = (path, opts) => _request('DELETE', path, opts);

  /* ── Domain helpers ───────────────────────────────────────────────────── */

  const api = {
    // Generic verbs
    get, post, put, patch, delete: del,

    // ── Dashboard ──────────────────────────────────────────────────────────
    dashboard: {
      kpis:           ()  => get('/api/v1/dashboard/kpis'),
      analyzeProcess: (id) => post(`/api/v1/dashboard/processes/${id}/analyse`),
    },

    // ── Processes ──────────────────────────────────────────────────────────
    processes: {
      list: (filters = {}) => get('/api/v1/processes', { query: filters }),
      getById: (id)        => get(`/api/v1/processes/${id}`),
      create: (data)       => post('/api/v1/processes', { body: data }),
      update: (id, data)   => put(`/api/v1/processes/${id}`, { body: data }),
      delete: (id)         => del(`/api/v1/processes/${id}`),
      advanceStatus: (id, newStatus) =>
        patch(`/api/v1/processes/${id}/status`, { body: { newStatus } }),
      workflowSteps: (id)  => get(`/api/v1/processes/${id}/workflow-steps`),
    },

    // ── Artifacts ──────────────────────────────────────────────────────────
    artifacts: {
      list: (processId) => get(`/api/v1/processes/${processId}/artifacts`),
      upload: (processId, file, artifactType = 'Other') => {
        const fd = new FormData();
        fd.append('file', file);
        return _request('POST', `/api/v1/processes/${processId}/artifacts`, {
          formData: fd,
          query: { artifactType },
        });
      },
      downloadUrl: (processId, artifactId, expiryMinutes = 60) =>
        get(`/api/v1/processes/${processId}/artifacts/${artifactId}/download-url`, {
          query: { expiryMinutes },
        }),
      delete: (processId, artifactId) =>
        del(`/api/v1/processes/${processId}/artifacts/${artifactId}`),
    },

    // ── Kanban ─────────────────────────────────────────────────────────────
    kanban: {
      getBoard: (processId)    => get(`/api/v1/processes/${processId}/kanban`),
      createCard: (processId, data) =>
        post(`/api/v1/processes/${processId}/kanban/cards`, { body: data }),
      updateCard: (cardId, data) =>
        put(`/api/v1/kanban/cards/${cardId}`, { body: data }),
      moveCard: (cardId, newColumn, newPosition) =>
        patch(`/api/v1/kanban/cards/${cardId}/move`, { body: { newColumn, newPosition } }),
      deleteCard: (cardId)     => del(`/api/v1/kanban/cards/${cardId}`),
    },
  };

  return api;
})();
