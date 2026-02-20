/**
 * workflow-api.js – Wires the Workflow Engine page to the REST API.
 *
 * What it does:
 *  1. Loads workflow steps for the current process from
 *     GET /api/v1/processes/{id}/workflow-steps and renders them.
 *  2. Wires Quick Action buttons (Upload, View Analysis, Save, Export).
 *  3. Provides a functional AI Assistant chatbot that calls the AI analysis
 *     endpoint and responds with real data.
 *
 * Depends on: app-config.js, auth.js, api-client.js, notifications.js
 */

(function () {
  'use strict';

  let _processId = null;
  let _process   = null;

  /* ── Resolve process ──────────────────────────────────────────────────── */

  async function resolveProcess() {
    const stored = localStorage.getItem('bpo_current_process_id') ||
                   window.BpoConfig.defaultProcessId;
    if (stored) {
      _processId = stored;
      try { _process = await window.BpoApi.processes.getById(_processId); } catch (_) {}
      return;
    }

    // Create demo process
    try {
      _process   = await window.BpoApi.processes.create({
        name: 'Demo Process', description: 'Auto-created for workflow demo.',
        department: 'General', owner: 'dev@localhost',
      });
      _processId = _process.id;
      localStorage.setItem('bpo_current_process_id', _processId);
    } catch (err) {
      console.error('[workflow-api] Could not resolve process:', err);
    }
  }

  /* ── Step status helpers ─────────────────────────────────────────────── */

  const STEP_ORDER = [
    'MetaInformation',
    'ArtifactUpload',
    'AiValidation',
    'ReviewAndApproval',
    'Deployment',
  ];

  function stepStatusClass(status) {
    return { Completed: 'completed', InProgress: 'in-progress', Pending: 'pending' }[status] || 'pending';
  }

  function stepStatusLabel(status) {
    return { Completed: '✅ Completed', InProgress: '🔄 In Progress', Pending: '⏳ Pending' }[status] || '⏳ Pending';
  }

  /* ── Render workflow steps ───────────────────────────────────────────── */

  function renderSteps(steps) {
    const container = document.querySelector('.steps-container');
    if (!container || !steps.length) return;

    const ordered = STEP_ORDER
      .map(name => steps.find(s => s.stepName === name) || null)
      .filter(Boolean);

    container.innerHTML = ordered.map((s, i) => `
      <div class="step ${stepStatusClass(s.status)}">
        <div class="step-number">${i + 1}</div>
        <div class="step-content">
          <h3>${s.displayName || s.stepName}</h3>
          <p>${s.description || ''}</p>
          <span class="step-status">${stepStatusLabel(s.status)}</span>
        </div>
      </div>`).join('');
  }

  /* ── Render checklists from steps ────────────────────────────────────── */

  function renderChecklists(steps) {
    // Update the "Artifacts" checklist card based on ArtifactUpload step
    const artifactStep = steps.find(s => s.stepName === 'ArtifactUpload');
    const checklistStatus = document.querySelector('.checklist-card:nth-child(2) .checklist-status');
    if (checklistStatus && artifactStep) {
      checklistStatus.className = `checklist-status ${stepStatusClass(artifactStep.status)}`;
      checklistStatus.textContent = artifactStep.status === 'Completed'
        ? 'All Complete'
        : artifactStep.status === 'InProgress'
          ? `In Progress`
          : 'Pending';
    }
  }

  /* ── Chatbot ─────────────────────────────────────────────────────────── */

  function appendMessage(content, isBot) {
    const msgs = document.getElementById('chatbotMessages');
    if (!msgs) return;
    const msg = document.createElement('div');
    msg.className = `message ${isBot ? 'bot-message' : 'user-message'}`;
    msg.innerHTML = isBot
      ? `<div class="message-avatar"><i class="bi bi-robot"></i></div>
         <div class="message-content"><p>${content}</p><span class="message-time">just now</span></div>`
      : `<div class="message-content"><p>${content}</p><span class="message-time">just now</span></div>
         <div class="message-avatar"><i class="bi bi-person-circle"></i></div>`;
    msgs.appendChild(msg);
    msgs.scrollTop = msgs.scrollHeight;
  }

  async function handleChatbotQuery(text) {
    const lower = text.toLowerCase();

    if (lower.includes('status') || lower.includes('workflow')) {
      try {
        const steps = await window.BpoApi.processes.workflowSteps(_processId);
        const current = steps.find(s => s.status === 'InProgress') || steps[0];
        appendMessage(
          `Your process "<strong>${_process?.name || 'current'}</strong>" is at step:
           <strong>${current?.displayName || current?.stepName || 'Unknown'}</strong>.
           ${steps.filter(s => s.status === 'Completed').length}/${steps.length} steps completed.`,
          true
        );
      } catch (_) {
        appendMessage('I could not retrieve the workflow status right now.', true);
      }
    } else if (lower.includes('analys') || lower.includes('ai')) {
      appendMessage('Starting AI analysis…', true);
      try {
        const result = await window.BpoApi.dashboard.analyzeProcess(_processId);
        appendMessage(
          `✅ AI Analysis complete! Automation potential: <strong>${Math.round(result.automationScore)}%</strong>,
           Compliance score: <strong>${Math.round(result.complianceScore)}%</strong>.
           Key insight: ${result.summary || 'Review the analysis page for details.'}`,
          true
        );
      } catch (err) {
        appendMessage(`Could not run analysis: ${err.message}`, true);
      }
    } else if (lower.includes('upload')) {
      appendMessage('You can upload process artifacts on the <a href="upload.html">Upload page</a>.', true);
    } else if (lower.includes('kanban') || lower.includes('task')) {
      appendMessage('Track your tasks on the <a href="kanban.html">Kanban Board</a>.', true);
    } else {
      appendMessage(
        `I can help with: <em>workflow status, AI analysis, uploading artifacts</em>.
         Try asking "What's the status?" or "Run AI analysis".`,
        true
      );
    }
  }

  function wireChatbot() {
    const input   = document.querySelector('.chatbot-text');
    const sendBtn = document.querySelector('.chatbot-input .btn');
    if (!input || !sendBtn) return;

    const send = async () => {
      const text = input.value.trim();
      if (!text) return;
      appendMessage(text, false);
      input.value = '';
      sendBtn.disabled = true;
      await handleChatbotQuery(text);
      sendBtn.disabled = false;
    };

    sendBtn.addEventListener('click', send);
    input.addEventListener('keypress', e => { if (e.key === 'Enter') send(); });

    // Minimise button
    document.getElementById('minimizeChatbot')?.addEventListener('click', () => {
      const msgs = document.getElementById('chatbotMessages');
      if (msgs) msgs.style.display = msgs.style.display === 'none' ? '' : 'none';
    });
  }

  /* ── Quick Actions ───────────────────────────────────────────────────── */

  function wireQuickActions() {
    const exportBtn = [...document.querySelectorAll('.btn')].find(b => b.textContent.includes('Export Report'));
    if (exportBtn) {
      exportBtn.addEventListener('click', async () => {
        exportBtn.disabled = true;
        exportBtn.textContent = 'Generating…';
        try {
          const steps = await window.BpoApi.processes.workflowSteps(_processId);
          const data  = {
            process: _process,
            workflowSteps: steps,
            exportedAt: new Date().toISOString(),
          };
          const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
          const url  = URL.createObjectURL(blob);
          const a    = Object.assign(document.createElement('a'), {
            href:     url,
            download: `bpo-process-report-${_processId}.json`,
          });
          a.click();
          URL.revokeObjectURL(url);
          window.BpoNotifications?.showToast('Export', 'Report downloaded.', 'success');
        } catch (err) {
          window.BpoNotifications?.showToast('Export Failed', err.message, 'error');
        } finally {
          exportBtn.disabled    = false;
          exportBtn.textContent = 'Export Report';
        }
      });
    }
  }

  /* ── Bootstrap ───────────────────────────────────────────────────────── */

  async function init() {
    await resolveProcess();
    if (_processId) {
      try {
        const steps = await window.BpoApi.processes.workflowSteps(_processId);
        renderSteps(steps);
        renderChecklists(steps);
      } catch (err) {
        console.error('[workflow-api] Failed to load workflow steps:', err);
        window.BpoNotifications?.showToast('Error', 'Could not load workflow data.', 'error');
      }
    }
    wireChatbot();
    wireQuickActions();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
