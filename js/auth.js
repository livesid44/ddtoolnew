/**
 * auth.js – MSAL.js v3 authentication module.
 *
 * Responsibilities:
 *  • Initialise MSAL PublicClientApplication
 *  • Silent token acquisition with interactive fallback (popup)
 *  • Expose BpoAuth.login(), BpoAuth.logout(), BpoAuth.getToken(),
 *    BpoAuth.getUser(), BpoAuth.isAuthenticated()
 *  • In Development (devBypass = true) skip MSAL entirely and return a
 *    fake dev token so the dev-bypass auth handler on the API is satisfied.
 *
 * Depends on: app-config.js, MSAL.js CDN script (msal-browser@3)
 */

window.BpoAuth = (function () {
  const cfg = window.BpoConfig.msal;
  let _msalApp = null;
  let _account = null;

  /* ── Dev bypass ───────────────────────────────────────────────────────── */

  if (cfg.devBypass) {
    console.info('[BpoAuth] Dev bypass active – MSAL is disabled.');
    return {
      isAuthenticated: () => true,
      getUser: () => ({ name: 'Dev User', username: 'dev@localhost', roles: ['Admin'] }),
      getToken: async () => 'dev-bypass-token',
      login:    async () => {},
      logout:   () => {},
      handleRedirect: async () => {},
    };
  }

  /* ── MSAL config ──────────────────────────────────────────────────────── */

  function _buildMsalApp() {
    if (typeof msal === 'undefined') {
      console.warn('[BpoAuth] MSAL library not loaded – auth disabled.');
      return null;
    }
    return new msal.PublicClientApplication({
      auth: {
        clientId:    cfg.clientId,
        authority:   `https://login.microsoftonline.com/${cfg.tenantId}`,
        redirectUri: window.location.origin,
      },
      cache: { cacheLocation: 'localStorage', storeAuthStateInCookie: false },
    });
  }

  async function _init() {
    _msalApp = _buildMsalApp();
    if (!_msalApp) return;
    await _msalApp.initialize();
    // Handle redirect response after AAD returns the user to the app
    await _msalApp.handleRedirectPromise();
    const accounts = _msalApp.getAllAccounts();
    if (accounts.length > 0) _account = accounts[0];
  }

  /* ── Public API ───────────────────────────────────────────────────────── */

  async function handleRedirect() { await _init(); }

  function isAuthenticated() { return !!_account; }

  function getUser() {
    if (!_account) return null;
    return {
      name:     _account.name,
      username: _account.username,
      roles:    _account.idTokenClaims?.roles ?? [],
    };
  }

  async function getToken() {
    if (!_msalApp) return null;
    if (!_account) return null;
    try {
      const resp = await _msalApp.acquireTokenSilent({ scopes: cfg.scopes, account: _account });
      return resp.accessToken;
    } catch (err) {
      // Use name check as a safe fallback in case the MSAL class is not accessible
      const isInteractionRequired =
        (typeof msal !== 'undefined' && err instanceof msal.InteractionRequiredAuthError) ||
        err?.name === 'InteractionRequiredAuthError';
      if (isInteractionRequired) {
        const resp = await _msalApp.acquireTokenPopup({ scopes: cfg.scopes });
        return resp.accessToken;
      }
      console.error('[BpoAuth] Token acquisition failed:', err);
      return null;
    }
  }

  async function login() {
    if (!_msalApp) return;
    const resp = await _msalApp.loginPopup({ scopes: cfg.scopes });
    _account = resp.account;
  }

  function logout() {
    if (!_msalApp || !_account) return;
    _msalApp.logoutPopup({ account: _account });
    _account = null;
  }

  // Auto-initialise when the script is loaded
  _init().catch(console.error);

  return { isAuthenticated, getUser, getToken, login, logout, handleRedirect };
})();
