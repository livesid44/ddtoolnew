/**
 * notifications.js – Real-time notification hub client using SignalR.
 *
 * Features:
 *  • Connects to /hubs/notifications on the API server
 *  • Displays toast notifications for ProcessStatusChanged, ArtifactUploaded,
 *    AiAnalysisComplete, and GenericNotification messages
 *  • Automatic reconnect with exponential back-off
 *  • Falls back gracefully when SignalR library is not loaded
 *
 * Depends on: app-config.js, auth.js, @microsoft/signalr CDN
 */

window.BpoNotifications = (function () {
  let _connection = null;
  let _toastContainer = null;

  /* ── Toast UI ─────────────────────────────────────────────────────────── */

  function _ensureToastContainer() {
    if (_toastContainer) return;
    _toastContainer = document.createElement('div');
    _toastContainer.id = 'bpo-toast-container';
    _toastContainer.style.cssText =
      'position:fixed;top:16px;right:16px;z-index:9999;display:flex;flex-direction:column;gap:8px;';
    document.body.appendChild(_toastContainer);
  }

  /**
   * Show a toast notification.
   * @param {string} title
   * @param {string} message
   * @param {'info'|'success'|'warning'|'error'} type
   */
  function showToast(title, message, type = 'info') {
    _ensureToastContainer();
    const colors = {
      info:    '#2196F3',
      success: '#4CAF50',
      warning: '#FF9800',
      error:   '#F44336',
    };
    const icons = { info: 'ℹ️', success: '✅', warning: '⚠️', error: '❌' };

    const toast = document.createElement('div');
    toast.style.cssText = [
      `background:${colors[type] || colors.info}`,
      'color:#fff',
      'padding:12px 16px',
      'border-radius:8px',
      'min-width:280px',
      'max-width:380px',
      'box-shadow:0 4px 12px rgba(0,0,0,.25)',
      'font-family:inherit',
      'opacity:0',
      'transform:translateX(40px)',
      'transition:opacity .25s,transform .25s',
    ].join(';');

    toast.innerHTML = `
      <div style="display:flex;align-items:center;gap:8px;">
        <span style="font-size:1.2rem">${icons[type] || icons.info}</span>
        <div>
          <div style="font-weight:600;font-size:.9rem">${title}</div>
          ${message ? `<div style="font-size:.8rem;opacity:.9;margin-top:2px">${message}</div>` : ''}
        </div>
        <button onclick="this.parentElement.parentElement.remove()"
          style="margin-left:auto;background:none;border:none;color:#fff;cursor:pointer;font-size:1rem;opacity:.8">✕</button>
      </div>`;

    _toastContainer.appendChild(toast);
    // Trigger CSS transition
    requestAnimationFrame(() => {
      toast.style.opacity = '1';
      toast.style.transform = 'translateX(0)';
    });
    // Auto-dismiss after 5 s
    setTimeout(() => {
      toast.style.opacity = '0';
      toast.style.transform = 'translateX(40px)';
      setTimeout(() => toast.remove(), 300);
    }, 5000);
  }

  /* ── SignalR connection ───────────────────────────────────────────────── */

  async function connect() {
    if (typeof signalR === 'undefined') {
      console.warn('[BpoNotifications] SignalR library not loaded.');
      return;
    }

    const hubUrl = window.BpoConfig.signalRHubUrl;

    _connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: async () => await window.BpoAuth.getToken() || '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // ── Message handlers ──────────────────────────────────────────────────
    _connection.on('ProcessStatusChanged', ({ processName, newStatus }) => {
      showToast('Status Updated', `${processName} → ${newStatus}`, 'info');
    });

    _connection.on('ArtifactUploaded', ({ processName, fileName }) => {
      showToast('Artifact Uploaded', `${fileName} added to ${processName}`, 'success');
    });

    _connection.on('AiAnalysisComplete', ({ processName, automationScore }) => {
      showToast(
        'AI Analysis Complete',
        `${processName} scored ${automationScore}% automation potential`,
        'success'
      );
    });

    _connection.on('Notification', ({ title, message, type }) => {
      showToast(title, message, type || 'info');
    });

    _connection.onreconnecting(() =>
      showToast('Connection', 'Reconnecting to notification service…', 'warning')
    );
    _connection.onreconnected(() =>
      showToast('Connection', 'Reconnected to notification service', 'success')
    );

    try {
      await _connection.start();
      console.info('[BpoNotifications] Connected to hub:', hubUrl);
    } catch (err) {
      // Hub may not be reachable in dev without the API running – fail silently
      console.warn('[BpoNotifications] Could not connect to notification hub:', err.message);
    }
  }

  async function disconnect() {
    if (_connection) await _connection.stop();
  }

  // Auto-connect when DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', connect);
  } else {
    connect();
  }

  return { connect, disconnect, showToast };
})();
