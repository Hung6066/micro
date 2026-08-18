import {
  friendlyNameLabel,
  friendlyReferenceLabel,
} from "@his-hope/frontend-foundation/contracts";
import type { FriendlyNameReference } from "@his-hope/frontend-foundation/contracts";

export function iamScopeLabel(
  id: string | null | undefined,
  scopes: readonly FriendlyNameReference[],
  includeKind = false,
): string {
  return friendlyReferenceLabel(id, scopes, includeKind);
}

export function iamPrincipalLabel(
  id: string | null | undefined,
  principalType: string | null | undefined,
  users: readonly FriendlyNameReference[] = [],
  groups: readonly FriendlyNameReference[] = [],
  workloadRoles: readonly FriendlyNameReference[] = [],
): string {
  if (!id) return "-";
  const source =
    principalType === "human"
      ? users
      : principalType === "group"
        ? groups
        : workloadRoles;
  return friendlyNameLabel(source.find((item) => item.id === id), id);
}
