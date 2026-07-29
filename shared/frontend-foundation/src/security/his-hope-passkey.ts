export interface HisHopePasskeySupport {
  supported: boolean;
  platformAuthenticatorAvailable: boolean;
}

export interface HisHopePasskeyClient {
  support(): Promise<HisHopePasskeySupport>;
  create(options: PublicKeyCredentialCreationOptions): Promise<PublicKeyCredential>;
  get(options: PublicKeyCredentialRequestOptions): Promise<PublicKeyCredential>;
}

/** Browser WebAuthn adapter. Server endpoints own challenge generation and verification. */
export class HisHopeBrowserPasskeyClient implements HisHopePasskeyClient {
  async support(): Promise<HisHopePasskeySupport> {
    const supported = typeof window !== "undefined" &&
      typeof window.PublicKeyCredential !== "undefined" &&
      typeof navigator !== "undefined" && !!navigator.credentials;
    const platformAuthenticatorAvailable = supported &&
      typeof window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable === "function" &&
      await window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
    return { supported, platformAuthenticatorAvailable };
  }

  async create(options: PublicKeyCredentialCreationOptions): Promise<PublicKeyCredential> {
    const credential = await navigator.credentials.create({ publicKey: options });
    if (!(credential instanceof PublicKeyCredential)) throw new Error("Passkey registration was cancelled.");
    return credential;
  }

  async get(options: PublicKeyCredentialRequestOptions): Promise<PublicKeyCredential> {
    const credential = await navigator.credentials.get({ publicKey: options });
    if (!(credential instanceof PublicKeyCredential)) throw new Error("Passkey authentication was cancelled.");
    return credential;
  }
}

