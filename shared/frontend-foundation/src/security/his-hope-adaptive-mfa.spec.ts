import {
  createHisHopeAdaptiveMfaState,
  getHisHopeAdaptiveMfaAlternateMethods,
  setHisHopeAdaptiveMfaAlternateMethodsOpen,
} from "./his-hope-adaptive-mfa";

describe("HisHope adaptive MFA", () => {
  it("orders methods deterministically and prefers passkey", () => {
    const state = createHisHopeAdaptiveMfaState({
      availableMethods: ["totp", "passkey", "mobileApproval", "passkey"],
    });

    expect(state.availableMethods).toEqual(["passkey", "mobileApproval", "totp"]);
    expect(state.preferredMethod).toBe("passkey");
  });

  it("prefers mobile approval on an unfamiliar device", () => {
    const state = createHisHopeAdaptiveMfaState({
      availableMethods: ["passkey", "mobileApproval"],
      unfamiliarDevice: true,
    });

    expect(state.preferredMethod).toBe("mobileApproval");
    expect(getHisHopeAdaptiveMfaAlternateMethods(state)).toEqual(["passkey"]);
  });

  it("keeps an explicit available preference", () => {
    const state = createHisHopeAdaptiveMfaState({
      availableMethods: ["passkey", "totp"],
      preferredMethod: "totp",
    });

    expect(state.preferredMethod).toBe("totp");
  });

  it("returns no preferred method when none are available", () => {
    const state = createHisHopeAdaptiveMfaState({ availableMethods: [] });

    expect(state.preferredMethod).toBeNull();
    expect(getHisHopeAdaptiveMfaAlternateMethods(state)).toEqual([]);
  });

  it("tracks alternate methods visibility without mutating the source", () => {
    const state = createHisHopeAdaptiveMfaState({ availableMethods: ["totp"] });
    const opened = setHisHopeAdaptiveMfaAlternateMethodsOpen(state, true);

    expect(state.alternateMethodsOpen).toBeFalse();
    expect(opened.alternateMethodsOpen).toBeTrue();
  });
});
