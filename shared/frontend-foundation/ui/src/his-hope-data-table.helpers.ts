/** Pure, stateless helpers extracted from HisHopeDataTableComponent for unit testing and reuse. */

export interface HisHopeColumnWidthBounds {
  readonly minWidth?: number;
  readonly maxWidth?: number;
}

export function clampColumnWidth(
  width: number,
  column: HisHopeColumnWidthBounds,
): number {
  return Math.max(
    column.minWidth ?? 96,
    Math.min(column.maxWidth ?? 640, Math.round(width)),
  );
}

const FILTER_OPERATOR_LABELS: Record<string, string> = {
  eq: "=",
  neq: "≠",
  contains: "∋",
  startsWith: "A*",
  gt: ">",
  gte: "≥",
  lt: "<",
  lte: "≤",
  in: "∈",
};

export function operatorLabel(operator: string): string {
  return FILTER_OPERATOR_LABELS[operator] ?? operator;
}

export function orderIndexOf(order: readonly string[], key: string): number {
  const index = order.indexOf(key);
  return index < 0 ? order.length : index;
}

export interface HisHopeSortTermLike {
  readonly key: string;
  readonly direction: "asc" | "desc";
}

export function compareRowsBySortTerms(
  a: Record<string, unknown>,
  b: Record<string, unknown>,
  terms: readonly HisHopeSortTermLike[],
): number {
  for (const term of terms) {
    const result = String(a[term.key] ?? "").localeCompare(
      String(b[term.key] ?? ""),
      undefined,
      {
        numeric: true,
        sensitivity: "base",
      },
    );
    if (result) return result * (term.direction === "asc" ? 1 : -1);
  }
  return 0;
}

export function isResponsiveColumnHidden(
  priority: 1 | 2 | 3 | undefined,
  viewportWidth: number,
): boolean {
  if (!priority) return false;
  return viewportWidth <= 600
    ? priority >= 2
    : viewportWidth <= 1024 && priority >= 3;
}
