import {
  friendlyNameLabel,
  friendlyReferenceLabel,
} from "@his-hope/frontend-foundation/contracts";
import type { FriendlyNameReference } from "@his-hope/frontend-foundation/contracts";
import type { IamScope } from "../contracts/iam.contracts";

export interface IamScopeOption {
  value: string;
  label: string;
}

export function iamScopeOptions(
  scopes: readonly IamScope[],
  selectedScopeId: string | undefined,
  selectLabel: string,
): IamScopeOption[] {
  const options = scopes.map((scope) => ({
    value: scope.id,
    label:
      [scope.displayName, scope.key].filter(Boolean).join(" · ") ||
      "Unknown scope",
  }));

  if (
    selectedScopeId &&
    !options.some((option) => option.value === selectedScopeId)
  ) {
    options.push({ value: selectedScopeId, label: "Unknown scope" });
  }

  return [{ value: "", label: selectLabel }, ...options];
}

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
  return friendlyNameLabel(
    source.find((item) => item.id === id),
    id,
  );
}
