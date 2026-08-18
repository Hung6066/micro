import { ChangeDetectionStrategy, Component, inject } from "@angular/core";
import {
  HIS_HOPE_DESIGN_PRESETS,
  HisHopeDesignPresetId,
} from "../presets/design-presets";
import { HisHopeThemeService } from "./his-hope-theme.service";

/**
 * Plugs a full enterprise design system (fonts, colors, radii, button/table/
 * navigation shape) into the running app by switching `HisHopeThemeService`'s
 * active preset. Any of the 20 registry presets can be selected; no per-app
 * CSS is required because `_presets.scss` keys off the preset's declared
 * `components.*` variant rather than the preset id.
 */
@Component({
  selector: "hh-preset-switcher",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="hh-preset-switcher">
      <span class="hh-preset-switcher__label">{{ label }}</span>
      <select
        class="hh-preset-switcher__select"
        [value]="theme.preset()"
        (change)="onChange($event)"
        [attr.aria-label]="label"
      >
        @for (preset of presets; track preset.id) {
          <option [value]="preset.id">{{ preset.name }}</option>
        }
      </select>
    </label>
  `,
  styles: [
    `
      :host {
        display: inline-block;
      }
      .hh-preset-switcher {
        display: inline-flex;
        align-items: center;
        gap: var(--space-2);
        color: inherit;
      }
      .hh-preset-switcher__label {
        font-size: var(--font-size-label);
        font-weight: var(--font-weight-semibold);
      }
      .hh-preset-switcher__select {
        min-height: var(--touch-target);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-button);
        padding: 0 var(--space-3);
        background: var(--surface-white);
        color: var(--text-primary);
        font: inherit;
      }
      .hh-preset-switcher__select:focus-visible {
        outline: 2px solid var(--color-focus);
        outline-offset: 2px;
      }
    `,
  ],
})
export class HisHopePresetSwitcherComponent {
  readonly theme = inject(HisHopeThemeService);
  readonly label = "Design preset";
  readonly presets = Object.values(HIS_HOPE_DESIGN_PRESETS);

  onChange(event: Event): void {
    const value = (event.target as HTMLSelectElement)
      .value as HisHopeDesignPresetId;
    this.theme.setPreset(value);
  }
}
