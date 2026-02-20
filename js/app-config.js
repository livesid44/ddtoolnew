/**
 * app-config.js
 * Single source of truth for all frontend configuration.
 * In production these values are injected by Azure Static Web Apps environment variables
 * or replaced at build time. In development, override via localStorage:
 *   localStorage.setItem('bpo_api_base_url', 'https://localhost:7123');
 */

window.BpoConfig = (function () {
  // Allow local override via localStorage for developer convenience
  const override = (key, fallback) =>
    localStorage.getItem(key) || fallback;

  return {
    /** Base URL of the BPO Platform REST API (no trailing slash) */
    apiBaseUrl: override('bpo_api_base_url', 'http://localhost:5000'),

    /** MSAL / Azure AD settings – replace __PLACEHOLDERS__ in production */
    msal: {
      clientId:   override('bpo_msal_client_id',   '__YOUR_CLIENT_ID__'),
      tenantId:   override('bpo_msal_tenant_id',   '__YOUR_TENANT_ID__'),
      /** OAuth 2.0 scopes to request from Azure AD */
      scopes:     ['openid', 'profile', 'offline_access',
                   override('bpo_msal_api_scope', 'api://__YOUR_CLIENT_ID__/BPOPlatform.Read')],
      /** Set to true to skip MSAL and use a dev token header instead */
      devBypass:  override('bpo_auth_dev_bypass', 'true') === 'true',
    },

    /** SignalR / real-time notifications hub URL */
    signalRHubUrl: override('bpo_signalr_url', 'http://localhost:5000/hubs/notifications'),

    /** Default process ID used by pages when no process is selected */
    defaultProcessId: override('bpo_default_process_id', null),
  };
})();
