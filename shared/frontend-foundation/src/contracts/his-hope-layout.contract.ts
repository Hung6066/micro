/**
 * Semantic spacing, radius, and surface color tokens for His.Hope UI.
 * Prefer t-shirt spacing (2xs → 3xl) and role-based radius (button, card, sheet).
 */
export const HIS_HOPE_SPACE_TOKENS = {
  none: "--space-none",
  space2xs: "--space-2xs",
  xs: "--space-xs",
  sm: "--space-sm",
  md: "--space-md",
  lg: "--space-lg",
  xl: "--space-xl",
  space2xl: "--space-2xl",
  space3xl: "--space-3xl",
  space4xl: "--space-4xl",
  inset: "--space-inset",
  pagePaddingBlock: "--page-padding-block",
} as const;

export const HIS_HOPE_RADIUS_TOKENS = {
  micro: "--radius-micro",
  input: "--radius-input",
  badge: "--radius-badge",
  button: "--radius-button",
  card: "--radius-card",
  control: "--radius-control",
  sheet: "--radius-sheet",
  pill: "--radius-pill",
  full: "--radius-full",
  brandMark: "--radius-brand-mark",
  chip: "--radius-chip",
  feature: "--radius-feature",
  panel: "--radius-panel",
  mobileSheet: "--radius-mobile-sheet",
} as const;

export const HIS_HOPE_SURFACE_COLOR_TOKENS = {
  onPrimary: "--color-on-primary",
  onDanger: "--color-on-danger",
  overlayBackdrop: "--overlay-backdrop",
  overlayBackdropSoft: "--overlay-backdrop-soft",
} as const;

/** @deprecated Use HIS_HOPE_SPACE_TOKENS semantic names instead of numeric CSS aliases. */
export const HIS_HOPE_LEGACY_SPACE_ALIASES = {
  space0: "--space-0",
  space1: "--space-1",
  space1_5: "--space-1-5",
  space2: "--space-2",
  space3: "--space-3",
  space4: "--space-4",
  space5: "--space-5",
  space6: "--space-6",
  space8: "--space-8",
} as const;
