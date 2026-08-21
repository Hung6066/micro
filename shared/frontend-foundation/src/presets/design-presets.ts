import {
  DEFAULT_HIS_HOPE_TYPOGRAPHY_SCALE,
  HisHopeTypographyScale,
} from "../contracts/his-hope-typography.contract";

export type HisHopeDesignPresetId =
  | "linear"
  | "intercom"
  | "supabase"
  | "hashicorp"
  | "stripe"
  | "notion"
  | "airtable"
  | "apple"
  | "cal"
  | "claude"
  | "coinbase"
  | "cursor"
  | "figma"
  | "ibm"
  | "mintlify"
  | "raycast"
  | "sentry"
  | "spotify"
  | "vercel"
  | "wise";

export interface HisHopePresetTokens {
  readonly canvas: string;
  readonly surface: string;
  readonly surfaceMuted: string;
  readonly ink: string;
  readonly inkMuted: string;
  readonly border: string;
  readonly primary: string;
  readonly primaryHover: string;
  readonly primarySoft: string;
  readonly focus: string;
  readonly header: string;
}

export interface HisHopeDesignPreset {
  readonly id: HisHopeDesignPresetId;
  readonly name: string;
  readonly source: string;
  readonly description: string;
  readonly tokens: HisHopePresetTokens;
  readonly typography: Readonly<{
    body: string;
    display: string;
    mono: string;
    scale: HisHopeTypographyScale;
    weight: string;
    lineHeight: string;
    lineHeightTight: string;
  }>;
  readonly layout: Readonly<{
    density: "compact" | "comfortable" | "spacious";
    sidebarWidth: string;
    contentMaxWidth: string;
    controlHeight: string;
    cardRadius: string;
    buttonRadius: string;
    inputRadius: string;
  }>;
  readonly components: Readonly<{
    button: "solid" | "soft" | "outline" | "pill";
    table: "dense" | "quiet" | "colorful" | "editorial";
    navigation: "rail" | "sidebar" | "workspace" | "command";
  }>;
}

type PresetTypographyOverrides = {
  body?: string;
  display?: string;
  mono?: string;
  scale?: Partial<HisHopeTypographyScale>;
  weight?: string;
  lineHeight?: string;
  lineHeightTight?: string;
  /** Legacy preset field mapped to `scale.body`. */
  bodySize?: string;
  /** Legacy preset field mapped to `scale.title`. */
  titleSize?: string;
};

type PresetOverrides = {
  tokens?: Partial<HisHopePresetTokens>;
  typography?: PresetTypographyOverrides;
  layout?: Partial<HisHopeDesignPreset["layout"]>;
  components?: Partial<HisHopeDesignPreset["components"]>;
};

const source = (slug: string): string =>
  `https://getdesign.md/${slug === "linear" ? "linear.app" : slug}/design-md`;

const base = (
  id: HisHopeDesignPresetId,
  name: string,
  description: string,
  overrides: PresetOverrides,
): HisHopeDesignPreset => ({
  id,
  name,
  source: source(id),
  description,
  tokens: {
    canvas: "#F6F8F6",
    surface: "#FFFFFF",
    surfaceMuted: "#F0F4F1",
    ink: "#17211C",
    inkMuted: "#5F6D65",
    border: "#DDE6DF",
    primary: "#1F6B4A",
    primaryHover: "#18563B",
    primarySoft: "#E4F1E9",
    focus: "#4C9B70",
    header: "#123B29",
    ...overrides.tokens,
  },
  typography: (() => {
    const typography = overrides.typography ?? {};
    const {
      bodySize,
      titleSize,
      scale: scaleOverrides,
      body = "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
      display = "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
      mono = 'HisHope JetBrains Mono, "Cascadia Mono", Consolas, monospace',
      weight = "400",
      lineHeight = "1.5",
      lineHeightTight = "1.25",
    } = typography;
    return {
      body,
      display,
      mono,
      scale: {
        ...DEFAULT_HIS_HOPE_TYPOGRAPHY_SCALE,
        ...(bodySize ? { body: bodySize } : {}),
        ...(titleSize ? { title: titleSize } : {}),
        ...scaleOverrides,
      },
      weight,
      lineHeight,
      lineHeightTight,
    };
  })(),
  layout: {
    density: "comfortable",
    sidebarWidth: "264px",
    contentMaxWidth: "1200px",
    controlHeight: "40px",
    cardRadius: "8px",
    buttonRadius: "6px",
    inputRadius: "4px",
    ...overrides.layout,
  },
  components: {
    button: "solid",
    table: "quiet",
    navigation: "sidebar",
    ...overrides.components,
  },
});

