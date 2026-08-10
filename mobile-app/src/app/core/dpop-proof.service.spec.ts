import { TestBed } from "@angular/core/testing";
import { DpopProofService } from "./dpop-proof.service";
import { NativeCapabilityService } from "./native-capability.service";

describe("DpopProofService", () => {
  let stored: string | null;
  let service: DpopProofService;

  beforeEach(() => {
    stored = null;
    TestBed.configureTestingModule({
      providers: [
        DpopProofService,
        {
          provide: NativeCapabilityService,
          useValue: {
            secureGet: async () => stored,
            secureSet: async (_key: string, value: string) => { stored = value; },
          },
        },
      ],
    });
    service = TestBed.inject(DpopProofService);
  });

  it("creates an ES256 proof and persists the device key", async () => {
    const proof = await service.createProof("https://api.example.test/connect/token", "POST");
    const [encodedHeader, encodedPayload, signature] = proof.split(".");
    const header = JSON.parse(atob(encodedHeader.replace(/-/g, "+").replace(/_/g, "/")));
    const payload = JSON.parse(atob(encodedPayload.replace(/-/g, "+").replace(/_/g, "/")));

    expect(header.typ).toBe("dpop+jwt");
    expect(header.alg).toBe("ES256");
    expect(header.jwk.kty).toBe("EC");
    expect(header.jwk.crv).toBe("P-256");
    expect(payload.htm).toBe("POST");
    expect(payload.htu).toBe("https://api.example.test/connect/token");
    expect(signature.length).toBeGreaterThan(80);
    expect(stored).toContain('"d"');
  });

  it("reuses the persisted key while issuing a fresh jti", async () => {
    const first = await service.createProof("https://api.example.test/patients", "GET");
    const second = await service.createProof("https://api.example.test/patients", "GET");
    const decode = (value: string) => JSON.parse(atob(value.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
    const firstHeader = JSON.parse(atob(first.split(".")[0].replace(/-/g, "+").replace(/_/g, "/")));
    const secondHeader = JSON.parse(atob(second.split(".")[0].replace(/-/g, "+").replace(/_/g, "/")));

    expect(decode(first).jti).not.toBe(decode(second).jti);
    expect(firstHeader.jwk.x).toBe(secondHeader.jwk.x);
    expect(firstHeader.jwk.y).toBe(secondHeader.jwk.y);
  });

  it("omits query and fragment from htu", async () => {
    const proof = await service.createProof(
      "https://api.example.test/patients?include=summary#details",
      "GET",
    );
    const payload = JSON.parse(atob(proof.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));

    expect(payload.htu).toBe("https://api.example.test/patients");
  });

  it("binds resource proofs to the access token with ath", async () => {
    const proof = await service.createProof(
      "https://api.example.test/patients",
      "GET",
      "access-token",
    );
    const payload = JSON.parse(atob(proof.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
    const expected = btoa(String.fromCharCode(...new Uint8Array(await crypto.subtle.digest(
      "SHA-256",
      new TextEncoder().encode("access-token"),
    )))).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/g, "");

    expect(payload.ath).toBe(expected);
  });
});
