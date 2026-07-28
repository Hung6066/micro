export interface HisHopeDeepLinkAllowListEntry {
  scheme: string;
  host?: string;
  pathPrefix?: string;
}

/** Returns true only if `url` matches one of the allow-listed
 *  scheme/host/path-prefix combinations. Deep links drive in-app navigation
 *  (including OIDC callbacks) and can arrive from any app on the device, so
 *  anything not explicitly allow-listed must be rejected rather than parsed. */
export function isHisHopeDeepLinkAllowed(
  url: string,
  allowList: readonly HisHopeDeepLinkAllowListEntry[],
): boolean {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return false;
  }
  const scheme = parsed.protocol.replace(":", "");
  return allowList.some((entry) => {
    if (entry.scheme !== scheme) return false;
    if (entry.host && entry.host !== parsed.hostname) return false;
    if (entry.pathPrefix && !parsed.pathname.startsWith(entry.pathPrefix))
      return false;
    return true;
  });
}
