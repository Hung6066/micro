import test from "node:test";
import assert from "node:assert/strict";
import { HisHopePushRegistrationCoordinator } from "./notifications.js";

test("HisHopePushRegistrationCoordinator registers the token through the platform adapter", async () => {
  const calls: string[] = [];
  const coordinator = new HisHopePushRegistrationCoordinator(
    {
      register: async () => "fcm-token",
      unregister: async () => { calls.push("unregister"); },
    },
    {
      registerToken: async (token, platform) => { calls.push(`${platform}:${token}`); },
    },
    "android",
  );

  assert.equal(await coordinator.register(), "fcm-token");
  await coordinator.unregister();
  assert.deepEqual(calls, ["android:fcm-token", "unregister"]);
});

test("HisHopePushRegistrationCoordinator does not register an empty token", async () => {
  let registered = false;
  const coordinator = new HisHopePushRegistrationCoordinator(
    { register: async () => null, unregister: async () => undefined },
    { registerToken: async () => { registered = true; } },
    "ios",
  );

  assert.equal(await coordinator.register(), null);
  assert.equal(registered, false);
});
