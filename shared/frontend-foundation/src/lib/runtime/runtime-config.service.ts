import {
  HisHopeRuntimeConfigContract,
  HisHopeWebRuntimeConfig,
  HisHopeWebRuntimeOptions,
} from "./runtime-config.contract";

type LocationLike = Pick<Location, "origin">;

function freeze<T extends object>(value: T): Readonly<T> {
  return Object.freeze({ ...value });
}

function normalizeOrigin(
  value: string,
  field: "apiOrigin" | "oidcAuthority",
): string {
  let parsed: URL;
  try {
    parsed = new URL(value);
  } catch {
    throw new Error(`Runtime config ${field} must be an absolute URL.`);
  }

  if (!parsed.hostname) {
    throw new Error(`Runtime config ${field} must include a host.`);
  }

  parsed.pathname = "";
  parsed.search = "";
  parsed.hash = "";
  return parsed.toString().replace(/\/$/, "");
}

function joinUrl(origin: string, path: string): string {
  return new URL(path.replace(/^\//, ""), `${origin.replace(/\/$/, "")}/`)
    .toString()
    .replace(/\/$/, "");
}

export class RuntimeConfigService {
  private resolved?: Readonly<HisHopeRuntimeConfigContract>;

  constructor(
    private readonly source: Partial<HisHopeRuntimeConfigContract> | undefined = globalThis.window?.__HISHOPE_CONFIG__,
    private readonly location: LocationLike = globalThis.location,
  ) {}

  require(): Readonly<HisHopeRuntimeConfigContract> {
    if (this.resolved) {
      return this.resolved;
    }

    const config = this.source;
    if (!config?.oidcAuthority?.trim()) {
      throw new Error("Runtime config oidcAuthority is required.");
    }

    if (!config.apiOrigin?.trim()) {
      throw new Error("Runtime config apiOrigin is required.");
    }

    const resolved = freeze({
      apiOrigin: normalizeOrigin(config.apiOrigin.trim(), "apiOrigin"),
      oidcAuthority: normalizeOrigin(
        config.oidcAuthority.trim(),
        "oidcAuthority",
      ),
      production: config.production === true,
      defaultLocale: config.defaultLocale?.trim() || undefined,
    } satisfies HisHopeRuntimeConfigContract);

    if (
      resolved.production &&
      (new URL(resolved.apiOrigin).protocol !== "https:" ||
        new URL(resolved.oidcAuthority).protocol !== "https:")
    ) {
      throw new Error(
        "Production runtime config requires HTTPS apiOrigin and oidcAuthority.",
      );
    }

    this.resolved = resolved;
    return resolved;
  }

  requireWeb(options: HisHopeWebRuntimeOptions): Readonly<HisHopeWebRuntimeConfig> {
    const config = this.require();
    const appOrigin = this.location?.origin || "http://localhost";

    return freeze({
      ...config,
      localizationApiUrl: joinUrl(
        config.apiOrigin,
        options.localizationApiPath ?? "/api/v1",
      ),
      redirectUrl: joinUrl(appOrigin, options.redirectPath),
      postLogoutRedirectUri: joinUrl(appOrigin, options.postLogoutRedirectPath),
      silentRenewUrl: options.silentRenewPath
        ? joinUrl(appOrigin, options.silentRenewPath)
        : undefined,
      clientId: options.clientId,
      scope: options.scope,
      secureRoutes: [...options.secureRoutes],
      responseType: options.responseType ?? "code",
      maxIdTokenIatOffsetInSeconds:
        options.maxIdTokenIatOffsetInSeconds ?? 600,
      usePkce: options.usePkce,
    } satisfies HisHopeWebRuntimeConfig);
  }
}
