import test from "node:test";
import assert from "node:assert/strict";
import { HisHopeWebCryptoDpopProofService } from "./index";

test("HisHopeWebCryptoDpopProofService creates a sender-constrained proof", async () => {
  const values = new Map<string, string>();
  const service = new HisHopeWebCryptoDpopProofService({
    get: async key => values.get(key) ?? null,
    set: async (key, value) => { values.set(key, value); },
    remove: async key => { values.delete(key); },
  });

  const proof = await service.createProof("https://api.example.test/records?patient=1", "get", "access-token");
  const [header, payload, signature] = proof.split(".");
  const decodedHeader = JSON.parse(Buffer.from(header, "base64url").toString("utf8")) as { typ: string; alg: string; jwk: { kty: string; crv: string } };
  const decodedPayload = JSON.parse(Buffer.from(payload, "base64url").toString("utf8")) as { htm: string; htu: string; ath: string; jti: string };

  assert.equal(decodedHeader.typ, "dpop+jwt");
  assert.equal(decodedHeader.alg, "ES256");
  assert.equal(decodedHeader.jwk.kty, "EC");
  assert.equal(decodedHeader.jwk.crv, "P-256");
  assert.equal(decodedPayload.htm, "GET");
  assert.equal(decodedPayload.htu, "https://api.example.test/records");
  assert.ok(decodedPayload.ath);
  assert.ok(decodedPayload.jti);
  assert.ok(signature.length > 0);
  assert.equal(values.size, 1);
});
