// Keep browser interception paths in one place. The server-side canonical
// contract lives in His.Hope.Contracts.Identity.IdentityApiRoutes; these
// values intentionally contain paths only, never environment-specific hosts.
const ApiV1 = '/api/v1';
const Auth = `${ApiV1}/auth`;
const Passkeys = `${Auth}/passkeys`;

const IdentityRoutes = Object.freeze({
  ApiV1,
  Auth,
  Login: `${Auth}/login`,
  SessionExchange: `${Auth}/session/exchange`,
  MfaMethods: `${Auth}/mfa/methods`,
  MfaVerify: `${Auth}/mfa/verify`,
  Passkeys,
  PasskeyMfaOptions: `${Passkeys}/mfa/options`,
  PasskeyMfaComplete: `${Passkeys}/mfa/complete`,
  NativeMfaStart: `${Passkeys}/mfa/native/start`,
  NativeMfaPoll: `${Passkeys}/mfa/native/poll`,
  NativeMfaOptions: `${Passkeys}/mfa/native/options`,
  PasskeyRegisterOptions: `${Passkeys}/register/options`,
  PasskeyAuthenticateOptions: `${Passkeys}/authenticate/options`,
});

module.exports = { IdentityRoutes };
