(() => {
  const encode = value => {
    const bytes = new Uint8Array(value);
    let binary = '';
    bytes.forEach(byte => binary += String.fromCharCode(byte));
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
  };

  const decode = value => Uint8Array.from(
    atob(value.replace(/-/g, '+').replace(/_/g, '/') + '='.repeat((4 - value.length % 4) % 4)),
    character => character.charCodeAt(0));

  const prepare = options => {
    options.challenge = decode(options.challenge);
    if (options.user) options.user.id = decode(options.user.id);
    if (options.allowCredentials) options.allowCredentials.forEach(item => item.id = decode(item.id));
    if (options.excludeCredentials) options.excludeCredentials.forEach(item => item.id = decode(item.id));
    return options;
  };

  const serialize = credential => {
    const response = credential.response;
    const result = {
      id: credential.id,
      rawId: encode(credential.rawId),
      type: credential.type,
      response: {
        clientDataJSON: encode(response.clientDataJSON),
        authenticatorData: response.authenticatorData ? encode(response.authenticatorData) : undefined,
        signature: response.signature ? encode(response.signature) : undefined,
        attestationObject: response.attestationObject ? encode(response.attestationObject) : undefined
      }
    };
    if (response.userHandle) result.response.userHandle = encode(response.userHandle);
    return result;
  };

  const showError = (element, error) => {
    if (!element) return;
    element.textContent = error instanceof Error ? error.message : String(error);
    element.hidden = false;
  };

  const readProblem = async (response, fallback) => {
    try {
      const body = await response.clone().json();
      return body.detail || body.title || body.errorDescription || body.error || fallback;
    } catch {
      const text = await response.text();
      return text || fallback;
    }
  };

  const wait = timeoutMs => new Promise(resolve => window.setTimeout(resolve, timeoutMs));
  const DEFAULT_NATIVE_APPROVAL_TICKET_LIFETIME_MS = 5 * 60 * 1000;
  const NATIVE_APPROVAL_CLIENT_BUFFER_MS = 15 * 1000;
  const NATIVE_APPROVAL_MIN_TIMEOUT_MS = 60 * 1000;
  const NATIVE_APPROVAL_INITIAL_INTERVAL_MS = 1000;
  const NATIVE_APPROVAL_MAX_INTERVAL_MS = 5000;

  const passkeyButton = document.getElementById('passkey-button');
  if (passkeyButton) {
    const email = document.getElementById('email');
    const error = document.getElementById('passkey-error');
    const returnUrl = document.querySelector('input[name="returnUrl"]')?.value || '/';

    passkeyButton.addEventListener('click', async () => {
      if (error) error.hidden = true;
      passkeyButton.disabled = true;
      try {
        if (!window.PublicKeyCredential || !navigator.credentials)
          throw new Error('This browser does not support passkeys.');
        const userName = email?.value.trim();
        if (!userName) throw new Error('Enter your email address before using a passkey.');

        const optionsResponse = await fetch('/api/v1/auth/passkeys/authenticate/options', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ userName })
        });
        if (!optionsResponse.ok) throw new Error(await readProblem(optionsResponse, 'No passkey is registered for this account.'));
        const payload = await optionsResponse.json();
        const credential = await navigator.credentials.get({ publicKey: prepare(payload.options) });
        if (!credential) throw new Error('Passkey authentication was cancelled.');

        const complete = await fetch('/api/v1/auth/passkeys/authenticate/complete', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ userId: payload.userId, returnUrl, response: serialize(credential) })
        });
        if (!complete.ok) throw new Error(await readProblem(complete, 'Passkey authentication failed.'));
        const result = await complete.json();
        window.location.assign(result.redirectUrl || returnUrl);
      } catch (exception) {
        showError(error, exception);
        passkeyButton.disabled = false;
      }
    });
  }

  const registerButton = document.getElementById('register');
  if (registerButton) {
    const status = document.getElementById('status');
    registerButton.addEventListener('click', async () => {
      registerButton.disabled = true;
      if (status) status.textContent = 'Use your device to continue.';
      try {
        if (!window.PublicKeyCredential || !navigator.credentials)
          throw new Error('This browser does not support passkeys.');
        const start = await fetch('/api/v1/auth/passkeys/register/options', { method: 'POST' });
        if (!start.ok) throw new Error(await readProblem(start, 'Unable to start passkey registration.'));
        const credential = await navigator.credentials.create({ publicKey: prepare(await start.json()) });
        if (!credential) throw new Error('Registration was cancelled.');
        const complete = await fetch('/api/v1/auth/passkeys/register/complete', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(serialize(credential))
        });
        if (!complete.ok) throw new Error(await readProblem(complete, 'Passkey registration failed.'));
        if (status) status.textContent = 'Passkey registered successfully.';
      } catch (exception) {
        showError(status, exception);
        registerButton.disabled = false;
      }
    });
  }

  const verificationRoot = document.querySelector('[data-mfa-methods-endpoint]');
  if (!verificationRoot) return;

  const methodsEndpoint = verificationRoot.getAttribute('data-mfa-methods-endpoint') || '/api/v1/auth/mfa/methods';
  const initialPreferredMethod = verificationRoot.getAttribute('data-preferred-method') || '';
  const initialAvailableMethods = (verificationRoot.getAttribute('data-available-methods') || '')
    .split(',')
    .map(value => value.trim())
    .filter(Boolean);

  const primaryActions = document.getElementById('primary-actions');
  const alternateActions = document.getElementById('alternate-actions');
  const passkeyMfaButton = document.getElementById('passkey-mfa');
  const nativeMfaButton = document.getElementById('native-passkey-mfa');
  const alternateMethodsButton = document.getElementById('alternate-methods');
  const alternatePanel = document.getElementById('alternate-method-panel');
  const totpForm = document.getElementById('totp-form');
  const totpInput = document.getElementById('totp-code');
  const totpSubmit = totpForm?.querySelector('button[type="submit"]');
  const status = document.getElementById('mfa-status');
  const error = document.getElementById('mfa-error');

  if (!primaryActions || !alternateActions || !passkeyMfaButton || !nativeMfaButton || !alternateMethodsButton || !alternatePanel || !totpForm || !totpInput || !totpSubmit)
    return;

  const state = {
    preferredMethod: initialPreferredMethod || null,
    availableMethods: initialAvailableMethods,
    alternateOpen: false,
    busyMethod: null
  };

  const hasMethod = method => state.availableMethods.includes(method);
  const clearFeedback = () => {
    if (status) status.textContent = '';
    if (error) {
      error.textContent = '';
      error.hidden = true;
    }
  };
  const setStatus = message => {
    if (status) status.textContent = message;
  };
  const setError = message => {
    if (!error) return;
    error.textContent = message;
    error.hidden = !message;
  };

  const syncMethods = methods => {
    const available = Array.isArray(methods?.availableMethods)
      ? methods.availableMethods.filter(value => typeof value === 'string')
      : [];
    state.availableMethods = available;

    if (typeof methods?.preferredMethod === 'string' && methods.preferredMethod) {
      state.preferredMethod = methods.preferredMethod;
    }

    if (!hasMethod(state.preferredMethod)) {
      state.preferredMethod = hasMethod('mobileApproval')
        ? 'mobileApproval'
        : hasMethod('passkey')
          ? 'passkey'
          : hasMethod('totp')
            ? 'totp'
            : null;
    }
  };

  const render = () => {
    const passkeyAvailable = hasMethod('passkey');
    const mobileAvailable = hasMethod('mobileApproval');
    const totpAvailable = hasMethod('totp');
    const mobilePrimary = state.preferredMethod === 'mobileApproval' && mobileAvailable;
    const hasAlternates = (mobileAvailable && !mobilePrimary) || totpAvailable;

    if (mobilePrimary) {
      if (nativeMfaButton.parentElement !== primaryActions) {
        primaryActions.insertBefore(nativeMfaButton, passkeyMfaButton);
      }
    } else if (nativeMfaButton.parentElement !== alternateActions) {
      alternateActions.prepend(nativeMfaButton);
    }

    passkeyMfaButton.hidden = !passkeyAvailable;
    nativeMfaButton.hidden = !mobileAvailable;
    alternateMethodsButton.hidden = !hasAlternates;
    alternateMethodsButton.textContent = !passkeyAvailable && !mobileAvailable && totpAvailable
      ? 'Use authenticator code'
      : 'Use another method';
    alternateMethodsButton.setAttribute('aria-expanded', state.alternateOpen ? 'true' : 'false');

    alternatePanel.hidden = !state.alternateOpen;
    totpForm.hidden = !totpAvailable || !state.alternateOpen;
    totpInput.disabled = !totpAvailable || state.busyMethod !== null;
    totpSubmit.disabled = !totpAvailable || state.busyMethod !== null;

    const busy = state.busyMethod !== null;
    passkeyMfaButton.disabled = !passkeyAvailable || busy;
    nativeMfaButton.disabled = !mobileAvailable || busy;
    alternateMethodsButton.disabled = busy;

    if (!passkeyAvailable && !mobileAvailable && !totpAvailable) {
      setError('This sign-in does not have an available verification method. Sign in again or contact support.');
    }
  };

  const loadMethods = async () => {
    const response = await fetch(methodsEndpoint, {
      method: 'GET',
      headers: { 'X-Requested-With': 'XMLHttpRequest' }
    });
    if (!response.ok) {
      throw new Error(await readProblem(response, 'Unable to load available verification methods.'));
    }

    syncMethods(await response.json());
    render();
  };

  const submitPasskey = async () => {
    state.busyMethod = 'passkey';
    clearFeedback();
    setStatus('Use your device passkey to continue.');
    render();

    try {
      if (!window.PublicKeyCredential || !navigator.credentials)
        throw new Error('This device does not support passkeys. Use another method to continue.');

      const start = await fetch('/api/v1/auth/passkeys/mfa/options', { method: 'POST' });
      if (!start.ok) throw new Error(await readProblem(start, 'Unable to start passkey verification.'));
      const payload = await start.json();

      const credential = await navigator.credentials.get({ publicKey: prepare(payload.options) });
      if (!credential) throw new Error('Passkey verification was cancelled.');

      const complete = await fetch('/api/v1/auth/passkeys/mfa/complete', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ response: serialize(credential) })
      });
      if (!complete.ok) throw new Error(await readProblem(complete, 'Passkey verification failed.'));
      const result = await complete.json();
      window.location.assign(result.redirectUrl || '/');
    } catch (exception) {
      state.alternateOpen = true;
      setError(exception instanceof Error ? exception.message : String(exception));
    } finally {
      state.busyMethod = null;
      render();
    }
  };

  const getNativeApprovalPollTimeout = expiresInMs => {
    const serverLifetimeMs = Number.isFinite(expiresInMs) && expiresInMs > 0
      ? expiresInMs
      : DEFAULT_NATIVE_APPROVAL_TICKET_LIFETIME_MS;

    return Math.max(NATIVE_APPROVAL_MIN_TIMEOUT_MS, serverLifetimeMs - NATIVE_APPROVAL_CLIENT_BUFFER_MS);
  };

  const openNativeApprovalWindow = () => {
    const launchWindow = window.open('', '_blank');
    if (!launchWindow) return null;

    try {
      launchWindow.opener = null;
    } catch {
      // Best effort only.
    }

    return launchWindow;
  };

  const navigateNativeApprovalWindow = (launchWindow, deepLink) => {
    if (launchWindow && !launchWindow.closed) {
      try {
        launchWindow.location.replace(deepLink);
        return;
      } catch {
        try {
          launchWindow.location.href = deepLink;
          return;
        } catch {
          // Fall through to same-tab navigation.
        }
      }

      launchWindow.close();
    }

    window.location.assign(deepLink);
  };

  const pollNativeApproval = async (ticket, timeoutMs) => {
    const deadline = Date.now() + timeoutMs;
    let intervalMs = NATIVE_APPROVAL_INITIAL_INTERVAL_MS;

    while (Date.now() < deadline) {
      await wait(intervalMs);

      const response = await fetch(`/api/v1/auth/passkeys/mfa/native/poll?ticket=${encodeURIComponent(ticket)}`);
      if (response.status === 202) {
        intervalMs = Math.min(Math.round(intervalMs * 1.35), NATIVE_APPROVAL_MAX_INTERVAL_MS);
        continue;
      }

      if (response.status === 409) {
        throw new Error(await readProblem(response, 'Mobile approval was rejected in the His.Hope mobile app.'));
      }

      if (response.status === 410) {
        throw new Error(await readProblem(response, 'Mobile approval expired. Retry the request from this page.'));
      }

      if (!response.ok) {
        throw new Error(await readProblem(response, 'Unable to confirm mobile approval.'));
      }

      return response.json();
    }

    throw new Error('Mobile approval timed out before the server ticket expired. Retry the request from this page.');
  };

  const startNativeApproval = async () => {
    state.busyMethod = 'mobileApproval';
    state.alternateOpen = true;
    clearFeedback();
    setStatus('Approve this sign-in in the His.Hope mobile app.');
    render();

    const launchWindow = openNativeApprovalWindow();

    try {
      const start = await fetch('/api/v1/auth/passkeys/mfa/native/start', { method: 'POST' });
      if (!start.ok) throw new Error(await readProblem(start, 'Unable to start mobile approval.'));

      const payload = await start.json();
      navigateNativeApprovalWindow(launchWindow, payload.deepLink);

      const result = await pollNativeApproval(payload.ticket, getNativeApprovalPollTimeout(payload.expiresInMs));
      window.location.assign(result.redirectUrl || '/');
    } catch (exception) {
      if (launchWindow && !launchWindow.closed) {
        launchWindow.close();
      }
      setError(exception instanceof Error ? exception.message : String(exception));
    } finally {
      state.busyMethod = null;
      render();
    }
  };

  const submitTotp = async event => {
    event.preventDefault();
    state.alternateOpen = true;
    clearFeedback();

    const code = `${totpInput.value || ''}`.trim();
    if (!/^\d{6}$/.test(code)) {
      setError('Enter the six-digit authenticator code.');
      render();
      return;
    }

    state.busyMethod = 'totp';
    setStatus('Verifying authenticator code.');
    render();

    try {
      const response = await fetch('/api/v1/auth/mfa/verify', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code })
      });
      if (!response.ok) throw new Error(await readProblem(response, 'TOTP verification failed.'));

      const result = await response.json();
      window.location.assign(result.redirectUrl || '/');
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : String(exception));
    } finally {
      state.busyMethod = null;
      render();
    }
  };

  passkeyMfaButton.addEventListener('click', submitPasskey);
  nativeMfaButton.addEventListener('click', startNativeApproval);
  alternateMethodsButton.addEventListener('click', () => {
    if (state.busyMethod !== null) return;
    state.alternateOpen = !state.alternateOpen;
    clearFeedback();
    render();
  });
  totpForm.addEventListener('submit', submitTotp);

  syncMethods({
    preferredMethod: state.preferredMethod,
    availableMethods: state.availableMethods
  });
  render();

  loadMethods().catch(exception => {
    setError(exception instanceof Error ? exception.message : String(exception));
    render();
  });
})();
