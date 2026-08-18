import { HisHopeBrowserPasskeyClient } from "./his-hope-passkey";

describe("HisHopeBrowserPasskeyClient", () => {
  it("reports unsupported browsers", async () => {
    const client = new HisHopeBrowserPasskeyClient();
    const publicKeyCredential = Object.getOwnPropertyDescriptor(window, "PublicKeyCredential");
    Object.defineProperty(window, "PublicKeyCredential", {
      configurable: true,
      value: undefined,
    });

    try {
      await expectAsync(client.support()).toBeResolvedTo({
        supported: false,
        platformAuthenticatorAvailable: false,
      });
    } finally {
      if (publicKeyCredential) {
        Object.defineProperty(window, "PublicKeyCredential", publicKeyCredential);
      }
    }
  });

  it("reports browser support and platform authenticator availability", async () => {
    const platformAuthenticatorAvailable = spyOn(
      window.PublicKeyCredential,
      "isUserVerifyingPlatformAuthenticatorAvailable",
    ).and.resolveTo(true);
    const client = new HisHopeBrowserPasskeyClient();

    const result = await client.support();

    expect(result).toEqual({
      supported: true,
      platformAuthenticatorAvailable: true,
    });
    expect(platformAuthenticatorAvailable).toHaveBeenCalled();
  });

  it("rejects cancelled registration and authentication", async () => {
    spyOn(navigator.credentials, "create").and.resolveTo(null);
    spyOn(navigator.credentials, "get").and.resolveTo(null);
    const client = new HisHopeBrowserPasskeyClient();

    await expectAsync(client.create({} as PublicKeyCredentialCreationOptions))
      .toBeRejectedWithError("Passkey registration was cancelled.");
    await expectAsync(client.get({} as PublicKeyCredentialRequestOptions))
      .toBeRejectedWithError("Passkey authentication was cancelled.");
  });
});
