/**
 * upload-api.js – Wires the Upload page to the BPO Platform REST API.
 *
 * What it does:
 *  1. On load: reads localStorage for a selected processId (set on workflow/process page).
 *     Falls back to creating a temporary "Demo Process" if none exists.
 *  2. Real multipart upload to POST /api/v1/processes/{id}/artifacts.
 *  3. Lists uploaded artifacts from GET /api/v1/processes/{id}/artifacts.
 *  4. Delete artifacts via DELETE /api/v1/processes/{id}/artifacts/{artifactId}.
 *  5. Progress bar driven by XMLHttpRequest upload events.
 *
 * Depends on: app-config.js, auth.js, api-client.js, notifications.js
 */

(function () {
  'use strict';

  let _processId = null;

  /* ── Resolve process ID ───────────────────────────────────────────────── */

  async function resolveProcessId() {
    // Try stored process first
    const stored = localStorage.getItem('bpo_current_process_id') ||
                   window.BpoConfig.defaultProcessId;
    if (stored) { _processId = stored; return; }

    // Create a transient demo process so the upload page works standalone
    try {
      const proc = await window.BpoApi.processes.create({
        name:        'Demo Process',
        description: 'Auto-created for artifact upload demo.',
        department:  'General',
        owner:       'dev@localhost',
      });
      _processId = proc.id;
      localStorage.setItem('bpo_current_process_id', _processId);
    } catch (err) {
      console.error('[upload-api] Could not resolve process ID:', err);
    }
  }

  /* ── Render artifact list ─────────────────────────────────────────────── */

  function artifactIcon(artifactType) {
    const map = {
      Video:         'bi-camera-video',
      Pdf:           'bi-file-earmark-pdf',
      Audio:         'bi-file-earmark-music',
      Transcription: 'bi-pencil-square',
      Image:         'bi-image',
      Other:         'bi-file-earmark',
    };
    return map[artifactType] || map.Other;
  }

  function formatBytes(bytes) {
    if (!bytes) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
  }

  function timeAgo(isoString) {
    if (!isoString) return '';
    const diff = Date.now() - new Date(isoString).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 1)  return 'just now';
    if (mins < 60) return `${mins} min ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24)  return `${hrs} hour${hrs > 1 ? 's' : ''} ago`;
    return `${Math.floor(hrs / 24)} day${Math.floor(hrs / 24) > 1 ? 's' : ''} ago`;
  }

  function renderArtifacts(artifacts) {
    const list = document.getElementById('artifactsList');
    if (!list) return;

    if (!artifacts || artifacts.length === 0) {
      list.innerHTML = '<p class="empty-state" style="padding:20px;color:#999;">No artifacts uploaded yet.</p>';
      return;
    }

    list.innerHTML = artifacts.map(a => `
      <div class="file-item uploaded" data-id="${a.id}">
        <div class="file-icon"><i class="bi ${artifactIcon(a.artifactType)}"></i></div>
        <div class="file-info">
          <h4>${a.fileName}</h4>
          <p>${a.artifactType} • ${formatBytes(a.fileSizeBytes)} • ${timeAgo(a.uploadedAt)}</p>
        </div>
        <div class="file-status">
          <span class="status-badge ${a.analysisStatus === 'Analysed' ? 'success' : 'info'}">
            ${a.analysisStatus === 'Analysed' ? '✅ Analysed' : '⏳ Pending'}
          </span>
          <div class="file-actions">
            <button class="btn btn-sm" onclick="bpoDownloadArtifact('${a.id}')">View</button>
            <button class="btn btn-sm" onclick="bpoDeleteArtifact('${a.id}')">Delete</button>
          </div>
        </div>
      </div>
    `).join('');
  }

  /* ── Load artifacts from API ─────────────────────────────────────────── */

  async function loadArtifacts() {
    if (!_processId) return;
    try {
      const artifacts = await window.BpoApi.artifacts.list(_processId);
      renderArtifacts(artifacts);
    } catch (err) {
      console.error('[upload-api] Failed to load artifacts:', err);
    }
  }

  /* ── Upload file with XHR progress ─────────────────────────────────────── */

  async function uploadFile(file, artifactType) {
    if (!_processId) {
      window.BpoNotifications?.showToast('Upload', 'No process selected.', 'error');
      return;
    }

    const token  = await window.BpoAuth.getToken();
    const url    = `${window.BpoConfig.apiBaseUrl}/api/v1/processes/${_processId}/artifacts?artifactType=${artifactType}`;
    const formData = new FormData();
    formData.append('file', file);

    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.open('POST', url);
      if (token) xhr.setRequestHeader('Authorization', `Bearer ${token}`);

      // Inline progress bar
      const progressId = `progress-${Date.now()}`;
      const inlineHtml = `
        <div id="${progressId}" class="file-item uploading">
          <div class="file-icon"><i class="bi ${artifactIcon(artifactType)}"></i></div>
          <div class="file-info">
            <h4>${file.name}</h4>
            <p>${formatBytes(file.size)} • Uploading…</p>
            <div class="progress-bar" style="height:4px;background:#eee;border-radius:2px;margin-top:4px">
              <div class="progress-fill" id="${progressId}-bar"
                   style="height:100%;width:0%;background:#294993;border-radius:2px;transition:width .2s"></div>
            </div>
          </div>
          <div class="file-status"><span class="status-badge warning">🔄 0%</span></div>
        </div>`;

      const list = document.getElementById('artifactsList');
      if (list) list.insertAdjacentHTML('afterbegin', inlineHtml);

      xhr.upload.onprogress = e => {
        if (!e.lengthComputable) return;
        const pct = Math.round(e.loaded / e.total * 100);
        const bar = document.getElementById(`${progressId}-bar`);
        if (bar) bar.style.width = `${pct}%`;
        const badge = document.querySelector(`#${progressId} .status-badge`);
        if (badge) badge.textContent = `🔄 ${pct}%`;
      };

      xhr.onload = () => {
        document.getElementById(progressId)?.remove();
        if (xhr.status >= 200 && xhr.status < 300) {
          window.BpoNotifications?.showToast('Uploaded', `${file.name} uploaded successfully.`, 'success');
          loadArtifacts();
          resolve(JSON.parse(xhr.responseText));
        } else {
          window.BpoNotifications?.showToast('Upload Failed', `Could not upload ${file.name}.`, 'error');
          reject(new Error(`Upload failed: ${xhr.status}`));
        }
      };
      xhr.onerror = () => {
        document.getElementById(progressId)?.remove();
        window.BpoNotifications?.showToast('Upload Error', 'Network error during upload.', 'error');
        reject(new Error('Network error'));
      };

      xhr.send(formData);
    });
  }

  /* ── Global helpers called from inline HTML onclick ─────────────────── */

  window.bpoDeleteArtifact = async function (artifactId) {
    if (!confirm('Delete this artifact?')) return;
    try {
      await window.BpoApi.artifacts.delete(_processId, artifactId);
      window.BpoNotifications?.showToast('Deleted', 'Artifact removed.', 'info');
      loadArtifacts();
    } catch (err) {
      window.BpoNotifications?.showToast('Error', err.message, 'error');
    }
  };

  window.bpoDownloadArtifact = async function (artifactId) {
    try {
      const resp = await window.BpoApi.artifacts.downloadUrl(_processId, artifactId);
      window.open(resp.downloadUrl, '_blank');
    } catch (err) {
      window.BpoNotifications?.showToast('Error', 'Could not get download URL.', 'error');
    }
  };

  /* ── Detect artifact type from MIME ─────────────────────────────────── */

  function detectArtifactType(file) {
    const mime = file.type;
    if (mime.startsWith('video/'))       return 'Video';
    if (mime === 'application/pdf')      return 'Pdf';
    if (mime.startsWith('audio/'))       return 'Audio';
    if (mime.startsWith('image/'))       return 'Image';
    if (mime.startsWith('text/'))        return 'Transcription';
    return 'Other';
  }

  /* ── Handle file selection ───────────────────────────────────────────── */

  function handleFiles(files) {
    for (const file of files) {
      const artifactType = detectArtifactType(file);
      uploadFile(file, artifactType).catch(console.error);
    }
  }

  /* ── Bootstrap ───────────────────────────────────────────────────────── */

  async function init() {
    await resolveProcessId();
    await loadArtifacts();

    // Drag & drop – augment existing bpo-platform.js handlers
    const uploadZone = document.getElementById('uploadZone');
    const fileInput  = document.getElementById('fileInput');

    if (fileInput) {
      fileInput.addEventListener('change', () => handleFiles(fileInput.files));
    }

    if (uploadZone) {
      uploadZone.addEventListener('drop', e => {
        e.preventDefault();
        handleFiles(e.dataTransfer.files);
      });
    }

    // "Analyze All" button
    const analyzeAllBtn = document.querySelector('[data-action="analyze-all"]') ||
                          [...document.querySelectorAll('.btn')].find(b => b.textContent.includes('Analyze All'));
    if (analyzeAllBtn) {
      analyzeAllBtn.addEventListener('click', async () => {
        if (!_processId) return;
        analyzeAllBtn.disabled = true;
        analyzeAllBtn.textContent = 'Analyzing…';
        try {
          await window.BpoApi.dashboard.analyzeProcess(_processId);
          window.BpoNotifications?.showToast('AI Analysis', 'Analysis complete!', 'success');
          loadArtifacts();
        } catch (err) {
          window.BpoNotifications?.showToast('Analysis Failed', err.message, 'error');
        } finally {
          analyzeAllBtn.disabled = false;
          analyzeAllBtn.textContent = 'Analyze All';
        }
      });
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
