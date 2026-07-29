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
        if (!optionsResponse.ok) throw new Error('No passkey is registered for this account.');
        const payload = await optionsResponse.json();
        const credential = await navigator.credentials.get({ publicKey: prepare(payload.options) });
        if (!credential) throw new Error('Passkey authentication was cancelled.');

        const complete = await fetch('/api/v1/auth/passkeys/authenticate/complete', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ userId: payload.userId, returnUrl, response: serialize(credential) })
        });
        if (!complete.ok) throw new Error('Passkey authentication failed.');
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
        if (!start.ok) throw new Error('Unable to start passkey registration.');
        const credential = await navigator.credentials.create({ publicKey: prepare(await start.json()) });
        if (!credential) throw new Error('Registration was cancelled.');
        const complete = await fetch('/api/v1/auth/passkeys/register/complete', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(serialize(credential))
        });
        if (!complete.ok) throw new Error('Passkey registration failed.');
        if (status) status.textContent = 'Passkey registered successfully.';
      } catch (exception) {
        showError(status, exception);
        registerButton.disabled = false;
      }
    });
  }

  const mfaPasskeyButton = document.getElementById('passkey-mfa');
  if (mfaPasskeyButton) {
    const status = document.getElementById('passkey-status');
    mfaPasskeyButton.addEventListener('click', async () => {
      mfaPasskeyButton.disabled = true;
      if (status) status.textContent = 'Use your device to verify your identity.';
      try {
        if (!window.PublicKeyCredential || !navigator.credentials)
          throw new Error('This device does not support passkeys. Use the TOTP fallback.');
        const start = await fetch('/api/v1/auth/passkeys/mfa/options', { method: 'POST' });
        if (!start.ok) throw new Error('No MFA passkey is registered for this account.');
        const payload = await start.json();
        const credential = await navigator.credentials.get({ publicKey: prepare(payload.options) });
        if (!credential) throw new Error('Passkey verification was cancelled.');
        const complete = await fetch('/api/v1/auth/passkeys/mfa/complete', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ userId: payload.userId, response: serialize(credential) })
        });
        if (!complete.ok) throw new Error('Passkey MFA verification failed.');
        const result = await complete.json();
        window.location.assign(result.redirectUrl || '/');
      } catch (exception) {
        showError(status, exception);
        mfaPasskeyButton.disabled = false;
      }
    });
  }

  const nativeMfaButton = document.getElementById('native-passkey-mfa');
  if (nativeMfaButton) {
    const status = document.getElementById('native-passkey-status');
    const poll = async (ticket) => {
      for (let attempt = 0; attempt < 150; attempt += 1) {
        await new Promise(resolve => setTimeout(resolve, 1000));
        const response = await fetch(`/api/v1/auth/passkeys/mfa/native/poll?ticket=${encodeURIComponent(ticket)}`);
        if (response.status === 202) continue;
        if (!response.ok) throw new Error('Native MFA approval expired or was rejected.');
        const result = await response.json();
        window.location.assign(result.redirectUrl || '/');
        return;
      }
      throw new Error('Native MFA approval timed out.');
    };

    nativeMfaButton.addEventListener('click', async () => {
      nativeMfaButton.disabled = true;
      if (status) status.textContent = 'Approve this sign-in in the His.Hope mobile app.';
      try {
        const start = await fetch('/api/v1/auth/passkeys/mfa/native/start', { method: 'POST' });
        if (!start.ok) throw new Error('Unable to start native MFA approval.');
        const result = await start.json();
        // Keep this MFA page alive in the browser while the native app signs
        // the one-time server challenge.
        window.open(result.deepLink, '_blank');
        await poll(result.ticket);
      } catch (exception) {
        showError(status, exception);
        nativeMfaButton.disabled = false;
      }
    });
  }
})();
