import { describe, it } from "node:test";
import assert from "node:assert/strict";
import type { HisHopeSecureStorage } from "../index";
import { HisHopeCachingSecureStorage } from "./his-hope-caching-secure-storage";

function fakeBackend(): HisHopeSecureStorage & { data: Map<string, string> } {
  const data = new Map<string, string>();
  return {
    data,
    async get(key) {
      return data.get(key) ?? null;
    },
    async set(key, value) {
      data.set(key, value);
    },
    async remove(key) {
      data.delete(key);
    },
  };
}

describe("HisHopeCachingSecureStorage", () => {
  it("reads back a value written before hydration completes", async () => {
    const storage = new HisHopeCachingSecureStorage(fakeBackend(), "ns");
    storage.write("token", "abc");
    assert.equal(storage.read("token"), "abc");
  });

  it("hydrates cached entries from the backend", async () => {
    const backend = fakeBackend();
    backend.data.set("ns", JSON.stringify({ token: "from-backend" }));
    const storage = new HisHopeCachingSecureStorage(backend, "ns");
    assert.equal(storage.read("token"), null);
    await storage.hydrate();
    assert.equal(storage.read("token"), "from-backend");
  });

  it("ignores a corrupted backend blob instead of throwing", async () => {
    const backend = fakeBackend();
    backend.data.set("ns", "not json");
    const storage = new HisHopeCachingSecureStorage(backend, "ns");
    await assert.doesNotReject(() => storage.hydrate());
    assert.equal(storage.read("anything"), null);
  });

  it("persists writes back to the backend under the namespace key", async () => {
    const backend = fakeBackend();
    const storage = new HisHopeCachingSecureStorage(backend, "ns");
    storage.write("a", "1");
    storage.write("b", "2");
    await new Promise((resolve) => setImmediate(resolve));
    assert.deepEqual(JSON.parse(backend.data.get("ns") ?? "{}"), {
      a: "1",
      b: "2",
    });
  });

  it("removes a single key without dropping the rest", async () => {
    const backend = fakeBackend();
    const storage = new HisHopeCachingSecureStorage(backend, "ns");
    storage.write("a", "1");
    storage.write("b", "2");
    storage.remove("a");
    assert.equal(storage.read("a"), null);
    assert.equal(storage.read("b"), "2");
  });

  it("clear() wipes the cache and deletes the backend entry", async () => {
    const backend = fakeBackend();
    const storage = new HisHopeCachingSecureStorage(backend, "ns");
    storage.write("a", "1");
    storage.clear();
    assert.equal(storage.read("a"), null);
    assert.equal(backend.data.has("ns"), false);
  });

  it("hydrate() only reads the backend once", async () => {
    let reads = 0;
    const backend = fakeBackend();
    const countingBackend: HisHopeSecureStorage = {
      get: async (key) => {
        reads++;
        return backend.get(key);
      },
      set: (key, value) => backend.set(key, value),
      remove: (key) => backend.remove(key),
    };
    const storage = new HisHopeCachingSecureStorage(countingBackend, "ns");
    await storage.hydrate();
    await storage.hydrate();
    assert.equal(reads, 1);
  });
});
