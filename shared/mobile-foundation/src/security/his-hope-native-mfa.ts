export interface HisHopeNativeMfaRequest {
  readonly ticket: string;
}

export type HisHopeNativeMfaStatus =
  | "approved"
  | "rejected"
  | "expired"
  | "cancelled"
  | "unsupported";

export interface HisHopeNativeMfaResult {
  readonly approved: boolean;
  readonly status: HisHopeNativeMfaStatus;
  readonly reason?: string;
}

export interface HisHopeNativeMfaOptionsResponse {
  readonly options: Readonly<Record<string, unknown>>;
}

export interface HisHopeNativeMfaCompletionResponse {
  readonly approved: boolean;
  readonly status?: HisHopeNativeMfaStatus | string;
  readonly reason?: string;
}

export interface HisHopeNativeMfaServer {
  requestOptions(ticket: string): Promise<HisHopeNativeMfaOptionsResponse>;
  complete(
    ticket: string,
    assertion: Readonly<Record<string, unknown>>,
  ): Promise<HisHopeNativeMfaCompletionResponse>;
}

export interface HisHopeNativeMfaPasskeyCapability {
  isSupported(): Promise<boolean>;
  authenticate(
    options: Readonly<Record<string, unknown>>,
  ): Promise<Readonly<Record<string, unknown>>>;
}

export interface HisHopeNativeMfaBridgeDependencies {
  readonly passkey: HisHopeNativeMfaPasskeyCapability;
  readonly server: HisHopeNativeMfaServer;
}

export interface HisHopeNativeMfaBridge {
  approveMfa(request: HisHopeNativeMfaRequest): Promise<HisHopeNativeMfaResult>;
}

export class HisHopeNativeMfaBridgeError extends Error {
  constructor(
    readonly status: Exclude<HisHopeNativeMfaStatus, "approved">,
    message?: string,
  ) {
    super(message ?? status);
    this.name = "HisHopeNativeMfaBridgeError";
  }
}

export function createHisHopeNativeMfaBridge(
  dependencies: HisHopeNativeMfaBridgeDependencies,
): HisHopeNativeMfaBridge {
  return new DefaultHisHopeNativeMfaBridge(dependencies);
}

class DefaultHisHopeNativeMfaBridge implements HisHopeNativeMfaBridge {
  constructor(private readonly dependencies: HisHopeNativeMfaBridgeDependencies) {}

  async approveMfa(request: HisHopeNativeMfaRequest): Promise<HisHopeNativeMfaResult> {
    const ticket = request.ticket.trim();
    if (!ticket) return rejected("Native MFA approval ticket is required.");

    if (!await this.dependencies.passkey.isSupported()) {
      return unsupported("Native passkey approval is not supported on this device.");
    }

    let options: Readonly<Record<string, unknown>>;
    try {
      options = (await this.dependencies.server.requestOptions(ticket)).options;
    } catch (error) {
      return classifyFailure(error);
    }

    let assertion: Readonly<Record<string, unknown>>;
    try {
      assertion = await this.dependencies.passkey.authenticate(options);
    } catch (error) {
      return classifyFailure(error);
    }

    try {
      const completion = await this.dependencies.server.complete(ticket, assertion);
      if (completion.approved) return { approved: true, status: "approved" };
      const status = normalizeStatus(completion.status) ?? "rejected";
      return {
        approved: false,
        status,
        reason: completion.reason ?? defaultReason(status),
      };
    } catch (error) {
      return classifyFailure(error);
    }
  }
}

function classifyFailure(error: unknown): HisHopeNativeMfaResult {
  if (error instanceof HisHopeNativeMfaBridgeError) {
    return { approved: false, status: error.status, reason: error.message };
  }

  const status = normalizeStatus(errorCode(error))
    ?? statusFromHttpStatus(error)
    ?? statusFromMessage(errorMessage(error))
    ?? "rejected";
  return { approved: false, status, reason: errorMessage(error) ?? defaultReason(status) };
}

function normalizeStatus(value: unknown): Exclude<HisHopeNativeMfaStatus, "approved"> | null {
  if (typeof value !== "string") return null;
  const normalized = value.toLowerCase().replace(/[_\s-]/g, "");
  if (normalized.includes("cancel")) return "cancelled";
  if (normalized.includes("unsupported") || normalized.includes("unavailable")) return "unsupported";
  if (normalized.includes("expired") || normalized.includes("timeout")) return "expired";
  if (normalized.includes("reject") || normalized.includes("denied") || normalized.includes("forbidden")) return "rejected";
  return null;
}

function statusFromHttpStatus(error: unknown): Exclude<HisHopeNativeMfaStatus, "approved"> | null {
  const status = typeof error === "object" && error !== null && "status" in error
    ? Number((error as { status: unknown }).status)
    : NaN;
  if (status === 410 || status === 408) return "expired";
  if (status === 401 || status === 403 || status === 404 || status === 409 || status === 422) return "rejected";
  return null;
}

function statusFromMessage(message: string | null): Exclude<HisHopeNativeMfaStatus, "approved"> | null {
  return normalizeStatus(message);
}

function errorCode(error: unknown): unknown {
  return typeof error === "object" && error !== null && "code" in error
    ? (error as { code: unknown }).code
    : null;
}

function errorMessage(error: unknown): string | null {
  if (error instanceof Error && error.message) return error.message;
  if (typeof error === "object" && error !== null) {
    if ("message" in error && typeof (error as { message: unknown }).message === "string") {
      return (error as { message: string }).message;
    }
    if ("error" in error) {
      const nested = (error as { error: unknown }).error;
      if (typeof nested === "string") return nested;
      if (typeof nested === "object" && nested !== null && "message" in nested) {
        const nestedMessage = (nested as { message: unknown }).message;
        if (typeof nestedMessage === "string") return nestedMessage;
      }
    }
  }
  return null;
}

function defaultReason(status: Exclude<HisHopeNativeMfaStatus, "approved">): string {
  switch (status) {
    case "cancelled":
      return "Native MFA approval was cancelled.";
    case "unsupported":
      return "Native passkey approval is not supported on this device.";
    case "expired":
      return "MFA approval ticket expired.";
    case "rejected":
      return "Native MFA assertion was rejected.";
  }
}

function rejected(reason: string): HisHopeNativeMfaResult {
  return { approved: false, status: "rejected", reason };
}

function unsupported(reason: string): HisHopeNativeMfaResult {
  return { approved: false, status: "unsupported", reason };
}
