/** Paths that must never receive automatic tenant scopeId query params. */
const TENANT_SCOPE_EXCLUDED_SEGMENTS = [
  "/me/",
  "/permissions",
  "/role-owners",
  "/settings",
  "/platform/",
  "/mobile/",
  "/localization",
  "/connect/",
  "/Account/",
  "/auth/session",
];

/** Admin IAM list/read endpoints that honor ?scopeId= tenant isolation. */
export function shouldAttachTenantScopeId(url: string): boolean {
  if (!url.includes("/api/")) {
    return false;
  }

  if (TENANT_SCOPE_EXCLUDED_SEGMENTS.some((segment) => url.includes(segment))) {
    return false;
  }

  return (
    url.includes("/admin/") ||
    url.includes("/iam/") ||
    url.includes("/audit-logs") ||
    url.includes("/auth/users") ||
    url.includes("/auth/roles") ||
    url.includes("/consents")
  );
}
