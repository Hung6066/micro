import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { isHisHopeDeepLinkAllowed } from "./his-hope-deep-link-allow-list";

const ALLOW_LIST = [
  { scheme: "hishope", host: "auth", pathPrefix: "/callback" },
  { scheme: "hishope", host: "auth", pathPrefix: "/logout-callback" },
  { scheme: "https", host: "mobile.his-hope.example", pathPrefix: "/auth/callback" },
  { scheme: "https", host: "mobile.his-hope.example", pathPrefix: "/auth/logout-callback" },
];

describe("isHisHopeDeepLinkAllowed", () => {
  it("allows the custom-scheme auth callback", () => {
    assert.equal(
      isHisHopeDeepLinkAllowed("hishope://auth/callback", ALLOW_LIST),
      true,
    );
  });

  it("allows the verified universal link under its path prefix", () => {
    assert.equal(
      isHisHopeDeepLinkAllowed(
        "https://mobile.his-hope.example/auth/logout-callback",
        ALLOW_LIST,
      ),
      true,
    );
  });

  it("rejects custom-scheme paths outside callback allow-list", () => {
    assert.equal(
      isHisHopeDeepLinkAllowed("hishope://auth/evil", ALLOW_LIST),
      false,
    );
  });

  it("rejects an unrelated host on an otherwise allow-listed scheme", () => {
    assert.equal(
      isHisHopeDeepLinkAllowed("hishope://evil/callback", ALLOW_LIST),
      false,
    );
  });

  it("rejects a universal link path outside the allowed prefix", () => {
    assert.equal(
      isHisHopeDeepLinkAllowed(
        "https://mobile.his-hope.example/other",
        ALLOW_LIST,
      ),
      false,
    );
  });

  it("rejects an unlisted scheme entirely", () => {
    assert.equal(
      isHisHopeDeepLinkAllowed("javascript://auth/callback", ALLOW_LIST),
      false,
    );
  });

  it("rejects a malformed URL instead of throwing", () => {
    assert.equal(isHisHopeDeepLinkAllowed("not a url", ALLOW_LIST), false);
  });
});
