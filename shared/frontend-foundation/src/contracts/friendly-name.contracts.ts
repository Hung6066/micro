export interface FriendlyNameReference {
  id: string;
  key?: string;
  displayName?: string;
  kind?: string;
  email?: string;
  userName?: string;
}

export interface FriendlyNameCatalog {
  [name: string]: readonly FriendlyNameReference[] | undefined;
}

export interface FriendlyNameField {
  field: string;
  catalog: string;
  includeKind?: boolean;
}

export function friendlyNameLabel(
  reference: FriendlyNameReference | undefined,
  fallback: string,
  includeKind = false,
): string {
  if (!reference) return fallback;
  const parts = [
    reference.displayName || reference.email || reference.userName,
    reference.key,
  ];
  if (includeKind && reference.kind) parts.push(reference.kind);
  return parts.filter(Boolean).join(" · ") || fallback;
}

export function friendlyReferenceLabel(
  id: string | null | undefined,
  references: readonly FriendlyNameReference[] | undefined,
  includeKind = false,
): string {
  if (!id) return "-";
  return friendlyNameLabel(
    references?.find((reference) => reference.id === id),
    id,
    includeKind,
  );
}

export function decorateReferenceRow<T extends Record<string, unknown>>(
  row: T,
  catalog: FriendlyNameCatalog,
  fields: readonly FriendlyNameField[],
): T {
  const result: Record<string, unknown> = { ...row };
  for (const field of fields) {
    const value = result[field.field];
    if (typeof value !== "string") continue;
    result[field.field] = friendlyReferenceLabel(
      value,
      catalog[field.catalog],
      field.includeKind,
    );
  }
  return result as T;
}
