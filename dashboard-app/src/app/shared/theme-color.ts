/**
 * Resolve a foundation design-token CSS variable to a concrete color string
 * for APIs that cannot read CSS custom properties (e.g. Chart.js canvas).
 * Falls back to a literal value when the token is unavailable (SSR / pre-paint).
 */
export function themeColor(token: string, fallback: string): string {
  if (typeof document === 'undefined') return fallback;
  const value = getComputedStyle(document.documentElement).getPropertyValue(token).trim();
  return value || fallback;
}
