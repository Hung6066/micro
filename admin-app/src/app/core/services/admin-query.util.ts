import { AdminPageQuery } from "../contracts/admin.contracts";

/** Builds the shared HTTP query params for a server-backed admin list resource. */
export function buildAdminPageParams(
  query: AdminPageQuery,
): Record<string, string> {
  const params: Record<string, string> = {
    page: String(query.page),
    pageSize: String(query.pageSize),
  };
  if (query.search?.trim()) params["search"] = query.search.trim();
  if (query.cursor) params["cursor"] = query.cursor;
  if (query.sort) {
    const sort = Array.isArray(query.sort) ? query.sort : [query.sort];
    params["sort"] = sort
      .map((term) => `${term.key}:${term.direction}`)
      .join(",");
  }
  for (const [key, value] of Object.entries(query.filters ?? {})) {
    if (value !== null && value !== undefined && value !== "")
      params[key] = String(value);
  }
  return params;
}

/** Builds the request-cache key for a paged admin resource query. */
export function adminPageCacheKey(
  resource: string,
  params: Record<string, string>,
): string {
  return `admin:${resource}:${new URLSearchParams(params).toString()}`;
}

/** Triggers a browser download for a table export blob and releases the object URL. */
export function downloadAdminTableExport(
  blob: Blob,
  resource: string,
  format: string,
): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `${resource}-${new Date().toISOString().slice(0, 10)}.${format}`;
  anchor.click();
  URL.revokeObjectURL(url);
}
