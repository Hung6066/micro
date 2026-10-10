import { provideHttpClient } from "@angular/common/http";
import { provideHttpClientTesting } from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { MobilePlatformService } from "./mobile-platform.service";

const attestationEnvelope = {
  id: "credential-id",
  rawId: "credential-id",
  type: "public-key",
  response: {
    clientDataJSON: "client-data",
    attestationObject: "attestation-object",
  },
};

const assertionEnvelope = {
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

type PasskeyResponseParser = {
  parsePasskeyResponse(value: string | Record<string, unknown>): Readonly<Record<string, unknown>>;
};

describe("MobilePlatformService", () => {
  let parser: PasskeyResponseParser;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        MobilePlatformService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    parser = TestBed.inject(MobilePlatformService) as unknown as PasskeyResponseParser;
  });

  it("preserves the full native attestation envelope", () => {
    expect(parser.parsePasskeyResponse(JSON.stringify(attestationEnvelope))).toEqual(attestationEnvelope);
  });

  it("preserves the full native assertion envelope", () => {
    expect(parser.parsePasskeyResponse(JSON.stringify(assertionEnvelope))).toEqual(assertionEnvelope);
  });
});
