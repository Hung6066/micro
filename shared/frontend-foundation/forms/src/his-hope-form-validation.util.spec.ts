import { firstValidationErrorKey } from "./his-hope-form-validation.util";

describe("his-hope-form-validation.util", () => {
  it("prioritizes required errors over format errors", () => {
    expect(
      firstValidationErrorKey({
        httpsUri: true,
        required: true,
      }),
    ).toBe("required");
  });

  it("prioritizes redirect-uri-required before https format errors", () => {
    expect(
      firstValidationErrorKey({
        httpsUri: true,
        redirectUriRequired: true,
      }),
    ).toBe("redirectUriRequired");
  });
});