export const HIS_HOPE_DESIGN_PRESETS: Readonly<
  Record<HisHopeDesignPresetId, HisHopeDesignPreset>
> = {
  linear: base(
    "linear",
    "Linear",
    "Precise, compact workspace with quiet dark chrome and a violet focus color.",
    {
      tokens: {
        canvas: "#F7F7F8",
        surfaceMuted: "#F0F0F2",
        ink: "#1F1F23",
        inkMuted: "#6E6E78",
        border: "#E4E4E8",
        primary: "#5E6AD2",
        primaryHover: "#4B49C7",
        primarySoft: "#ECEBFF",
        focus: "#5E6AD2",
        header: "#1F1F23",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        bodySize: "14px",
        titleSize: "24px",
        weight: "500",
      },
      layout: {
        density: "compact",
        sidebarWidth: "220px",
        contentMaxWidth: "1440px",
        controlHeight: "36px",
        cardRadius: "8px",
        buttonRadius: "6px",
        inputRadius: "6px",
      },
      components: { button: "solid", table: "dense", navigation: "workspace" },
    },
  ),
  intercom: base(
    "intercom",
    "Intercom",
    "Friendly customer-service workspace with soft surfaces and conversational actions.",
    {
      tokens: {
        canvas: "#F5F9FC",
        surfaceMuted: "#EDF6FF",
        ink: "#17324D",
        inkMuted: "#5E7488",
        border: "#DCEAF5",
        primary: "#1677C8",
        primaryHover: "#0F67AE",
        primarySoft: "#E2F1FF",
        focus: "#1677C8",
        header: "#0D4777",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        weight: "500",
      },
      layout: {
        density: "spacious",
        sidebarWidth: "300px",
        contentMaxWidth: "1280px",
        controlHeight: "44px",
        cardRadius: "12px",
        buttonRadius: "8px",
        inputRadius: "8px",
      },
      components: { button: "soft", table: "quiet", navigation: "sidebar" },
    },
  ),
  supabase: base(
    "supabase",
    "Supabase",
    "Developer-first console with dark emerald surfaces and crisp data controls.",
    {
      // Keep the emerald identity while meeting WCAG AA against the light
      // surfaces used by the default workspace preset.
      tokens: {
        canvas: "#F7FAF8",
        surface: "#FFFFFF",
        surfaceMuted: "#EEF5F0",
        ink: "#1C2B24",
        inkMuted: "#52665B",
        border: "#D5E4DA",
        primary: "#16825A",
        primaryHover: "#106A49",
        primarySoft: "#DDF7E9",
        focus: "#16825A",
        header: "#173B2C",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        mono: 'HisHope Plex Mono, "Cascadia Mono", Consolas, monospace',
      },
      layout: {
        density: "compact",
        sidebarWidth: "240px",
        contentMaxWidth: "1440px",
        controlHeight: "36px",
        cardRadius: "6px",
        buttonRadius: "6px",
        inputRadius: "6px",
      },
      components: { button: "solid", table: "dense", navigation: "workspace" },
    },
  ),
  hashicorp: base(
    "hashicorp",
    "HashiCorp",
    "Enterprise-clean infrastructure console with restrained black, white and red accents.",
    {
      tokens: {
        canvas: "#F7F7F7",
        surfaceMuted: "#EEEEEE",
        ink: "#161616",
        inkMuted: "#5B5B5B",
        border: "#D8D8D8",
        primary: "#A8212D",
        primaryHover: "#861B25",
        primarySoft: "#F9E8EA",
        focus: "#A8212D",
        header: "#161616",
      },
      typography: {
        body: 'HisHope Plex Sans, "IBM Plex Sans", ui-sans-serif, system-ui, sans-serif',
        display:
          'HisHope Plex Sans, "IBM Plex Sans", ui-sans-serif, system-ui, sans-serif',
        weight: "500",
      },
      layout: {
        density: "comfortable",
        sidebarWidth: "248px",
        contentMaxWidth: "1280px",
        controlHeight: "40px",
        cardRadius: "4px",
        buttonRadius: "4px",
        inputRadius: "4px",
      },
      components: {
        button: "outline",
        table: "editorial",
        navigation: "sidebar",
      },
    },
  ),
  stripe: base(
    "stripe",
    "Stripe",
    "Financial operations surface with elegant typography, high clarity and focused purple actions.",
    {
      tokens: {
        canvas: "#F6F9FC",
        surfaceMuted: "#EEF2FF",
        ink: "#172B4D",
        inkMuted: "#667085",
        border: "#D9E2F2",
        primary: "#635BFF",
        primaryHover: "#5148D8",
        primarySoft: "#E9E8FF",
        focus: "#635BFF",
        header: "#172B4D",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        bodySize: "14px",
        titleSize: "28px",
        weight: "400",
        lineHeight: "1.55",
      },
      layout: {
        density: "comfortable",
        sidebarWidth: "264px",
        contentMaxWidth: "1280px",
        controlHeight: "40px",
        cardRadius: "10px",
        buttonRadius: "8px",
        inputRadius: "8px",
      },
      components: { button: "solid", table: "quiet", navigation: "workspace" },
    },
  ),
  notion: base(
    "notion",
    "Notion",
    "Warm editorial workspace with soft neutral surfaces and document-first hierarchy.",
    {
      tokens: {
        canvas: "#FBFAF9",
        surfaceMuted: "#F3F2F0",
        ink: "#37352F",
        inkMuted: "#787774",
        border: "#E8E6E3",
        primary: "#37352F",
        primaryHover: "#1F1F1D",
        primarySoft: "#EDECEB",
        focus: "#2383E2",
        header: "#37352F",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        bodySize: "15px",
        titleSize: "26px",
        weight: "400",
        lineHeight: "1.6",
      },
      layout: {
        density: "spacious",
        sidebarWidth: "260px",
        contentMaxWidth: "1100px",
        controlHeight: "40px",
        cardRadius: "6px",
        buttonRadius: "6px",
        inputRadius: "6px",
      },
      components: { button: "soft", table: "editorial", navigation: "sidebar" },
    },
  ),
  airtable: base(
    "airtable",
    "Airtable",
    "Structured data workspace with friendly color coding and scannable dense records.",
    {
      tokens: {
        canvas: "#F7F8FC",
        surfaceMuted: "#F0F2F8",
        ink: "#25253D",
        inkMuted: "#6B6B80",
        border: "#DADDEB",
        primary: "#6B4EFF",
        primaryHover: "#553BE0",
        primarySoft: "#EDEAFF",
        focus: "#6B4EFF",
        header: "#25253D",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
      },
      layout: {
        density: "compact",
        sidebarWidth: "232px",
        contentMaxWidth: "1600px",
        controlHeight: "36px",
        cardRadius: "6px",
        buttonRadius: "6px",
        inputRadius: "6px",
      },
      components: {
        button: "solid",
        table: "colorful",
        navigation: "workspace",
      },
    },
  ),
  apple: base(
    "apple",
    "Apple",
    "Premium white-space system with quiet chrome, precise typography and restrained blue actions.",
    {
      tokens: {
        canvas: "#FFFFFF",
        surfaceMuted: "#F5F5F7",
        ink: "#1D1D1F",
        inkMuted: "#6E6E73",
        border: "#D2D2D7",
        primary: "#0071E3",
        primaryHover: "#0077ED",
        primarySoft: "#E8F2FF",
        focus: "#0071E3",
        header: "#1D1D1F",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        bodySize: "15px",
        titleSize: "28px",
        weight: "400",
        lineHeight: "1.45",
      },
      layout: {
        density: "spacious",
        sidebarWidth: "240px",
        contentMaxWidth: "1200px",
        controlHeight: "44px",
        cardRadius: "12px",
        buttonRadius: "999px",
        inputRadius: "10px",
      },
      components: { button: "pill", table: "quiet", navigation: "sidebar" },
    },
  ),
  cal: base(
    "cal",
    "Cal.com",
    "Open-source scheduling console with neutral surfaces and developer-oriented clarity.",
    {
      tokens: {
        canvas: "#FFFFFF",
        surfaceMuted: "#F6F6F6",
        ink: "#111111",
        inkMuted: "#6B7280",
        border: "#E5E7EB",
        primary: "#111111",
        primaryHover: "#27272A",
        primarySoft: "#F0F0F0",
        focus: "#111111",
        header: "#111111",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display:
          "HisHope Cal Sans, Cal Sans, HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
      },
      layout: {
        density: "comfortable",
        sidebarWidth: "240px",
        contentMaxWidth: "1280px",
        controlHeight: "40px",
        cardRadius: "8px",
        buttonRadius: "6px",
        inputRadius: "6px",
      },
      components: { button: "solid", table: "quiet", navigation: "sidebar" },
    },
  ),
  claude: base(
    "claude",
    "Claude",
    "Calm editorial assistant surface with warm terracotta action color and readable type.",
    {
      tokens: {
        canvas: "#F7F4EF",
        surfaceMuted: "#EFE9E0",
        ink: "#28231F",
        inkMuted: "#756D65",
        border: "#DDD4C8",
        primary: "#C15F3C",
        primaryHover: "#A94B2D",
        primarySoft: "#F5E3DA",
        focus: "#C15F3C",
        header: "#28231F",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Source Serif, Georgia, ui-serif, serif",
        bodySize: "15px",
        titleSize: "28px",
        weight: "400",
        lineHeight: "1.6",
      },
      layout: {
        density: "spacious",
        sidebarWidth: "280px",
        contentMaxWidth: "1100px",
        controlHeight: "44px",
        cardRadius: "12px",
        buttonRadius: "8px",
        inputRadius: "8px",
      },
      components: { button: "soft", table: "editorial", navigation: "sidebar" },
    },
  ),
  coinbase: base(
    "coinbase",
    "Coinbase",
    "Trust-focused financial console with clean blue identity and high-signal data hierarchy.",
    {
      tokens: {
        canvas: "#FFFFFF",
        surfaceMuted: "#F5F8FF",
        ink: "#0A0B0D",
        inkMuted: "#5B616E",
        border: "#DCE3F0",
        primary: "#0052FF",
        primaryHover: "#0041CC",
        primarySoft: "#E6EEFF",
        focus: "#0052FF",
        header: "#0A0B0D",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
      },
      layout: {
        density: "compact",
        sidebarWidth: "232px",
        contentMaxWidth: "1440px",
        controlHeight: "40px",
        cardRadius: "8px",
        buttonRadius: "6px",
        inputRadius: "6px",
      },
      components: { button: "solid", table: "dense", navigation: "workspace" },
    },
  ),
  cursor: base(
    "cursor",
    "Cursor",
    "Dark developer workspace with focused surfaces and a single energetic violet accent.",
    {
      tokens: {
        canvas: "#111113",
        surface: "#19191C",
        surfaceMuted: "#222226",
        ink: "#F5F5F7",
        inkMuted: "#A4A4AD",
        border: "#34343B",
        primary: "#8B5CF6",
        primaryHover: "#A78BFA",
        primarySoft: "#30245A",
        focus: "#A78BFA",
        header: "#0B0B0D",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        mono: 'HisHope Plex Mono, "Cascadia Mono", Consolas, monospace',
        bodySize: "13px",
        titleSize: "22px",
        weight: "400",
        lineHeight: "1.5",
      },
      layout: {
        density: "compact",
        sidebarWidth: "224px",
        contentMaxWidth: "1600px",
        controlHeight: "34px",
        cardRadius: "6px",
        buttonRadius: "5px",
        inputRadius: "5px",
      },
      components: { button: "solid", table: "dense", navigation: "command" },
    },
  ),
  figma: base(
    "figma",
    "Figma",
    "Collaborative tool surface with playful but disciplined color-coded controls.",
    {
      tokens: {
        canvas: "#F7F7F7",
        surfaceMuted: "#F0F0F0",
        ink: "#1E1E1E",
        inkMuted: "#6B6B6B",
        border: "#E0E0E0",
        primary: "#1E1E1E",
        primaryHover: "#000000",
        primarySoft: "#E9E9E9",
        focus: "#18A0FB",
        header: "#1E1E1E",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
      },
      layout: {
        density: "compact",
        sidebarWidth: "248px",
        contentMaxWidth: "1440px",
        controlHeight: "36px",
        cardRadius: "6px",
        buttonRadius: "6px",
        inputRadius: "6px",
      },
      components: {
        button: "solid",
        table: "colorful",
        navigation: "workspace",
      },
    },
  ),
  ibm: base(
    "ibm",
    "IBM",
    "Structured enterprise system with blue actions, dense information and explicit hierarchy.",
    {
      tokens: {
        canvas: "#F4F4F4",
        surfaceMuted: "#E8E8E8",
        ink: "#161616",
        inkMuted: "#525252",
        border: "#C6C6C6",
        primary: "#0F62FE",
        primaryHover: "#0043CE",
        primarySoft: "#D0E2FF",
        focus: "#0F62FE",
        header: "#161616",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
      },
      layout: {
        density: "compact",
        sidebarWidth: "256px",
        contentMaxWidth: "1440px",
        controlHeight: "40px",
        cardRadius: "0px",
        buttonRadius: "0px",
        inputRadius: "0px",
      },
      components: { button: "solid", table: "dense", navigation: "sidebar" },
    },
  ),
  mintlify: base(
    "mintlify",
    "Mintlify",
    "Reading-optimized documentation system with calm green accents and clear content rhythm.",
    {
      tokens: {
        canvas: "#FFFFFF",
        surfaceMuted: "#F4F8F5",
        ink: "#1B2B22",
        inkMuted: "#65756B",
        border: "#DCE8DF",
        primary: "#18A058",
        primaryHover: "#137A43",
        primarySoft: "#E3F5EA",
        focus: "#18A058",
        header: "#1B2B22",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        mono: 'HisHope Geist Mono, "Geist Mono", ui-monospace, monospace',
        bodySize: "15px",
        titleSize: "28px",
        weight: "400",
        lineHeight: "1.6",
      },
      layout: {
        density: "spacious",
        sidebarWidth: "280px",
        contentMaxWidth: "1120px",
        controlHeight: "40px",
        cardRadius: "10px",
        buttonRadius: "8px",
        inputRadius: "8px",
      },
      components: { button: "soft", table: "editorial", navigation: "sidebar" },
    },
  ),
  raycast: base(
    "raycast",
    "Raycast",
    "Keyboard-first command surface with dark chrome, compact controls and vivid focus states.",
    {
      tokens: {
        canvas: "#161619",
        surface: "#202024",
        surfaceMuted: "#29292F",
        ink: "#F7F7FA",
        inkMuted: "#A6A6B1",
        border: "#3A3A43",
        primary: "#FF6363",
        primaryHover: "#FF8585",
        primarySoft: "#4A272C",
        focus: "#FF6363",
        header: "#0F0F11",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        bodySize: "13px",
        titleSize: "22px",
        weight: "500",
        lineHeight: "1.45",
      },
      layout: {
        density: "compact",
        sidebarWidth: "220px",
        contentMaxWidth: "1280px",
        controlHeight: "36px",
        cardRadius: "10px",
        buttonRadius: "8px",
        inputRadius: "8px",
      },
      components: { button: "solid", table: "dense", navigation: "command" },
    },
  ),
  sentry: base(
    "sentry",
    "Sentry",
    "Observability dashboard with dark data surfaces, high contrast and incident-oriented status color.",
    {
      tokens: {
        canvas: "#17151A",
        surface: "#211E25",
        surfaceMuted: "#2B2730",
        ink: "#F6F4F8",
        inkMuted: "#AAA3B1",
        border: "#403A47",
        primary: "#E1567A",
        primaryHover: "#F07A98",
        primarySoft: "#4A2533",
        focus: "#E1567A",
        header: "#17151A",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        mono: 'HisHope Plex Mono, "Cascadia Mono", Consolas, monospace',
      },
      layout: {
        density: "compact",
        sidebarWidth: "240px",
        contentMaxWidth: "1600px",
        controlHeight: "36px",
        cardRadius: "6px",
        buttonRadius: "6px",
        inputRadius: "6px",
      },
      components: { button: "solid", table: "dense", navigation: "workspace" },
    },
  ),
  spotify: base(
    "spotify",
    "Spotify",
    "Dark media workspace with energetic green actions and bold, scannable surfaces.",
    {
      tokens: {
        canvas: "#121212",
        surface: "#1E1E1E",
        surfaceMuted: "#2A2A2A",
        ink: "#FFFFFF",
        inkMuted: "#B3B3B3",
        border: "#3A3A3A",
        primary: "#1DB954",
        primaryHover: "#1ED760",
        primarySoft: "#173D28",
        focus: "#1ED760",
        header: "#000000",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        bodySize: "14px",
        titleSize: "28px",
        weight: "500",
        lineHeight: "1.5",
      },
      layout: {
        density: "comfortable",
        sidebarWidth: "240px",
        contentMaxWidth: "1440px",
        controlHeight: "40px",
        cardRadius: "8px",
        buttonRadius: "999px",
        inputRadius: "8px",
      },
      components: {
        button: "pill",
        table: "colorful",
        navigation: "workspace",
      },
    },
  ),
  vercel: base(
    "vercel",
    "Vercel",
    "Black and white deployment console with sharp hierarchy and precise technical controls.",
    {
      tokens: {
        canvas: "#FFFFFF",
        surfaceMuted: "#FAFAFA",
        ink: "#171717",
        inkMuted: "#666666",
        border: "#EAEAEA",
        primary: "#000000",
        primaryHover: "#333333",
        primarySoft: "#F0F0F0",
        focus: "#0070F3",
        header: "#000000",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        mono: 'HisHope Geist Mono, "Geist Mono", ui-monospace, monospace',
        bodySize: "14px",
        titleSize: "24px",
        weight: "400",
        lineHeight: "1.5",
      },
      layout: {
        density: "compact",
        sidebarWidth: "240px",
        contentMaxWidth: "1440px",
        controlHeight: "36px",
        cardRadius: "6px",
        buttonRadius: "6px",
        inputRadius: "6px",
      },
      components: { button: "solid", table: "dense", navigation: "workspace" },
    },
  ),
  wise: base(
    "wise",
    "Wise",
    "Clear financial workflow with bright green accent, friendly hierarchy and decisive actions.",
    {
      tokens: {
        canvas: "#F7F7F5",
        surfaceMuted: "#EFF3EC",
        ink: "#163300",
        inkMuted: "#55704A",
        border: "#D8E1D2",
        primary: "#163300",
        primaryHover: "#234D00",
        primarySoft: "#E1F0D8",
        focus: "#9FE870",
        header: "#163300",
      },
      typography: {
        body: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        display: "HisHope Inter, Inter, ui-sans-serif, system-ui, sans-serif",
        bodySize: "15px",
        titleSize: "28px",
        weight: "400",
        lineHeight: "1.55",
      },
      layout: {
        density: "comfortable",
        sidebarWidth: "256px",
        contentMaxWidth: "1280px",
        controlHeight: "44px",
        cardRadius: "8px",
        buttonRadius: "999px",
        inputRadius: "8px",
      },
      components: { button: "pill", table: "quiet", navigation: "sidebar" },
    },
  ),
};

export const DEFAULT_HIS_HOPE_DESIGN_PRESET: HisHopeDesignPresetId = "supabase";

export function getHisHopeDesignPreset(
  id: string | null | undefined,
): HisHopeDesignPreset {
  return (
    HIS_HOPE_DESIGN_PRESETS[id as HisHopeDesignPresetId] ??
    HIS_HOPE_DESIGN_PRESETS[DEFAULT_HIS_HOPE_DESIGN_PRESET]
  );
}
