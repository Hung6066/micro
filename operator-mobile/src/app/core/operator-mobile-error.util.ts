import type { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";

/** Converts API problem statuses into stable, localized field-app guidance. */
export function operatorMobileErrorMessage(i18n: Pick<HisHopeI18nService, "t">, error: unknown): string {
  const candidate = error as { status?: number; error?: { errorCode?: string } } | null;
  const status = candidate?.status;
  const code = candidate?.error?.errorCode;
  if (status === 401) return i18n.t("mobile.operatorSessionExpired", "Your session expired. Sign in again.");
  if (status === 403) return i18n.t("mobile.operatorForbidden", "You do not have permission for this operation.");
  if (status === 409) return i18n.t("mobile.operatorConflict", "The record changed. Refresh and review before retrying.");
  if (status === 422 || code?.startsWith("invalid_")) return i18n.t("mobile.operatorValidationFailed", "The server rejected the data. Review the required fields.");
  return i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions.");
}
