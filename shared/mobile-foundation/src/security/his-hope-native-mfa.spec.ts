import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  HisHopeNativeMfaBridgeError,
  createHisHopeNativeMfaBridge,
} from "./his-hope-native-mfa";

const options = { challenge: "server-challenge", rpId: "identity.his-hope.example" };
const assertion = {
  id: "credential-id",
  rawId: "credential-id",
  type: "public-key",
  response: {
    clientDataJSON: "client-data",
    authenticatorData: "authenticator-data",
    signature: "signature",
    userHandle: "user-handle",
  },
};

describe("HisHope native MFA bridge", () => {
  it("keeps ticket-bound options and completion inside approveMfa on success", async () => {
    const calls: string[] = [];
    const bridge = createHisHopeNativeMfaBridge({
      passkey: {
        isSupported: async () => true,
        authenticate: async (actualOptions) => {
          assert.deepEqual(actualOptions, options);
          return assertion;
        },
      },
      server: {
        requestOptions: async (ticket) => {
          calls.push(`options:${ticket}`);
          return { options };
        },
        complete: async (ticket, actualAssertion) => {
          calls.push(`complete:${ticket}`);
          assert.deepEqual(actualAssertion, assertion);
          return { approved: true };
        },
      },
    });

    const result = await bridge.approveMfa({ ticket: "ticket-123" });

    assert.deepEqual(result, { approved: true, status: "approved" });
    assert.deepEqual(calls, ["options:ticket-123", "complete:ticket-123"]);
  });

  it("returns cancelled when the native passkey prompt is dismissed", async () => {
    const bridge = createHisHopeNativeMfaBridge({
      passkey: {
        isSupported: async () => true,
        authenticate: async () => {
          throw new HisHopeNativeMfaBridgeError("cancelled", "Native prompt cancelled");
        },
      },
      server: {
        requestOptions: async () => ({ options }),
        complete: async () => ({ approved: true }),
      },
    });

    assert.deepEqual(await bridge.approveMfa({ ticket: "ticket-123" }), {
      approved: false,
      status: "cancelled",
      reason: "Native prompt cancelled",
    });
  });

  it("returns unsupported without requesting a server challenge when native passkeys are unavailable", async () => {
    let optionsRequested = false;
    const bridge = createHisHopeNativeMfaBridge({
      passkey: {
        isSupported: async () => false,
        authenticate: async () => assertion,
      },
      server: {
        requestOptions: async () => {
          optionsRequested = true;
          return { options };
        },
        complete: async () => ({ approved: true }),
      },
    });

    assert.deepEqual(await bridge.approveMfa({ ticket: "ticket-123" }), {
      approved: false,
      status: "unsupported",
      reason: "Native passkey approval is not supported on this device.",
    });
    assert.equal(optionsRequested, false);
  });

  it("returns expired when the one-time ticket has expired before assertion", async () => {
    const bridge = createHisHopeNativeMfaBridge({
      passkey: {
        isSupported: async () => true,
        authenticate: async () => assertion,
      },
      server: {
        requestOptions: async () => {
          throw new HisHopeNativeMfaBridgeError("expired", "MFA approval ticket expired");
        },
        complete: async () => ({ approved: true }),
      },
    });

    assert.deepEqual(await bridge.approveMfa({ ticket: "ticket-123" }), {
      approved: false,
      status: "expired",
      reason: "MFA approval ticket expired",
    });
  });

  it("returns rejected when the server refuses the assertion", async () => {
    const bridge = createHisHopeNativeMfaBridge({
      passkey: {
        isSupported: async () => true,
        authenticate: async () => assertion,
      },
      server: {
        requestOptions: async () => ({ options }),
        complete: async () => ({ approved: false }),
      },
    });

    assert.deepEqual(await bridge.approveMfa({ ticket: "ticket-123" }), {
      approved: false,
      status: "rejected",
      reason: "Native MFA assertion was rejected.",
    });
  });
});
