import { DOCUMENT, isPlatformBrowser } from "@angular/common";
import { Injectable, PLATFORM_ID, inject, signal } from "@angular/core";
import {
  hisHopeTypographyCssVariables,
} from "../contracts/his-hope-typography.contract";
import {
  DEFAULT_HIS_HOPE_DESIGN_PRESET,
  getHisHopeDesignPreset,
  HisHopeDesignPreset,
  HisHopeDesignPresetId,
} from "../presets/design-presets";

export type HisHopeTheme = "light" | "dark" | "system";
export type HisHopeResolvedTheme = "light" | "dark";
export type HisHopePlatform = "mobile" | "desktop";

@Injectable({ providedIn: "root" })
export class HisHopeThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  readonly theme = signal<HisHopeTheme>("system");
  readonly resolvedTheme = signal<HisHopeResolvedTheme>("light");
  readonly highContrast = signal(false);
  readonly preset = signal<HisHopeDesignPresetId>(
    DEFAULT_HIS_HOPE_DESIGN_PRESET,
  );
  private systemMediaQuery?: MediaQueryList;
  private syncChannel?: BroadcastChannel;

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.systemMediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
      this.systemMediaQuery.addEventListener("change", () => {
        if (this.theme() === "system") this.applyThemeAttribute("system");
      });
      if ("BroadcastChannel" in window) {
        this.syncChannel = new BroadcastChannel("his-hope-preferences");
        this.syncChannel.onmessage = ({ data }) => {
          if (
            data?.type === "theme" &&
            (data.theme === "light" ||
              data.theme === "dark" ||
              data.theme === "system")
          )
            this.setTheme(data.theme, false);
          if (data?.type === "contrast" && typeof data.enabled === "boolean")
            this.setHighContrast(data.enabled, false);
          if (data?.type === "preset" && typeof data.preset === "string")
            this.setPreset(data.preset as HisHopeDesignPresetId, false);
        };
      }
    }
    this.restore();
  }

  setTheme(theme: HisHopeTheme, broadcast = true): void {
    this.theme.set(theme);
    if (!isPlatformBrowser(this.platformId)) return;
    this.applyThemeAttribute(theme);
    localStorage.setItem("hh-theme", theme);
    if (broadcast) this.syncChannel?.postMessage({ type: "theme", theme });
  }

  setHighContrast(enabled: boolean, broadcast = true): void {
    this.highContrast.set(enabled);
    if (!isPlatformBrowser(this.platformId)) return;
    this.applyContrastAttribute(enabled);
    localStorage.setItem("hh-contrast", String(enabled));
    if (broadcast) this.syncChannel?.postMessage({ type: "contrast", enabled });
  }

  setPreset(preset: HisHopeDesignPresetId, broadcast = true): void {
    const selected = getHisHopeDesignPreset(preset);
    this.preset.set(selected.id);
    if (!isPlatformBrowser(this.platformId)) return;
    this.applyPreset(selected);
    this.applyThemeAttribute(this.theme());
    localStorage.setItem("hh-ui-preset", selected.id);
    if (broadcast)
      this.syncChannel?.postMessage({ type: "preset", preset: selected.id });
  }

  /** Applies `[data-platform]` for mobile density/type overrides. */
  setPlatform(platform: HisHopePlatform | null): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const root = this.document.documentElement;
    if (platform) root.dataset["platform"] = platform;
    else root.removeAttribute("data-platform");
  }

  restore(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const storedTheme = localStorage.getItem("hh-theme") as HisHopeTheme | null;
    const storedContrast = localStorage.getItem("hh-contrast");
    const queryPreset = new URLSearchParams(window.location.search).get("ui");
    const storedPreset = queryPreset ?? localStorage.getItem("hh-ui-preset");
    this.theme.set(
      storedTheme === "light" ||
        storedTheme === "dark" ||
        storedTheme === "system"
        ? storedTheme
        : "system",
    );
    this.highContrast.set(storedContrast === "true");
    const selected = getHisHopeDesignPreset(storedPreset);
    this.preset.set(selected.id);
    this.applyThemeAttribute(this.theme());
    this.applyContrastAttribute(this.highContrast());
    this.applyPreset(selected);
  }

  private applyThemeAttribute(theme: HisHopeTheme): void {
    const root = this.document.documentElement;
    const resolvedTheme: HisHopeResolvedTheme =
      theme === "system"
        ? this.systemMediaQuery?.matches
          ? "dark"
          : "light"
        : theme;
    this.resolvedTheme.set(resolvedTheme);
    root.dataset["theme"] = resolvedTheme;
    root.dataset["themeMode"] = theme;
    if (isPlatformBrowser(this.platformId)) {
      const selected = getHisHopeDesignPreset(this.preset());
      if (resolvedTheme === "dark") this.applyDarkVariables(selected);
      else this.applyPresetVariables(selected);
    }
    // Keep body compatibility for feature styles that predate the root theme contract.
    if (this.document.body) {
      this.document.body.dataset["theme"] = resolvedTheme;
      this.document.body.dataset["themeMode"] = theme;
    }
  }

  private applyContrastAttribute(enabled: boolean): void {
    if (enabled) this.document.documentElement.dataset["contrast"] = "high";
    else this.document.documentElement.removeAttribute("data-contrast");
  }

  private applyPreset(preset: HisHopeDesignPreset): void {
    const root = this.document.documentElement;
    root.dataset["uiPreset"] = preset.id;
    root.dataset["uiButtonStyle"] = preset.components.button;
    root.dataset["uiTableStyle"] = preset.components.table;
    root.dataset["uiNavStyle"] = preset.components.navigation;
    this.applyPresetVariables(preset);
  }

  private applyPresetVariables(preset: HisHopeDesignPreset): void {
    const root = this.document.documentElement;
    const cssVars: Record<string, string> = {
      "--bg-warm": preset.tokens.canvas,
      "--surface-white": preset.tokens.surface,
      "--surface-muted": preset.tokens.surfaceMuted,
      "--text-primary": preset.tokens.ink,
      "--text-secondary": preset.tokens.inkMuted,
      "--border-default": preset.tokens.border,
      "--color-primary": preset.tokens.primary,
      "--color-primary-hover": preset.tokens.primaryHover,
      "--color-primary-soft": preset.tokens.primarySoft,
      "--color-on-primary": this.getOnColor(preset.tokens.primary),
      "--color-focus": preset.tokens.focus,
      "--shell-header-bg": preset.tokens.header,
      "--font-sans": preset.typography.body,
      "--font-body": preset.typography.body,
      "--font-display": preset.typography.display,
      "--font-mono": preset.typography.mono,
      ...hisHopeTypographyCssVariables(preset.typography.scale),
      "--font-weight-regular": preset.typography.weight,
      "--leading-body": preset.typography.lineHeight,
      "--leading-tight": preset.typography.lineHeightTight,
      "--shell-sidebar-width": preset.layout.sidebarWidth,
      "--max-width-container": preset.layout.contentMaxWidth,
      "--control-height": preset.layout.controlHeight,
      "--radius-card": preset.layout.cardRadius,
      "--radius-button": preset.layout.buttonRadius,
      "--radius-input": preset.layout.inputRadius,
    };
    Object.entries(cssVars).forEach(([name, value]) =>
      root.style.setProperty(name, value),
    );
  }

  private applyDarkVariables(preset: HisHopeDesignPreset): void {
    const root = this.document.documentElement;
    const cssVars: Record<string, string> = {
      "--bg-warm": preset.tokens.darkCanvas,
      "--surface-white": preset.tokens.darkSurface,
      "--surface-muted": preset.tokens.darkSurfaceMuted,
      "--text-primary": preset.tokens.darkInk,
      "--text-secondary": preset.tokens.darkInkMuted,
      "--border-default": preset.tokens.darkBorder,
      "--color-primary": preset.tokens.darkPrimary,
      "--color-primary-hover": preset.tokens.darkPrimaryHover,
      "--color-primary-soft": preset.tokens.darkPrimarySoft,
      "--color-focus": preset.tokens.darkFocus,
      "--shell-header-bg": preset.tokens.darkHeader,
      "--color-on-primary": preset.tokens.darkOnPrimary,
    };
    Object.entries(cssVars).forEach(([name, value]) =>
      root.style.setProperty(name, value),
    );
  }

  private getOnColor(background: string): string {
    const value = background.trim().replace(/^#/, "");
    if (!/^[0-9a-f]{6}$/i.test(value)) return "var(--surface-white)";
    const channels = [0, 2, 4].map((offset) =>
      Number.parseInt(value.slice(offset, offset + 2), 16) / 255,
    );
    const luminance = channels.reduce((sum, channel, index) => {
      const linear = channel <= 0.03928
        ? channel / 12.92
        : Math.pow((channel + 0.055) / 1.055, 2.4);
      return sum + linear * [0.2126, 0.7152, 0.0722][index];
    }, 0);
    return luminance > 0.179
      ? "var(--text-primary)"
      : "var(--surface-white)";
  }
}
