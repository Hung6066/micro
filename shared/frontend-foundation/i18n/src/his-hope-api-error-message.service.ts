import { HttpErrorResponse } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { HisHopeI18nService } from "./his-hope-i18n.service";

export interface HisHopeApiErrorPayload {
  code?: string;
  errorCode?: string;
  error?: string;
  title?: string;
  detail?: string;
  message?: string;
}

export interface HisHopeNormalizedApiError {
  status: number;
  code: string;
  correlationId?: string;
}

/** Resolves the shared ProblemDetails contract into a safe localized message.
 * Legacy `{ error }` payloads remain supported at the frontend boundary while
 * services migrate, so every Angular app follows the same fallback rules. */
@Injectable({ providedIn: "root" })
export class HisHopeApiErrorMessageService {
  private readonly i18n = inject(HisHopeI18nService);

  normalize(error: unknown): HisHopeNormalizedApiError {
    const response = error instanceof HttpErrorResponse ? error : undefined;
    const payload = this.payload(error);
    return {
      status: response?.status ?? 0,
      code:
        payload?.errorCode ??
        payload?.code ??
        payload?.error ??
        this.statusCode(response?.status),
      correlationId:
        response?.headers?.get("x-correlation-id") ??
        response?.headers?.get("trace-id") ??
        undefined,
    };
  }

  message(error: unknown, fallbackKey = "errors.unexpectedError"): string {
    const normalized = this.normalize(error);
    const specific = this.i18n.t(
      `errors.api.${this.toTranslationKey(normalized.code)}`,
      "",
    );
    if (specific) return specific;
    if (fallbackKey === "errors.unexpectedError")
      return this.genericFallback(normalized.status);
    return fallbackKey.includes(".")
      ? this.i18n.t(fallbackKey, this.genericFallback(normalized.status))
      : fallbackKey;
  }

  private payload(error: unknown): HisHopeApiErrorPayload | undefined {
    if (error instanceof HttpErrorResponse && typeof error.error === "string")
      return { error: error.error };
    if (
      error instanceof HttpErrorResponse &&
      error.error &&
      typeof error.error === "object"
    )
      return error.error as HisHopeApiErrorPayload;
    return undefined;
  }

  private statusCode(status?: number): string {
    return !status ? "network_error" : `http_${status}`;
  }

  private toTranslationKey(code: string): string {
    return code
      .toLowerCase()
      .split(/[_-]/)
      .map((part, index) =>
        index === 0 ? part : `${part.charAt(0).toUpperCase()}${part.slice(1)}`,
      )
      .join("");
  }

  private genericFallback(status: number): string {
    if (status === 0) return this.i18n.t("errors.networkError", "Network error.");
    if (status === 401) return this.i18n.t("errors.unauthorized", "Authentication is required.");
    if (status === 403) return this.i18n.t("errors.accessDenied", "Access denied.");
    if (status === 404) return this.i18n.t("errors.notFound", "The requested resource was not found.");
    if (status === 409) return this.i18n.t("errors.conflict", "The request conflicts with the current resource state.");
    if (status === 429) return this.i18n.t("errors.rateLimited", "Too many requests.");
    if (status >= 400 && status < 500) return this.i18n.t("errors.validationFailed", "Validation failed.");
    if (status >= 500) return this.i18n.t("errors.serverError", "A server error occurred.");
    return this.i18n.t("errors.unexpectedError", "An unexpected error occurred.");
  }
}
