const VALIDATION_ERROR_PRIORITY = [
  "required",
  "redirectUriRequired",
  "email",
  "min",
  "max",
  "minlength",
  "maxlength",
  "pattern",
  "httpsUri",
  "invalidJson",
] as const;

/** Pick the most actionable validation error key for display. */
export function firstValidationErrorKey(
  errors: Record<string, unknown>,
): string {
  for (const key of VALIDATION_ERROR_PRIORITY) {
    if (key in errors) return key;
  }
  return Object.keys(errors)[0];
}
