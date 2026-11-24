import { splitUris } from "./clients-page.component";
import { isAllowedRedirectUri } from "./client-edit-dialog.component";

describe("clients page inline redirect URI edit", () => {
  it("splits comma, whitespace and newline separated URIs", () => {
    expect(
      splitUris(
        "https://consoles.example/cb, http://localhost:8250/oidc/callback\nhttp://127.0.0.1:8000  ",
      ),
    ).toEqual([
      "https://consoles.example/cb",
      "http://localhost:8250/oidc/callback",
      "http://127.0.0.1:8000",
    ]);
    expect(splitUris("")).toEqual([]);
    expect(splitUris(undefined)).toEqual([]);
  });

  it("the column validator rule rejects any clear-text non-loopback entry", () => {
    const valid = (value: string) => splitUris(value).every(isAllowedRedirectUri);
    expect(valid("https://a.example/cb, http://localhost:8000")).toBeTrue();
    expect(valid("https://a.example/cb, http://b.example/cb")).toBeFalse();
  });
});
