/**
 * analysis-api.js – Wires the AI Analysis page to the REST API.
 *
 * What it does:
 *  1. Loads all processes from GET /api/v1/processes and populates
 *     the process selector dropdown.
 *  2. Loads analysis status cards dynamically from API data.
 *  3. Triggers AI analysis via POST /api/v1/dashboard/processes/{id}/analyse.
 *  4. Wires feedback form submission (advances process status via API).
 *
 * Depends on: app-config.js, auth.js, api-client.js, notifications.js
 */

(function () {
  'use strict';

  let _processes = [];

  /* ── Status counts ───────────────────────────────────────────────────── */

  function renderStatusCounts(processes) {
    const complete   = processes.filter(p => p.status === 'Approved' || p.status === 'Deployed').length;
    const missing    = processes.filter(p => p.automationScore !== null && p.automationScore < 60).length;
    const inProgress = processes.filter(p => p.status === 'InProgress' || p.status === 'Draft').length;

    const setText = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = v; };
    setText('status-complete',    complete);
    setText('status-missing',     missing);
    setText('status-in-progress', inProgress);
  }

  /* ── Render suggestion cards ─────────────────────────────────────────── */

  function confidenceClass(score) {
    if (score == null) return 'medium';
    if (score >= 85)   return 'high';
    if (score >= 60)   return 'medium';
    return 'low';
  }

  function renderSuggestionCards(processes) {
    const container = document.getElementById('suggestionCards');
    if (!container) return;

    if (processes.length === 0) {
      container.innerHTML = '<p class="empty-state" style="color:#999;padding:20px">No processes found.</p>';
      return;
    }

    container.innerHTML = processes.slice(0, 5).map(p => `
      <div class="suggestion-card" data-process-id="${p.id}">
        <div class="suggestion-header">
          <h3>Process: ${p.name}</h3>
          <span class="confidence-badge ${confidenceClass(p.automationScore)}">
            ${p.automationScore != null ? `${Math.round(p.automationScore)}% Automation` : 'Not analysed'}
          </span>
        </div>
        <div class="suggestion-content">
          <h4>Key Insights:</h4>
          <ul class="insights-list">
            <li>${p.automationScore != null ? '✅' : '⏳'} Automation potential: ${p.automationScore != null ? Math.round(p.automationScore) + '%' : 'Pending analysis'}</li>
            <li>${p.complianceScore  != null ? '✅' : '⏳'} Compliance score: ${p.complianceScore  != null ? Math.round(p.complianceScore)  + '%' : 'Pending analysis'}</li>
            <li>📌 Status: ${p.status}</li>
            <li>🏢 Department: ${p.department || 'N/A'}</li>
          </ul>
          <div class="suggestion-actions">
            <button class="btn btn-primary"
              onclick="bpoRunAnalysis('${p.id}', this)">
              ${p.automationScore != null ? 'Re-Analyse' : 'Analyse Now'}
            </button>
            <button class="btn btn-secondary"
              onclick="bpoAdvanceStatus('${p.id}', 'UnderReview', this)">Send to Review</button>
          </div>
        </div>
      </div>
    `).join('');
  }

  /* ── Render validation grid ──────────────────────────────────────────── */

  function renderValidationGrid(processes) {
    const grid = document.getElementById('validationGrid');
    if (!grid) return;

    if (processes.length === 0) {
      grid.innerHTML = '<p class="empty-state" style="color:#999;padding:20px">No processes found.</p>';
      return;
    }

    grid.innerHTML = processes.slice(0, 6).map(p => {
      const isComplete   = p.status === 'Approved' || p.status === 'Deployed';
      const isIncomplete = p.automationScore == null || p.complianceScore == null;
      const cls = isComplete ? 'complete' : (isIncomplete ? 'incomplete' : 'in-progress');
      const badge = isComplete
        ? '✅ Complete'
        : (isIncomplete ? '⚠️ Missing Elements' : '🔄 In Progress');

      return `
        <div class="validation-card ${cls}">
          <div class="validation-header">
            <h3>${p.name}</h3>
            <span class="validation-badge">${badge}</span>
          </div>
          <div class="validation-details">
            <div class="detail-row"><span>Department:</span><span>${p.department || 'N/A'}</span></div>
            <div class="detail-row"><span>Status:</span><span>${p.status}</span></div>
            <div class="detail-row">
              <span>Automation:</span>
              <span ${p.automationScore == null ? 'class="missing"' : ''}>${p.automationScore != null ? Math.round(p.automationScore) + '/100' : 'Pending'}</span>
            </div>
            <div class="detail-row">
              <span>Compliance:</span>
              <span ${p.complianceScore == null ? 'class="missing"' : ''}>${p.complianceScore != null ? Math.round(p.complianceScore) + '/100' : 'Pending'}</span>
            </div>
          </div>
        </div>`;
    }).join('');
  }

  /* ── Populate feedback dropdown ──────────────────────────────────────── */

  function populateFeedbackDropdown(processes) {
    const select = document.getElementById('feedbackProcessSelect');
    if (!select) return;
    select.innerHTML = processes
      .map(p => `<option value="${p.id}">${p.name}</option>`)
      .join('');
  }

  /* ── Global helpers ──────────────────────────────────────────────────── */

  window.bpoRunAnalysis = async function (processId, btn) {
    const original = btn.textContent;
    btn.disabled   = true;
    btn.textContent = 'Analyzing…';
    try {
      const result = await window.BpoApi.dashboard.analyzeProcess(processId);
      window.BpoNotifications?.showToast(
        'AI Analysis Complete',
        `Automation: ${Math.round(result.automationScore)}% | Compliance: ${Math.round(result.complianceScore)}%`,
        'success'
      );
      await loadData();
    } catch (err) {
      window.BpoNotifications?.showToast('Analysis Failed', err.message, 'error');
    } finally {
      btn.disabled    = false;
      btn.textContent = original;
    }
  };

  window.bpoAdvanceStatus = async function (processId, newStatus, btn) {
    btn.disabled = true;
    try {
      await window.BpoApi.processes.advanceStatus(processId, newStatus);
      window.BpoNotifications?.showToast('Status Updated', `Process sent to ${newStatus}.`, 'info');
      await loadData();
    } catch (err) {
      window.BpoNotifications?.showToast('Error', err.message, 'error');
    } finally {
      btn.disabled = false;
    }
  };

  /* ── Load data ───────────────────────────────────────────────────────── */

  async function loadData() {
    try {
      const resp = await window.BpoApi.processes.list({ pageSize: 50 });
      _processes = resp.items ?? resp;
      renderStatusCounts(_processes);
      renderSuggestionCards(_processes);
      renderValidationGrid(_processes);
      populateFeedbackDropdown(_processes);
    } catch (err) {
      console.error('[analysis-api] Failed to load processes:', err);
      window.BpoNotifications?.showToast('Error', 'Could not load process data.', 'error');
    }
  }

  /* ── Feedback form ───────────────────────────────────────────────────── */

  function wireFeedbackForm() {
    const form = document.querySelector('.feedback-form');
    if (!form) return;
    form.addEventListener('submit', async e => {
      e.preventDefault();
      const processId  = document.getElementById('feedbackProcessSelect')?.value;
      const typeSelect = form.querySelector('select:nth-of-type(2)');
      const comments   = form.querySelector('textarea')?.value;

      if (!processId || !comments?.trim()) {
        window.BpoNotifications?.showToast('Feedback', 'Please select a process and add comments.', 'warning');
        return;
      }

      const typeMap = {
        'Approve Analysis':  'Approved',
        'Request Revision':  'InProgress',
        'Flag Issue':        'Draft',
      };
      const newStatus = typeMap[typeSelect?.value] || null;
      if (newStatus) {
        try {
          await window.BpoApi.processes.advanceStatus(processId, newStatus);
          window.BpoNotifications?.showToast('Feedback Submitted', 'Process status updated.', 'success');
          form.reset();
          await loadData();
        } catch (err) {
          window.BpoNotifications?.showToast('Error', err.message, 'error');
        }
      } else {
        window.BpoNotifications?.showToast('Feedback Saved', 'Comment recorded.', 'info');
        form.reset();
      }
    });
  }

  /* ── Refresh button ──────────────────────────────────────────────────── */

  function wireRefreshButton() {
    const btn = document.querySelector('[data-action="refresh-analysis"]') ||
                [...document.querySelectorAll('.btn-sm')].find(b => b.textContent.includes('Refresh'));
    if (btn) btn.addEventListener('click', loadData);
  }

  /* ── Bootstrap ───────────────────────────────────────────────────────── */

  async function init() {
    await loadData();
    wireFeedbackForm();
    wireRefreshButton();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
