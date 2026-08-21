/** CSS custom property names for the shared His.Hope typography scale. */
export const HIS_HOPE_TYPOGRAPHY_TOKENS = {
  overline: "--font-size-overline",
  nav: "--font-size-nav",
  micro: "--font-size-micro",
  caption: "--font-size-caption",
  label: "--font-size-label",
  body: "--font-size-body",
  bodyEmphasis: "--font-size-body-emphasis",
  toolbar: "--font-size-toolbar",
  input: "--font-size-input",
  subhead: "--font-size-subhead",
  iconSm: "--font-size-icon-sm",
  section: "--font-size-section",
  iconMd: "--font-size-icon-md",
  headline: "--font-size-headline",
  title: "--font-size-title",
  iconLg: "--font-size-icon-lg",
  display: "--font-size-display",
  displayMd: "--font-size-display-md",
  displayLg: "--font-size-display-lg",
  displayXl: "--font-size-display-xl",
} as const;

export type HisHopeTypographyTokenName =
  keyof typeof HIS_HOPE_TYPOGRAPHY_TOKENS;

export interface HisHopeTypographyScale {
  readonly overline: string;
  readonly nav: string;
  readonly micro: string;
  readonly caption: string;
  readonly label: string;
  readonly body: string;
  readonly bodyEmphasis: string;
  readonly toolbar: string;
  readonly input: string;
  readonly subhead: string;
  readonly iconSm: string;
  readonly section: string;
  readonly iconMd: string;
  readonly headline: string;
  readonly title: string;
  readonly iconLg: string;
  readonly display: string;
  readonly displayMd: string;
  readonly displayLg: string;
  readonly displayXl: string;
}

/** Maps the typography scale contract onto CSS custom properties. */
export function hisHopeTypographyCssVariables(
  scale: HisHopeTypographyScale,
): Record<string, string> {
  return {
    "--font-size-overline": scale.overline,
    "--font-size-nav": scale.nav,
    "--font-size-micro": scale.micro,
    "--font-size-caption": scale.caption,
    "--font-size-label": scale.label,
    "--font-size-body": scale.body,
    "--font-size-body-emphasis": scale.bodyEmphasis,
    "--font-size-toolbar": scale.toolbar,
    "--font-size-input": scale.input,
    "--font-size-subhead": scale.subhead,
    "--font-size-icon-sm": scale.iconSm,
    "--font-size-section": scale.section,
    "--font-size-icon-md": scale.iconMd,
    "--font-size-headline": scale.headline,
    "--font-size-title": scale.title,
    "--font-size-icon-lg": scale.iconLg,
    "--font-size-display": scale.display,
    "--font-size-display-md": scale.displayMd,
    "--font-size-display-lg": scale.displayLg,
    "--font-size-display-xl": scale.displayXl,
  };
}

export const DEFAULT_HIS_HOPE_TYPOGRAPHY_SCALE: HisHopeTypographyScale = {
  overline: "10px",
  nav: "11px",
  micro: "9px",
  caption: "12px",
  label: "13px",
  body: "14px",
  bodyEmphasis: "15px",
  toolbar: "16px",
  input: "16px",
  subhead: "17px",
  iconSm: "18px",
  section: "20px",
  iconMd: "20px",
  headline: "22px",
  title: "24px",
  iconLg: "24px",
  display: "28px",
  displayMd: "32px",
  displayLg: "40px",
  displayXl: "48px",
};
