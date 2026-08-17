import type {
  HisHopeMobileRuntimeContract,
  HisHopeMobileRuntimeOptions,
  HisHopeResolvedMobileRuntimeConfig,
} from "./runtime-config.contract";

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
    throw new Error(`Mobile runtime config ${field} must be an absolute URL.`);
  }

  if (!parsed.hostname) {
    throw new Error(`Mobile runtime config ${field} must include a host.`);
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

function nativeCallbackUrl(appScheme: string, path: string): string {
  if (!path.startsWith("/auth/")) {
    return `${appScheme}://${path.replace(/^\//, "")}`;
  }

  return `${appScheme}://auth/${path.slice("/auth/".length)}`;
}

export class RuntimeConfigService {
  private resolved?: Readonly<HisHopeMobileRuntimeContract>;

  constructor(
    private readonly source: Partial<HisHopeMobileRuntimeContract> | undefined = globalThis.window?.__HISHOPE_CONFIG__,
  ) {}

  require(
    options: HisHopeMobileRuntimeOptions,
  ): Readonly<HisHopeResolvedMobileRuntimeConfig> {
    const base = this.resolveBase(options.production);
    const appScheme = options.appScheme ?? "hishope";
    const webOrigin = options.webOrigin ?? "http://localhost:4300";

    return freeze({
      ...base,
      redirectUrl: options.platform.isNative
        ? nativeCallbackUrl(appScheme, options.redirectPath)
        : joinUrl(webOrigin, options.redirectPath),
      postLogoutRedirectUri: options.platform.isNative
        ? nativeCallbackUrl(appScheme, options.postLogoutRedirectPath)
        : joinUrl(webOrigin, options.postLogoutRedirectPath),
      clientId: options.clientId,
      scope: options.scope,
      responseType: "code",
      secureRoutes: [...options.secureRoutes],
      adminApiUrl: `${base.apiOrigin}/api/v1/admin`,
      appVersion: options.appVersion ?? "0.1.0",
      sentryEnvironment:
        base.sentryEnvironment?.trim() ||
        options.defaultSentryEnvironment ||
        (options.production ? "production" : "development"),
      pushNotificationsEnabled:
        base.pushNotificationsEnabled ??
        options.defaultPushNotificationsEnabled ??
        true,
      certificatePins: [...(options.certificatePins ?? [])],
    } satisfies HisHopeResolvedMobileRuntimeConfig);
  }

  private resolveBase(
    production: boolean,
  ): Readonly<HisHopeMobileRuntimeContract> {
    if (this.resolved && this.resolved.production === production) {
      return this.resolved;
    }

    const config = this.source;
    if (!config?.oidcAuthority?.trim()) {
      throw new Error("Mobile runtime config oidcAuthority is required.");
    }

    if (!config.apiOrigin?.trim()) {
      throw new Error("Mobile runtime config apiOrigin is required.");
    }

    const resolved = freeze({
      apiOrigin: normalizeOrigin(config.apiOrigin.trim(), "apiOrigin"),
      oidcAuthority: normalizeOrigin(
        config.oidcAuthority.trim(),
        "oidcAuthority",
      ),
      production,
      defaultLocale: config.defaultLocale?.trim() || undefined,
      sentryDsn: config.sentryDsn?.trim() || undefined,
      sentryEnvironment: config.sentryEnvironment?.trim() || undefined,
      pushNotificationsEnabled: config.pushNotificationsEnabled,
    } satisfies HisHopeMobileRuntimeContract);

    if (
      production &&
      (new URL(resolved.apiOrigin).protocol !== "https:" ||
        new URL(resolved.oidcAuthority).protocol !== "https:")
    ) {
      throw new Error(
        "Production mobile runtime config requires HTTPS apiOrigin and oidcAuthority.",
      );
    }

    this.resolved = resolved;
    return resolved;
  }
}
