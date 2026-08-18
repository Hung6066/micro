# Design presets

The foundation ships 20 typed presets based on the public `getdesign.md` catalog. They are reusable starting points, not copies of the referenced brands. The presets contain neutral tokens and interaction recipes for shell, typography, controls and data-heavy pages.

## Presets

`linear`, `intercom`, `supabase`, `hashicorp`, `stripe`, `notion`, `airtable`, `apple`, `cal`, `claude`, `coinbase`, `cursor`, `figma`, `ibm`, `mintlify`, `raycast`, `sentry`, `spotify`, `vercel`, `wise`.

Each folder under `docs/design-presets/` contains the complete catalog `DESIGN.md` for that preset, including every source-defined color, typography, radius, spacing, component variant, responsive rule and do/don't rule. The runtime TypeScript registry is a safe application subset of those files; it does not replace the full design brief used by coding agents.

## Runtime usage

```ts
import {
  HisHopeThemeService,
  HIS_HOPE_DESIGN_PRESETS,
  type HisHopeDesignPresetId,
} from '@his-hope/frontend-foundation';

private readonly theme = inject(HisHopeThemeService);

selectPreset(id: HisHopeDesignPresetId): void {
  this.theme.setPreset(id);
}

readonly presets = Object.values(HIS_HOPE_DESIGN_PRESETS);
```

`HisHopeThemeService` persists the selected preset as `hh-ui-preset`, applies `data-ui-preset` to the document root and keeps the selected light/dark/high-contrast state in the same foundation contract.

## Plug-in component switcher

`<hh-preset-switcher />` (exported from `@his-hope/frontend-foundation`) renders a select bound to `HisHopeThemeService`, so any app can let an operator swap the entire design system at runtime without app-specific code.

## Full-system variants, not just colors

Selecting a preset does more than swap colors and fonts. Each preset declares `components.button` (`solid` | `soft` | `outline` | `pill`), `components.table` (`dense` | `quiet` | `colorful` | `editorial`) and `components.navigation` (`rail` | `sidebar` | `workspace` | `command`). `HisHopeThemeService` mirrors these onto `data-ui-button-style`, `data-ui-table-style` and `data-ui-nav-style` on the document root, and `_presets.scss` keys its button/table/navigation shape rules off those attribute values rather than the preset id. This means every one of the 20 presets automatically gets a matching button/table/navigation treatment the moment it is selected — plugging in a preset is enough to get its complete look, not only its palette.

## Source and attribution

The inspiration catalog is [getdesign.md](https://getdesign.md/). Each registry item exposes its source URL. The local implementation intentionally stores only a compact, neutral design brief and tokens so product code does not depend on third-party logos, proprietary fonts or copied website content.

## Bundled typography

The foundation bundles open-license Latin families found in the preset briefs: Inter, IBM Plex Sans, IBM Plex Mono, Source Serif 4, Cal Sans, Geist, Geist Mono, Rubik and JetBrains Mono. Manrope and DM Sans assets are also retained as open-license fallback families for compatibility. Presets that reference proprietary families in the source briefs use these local substitutes so typography remains deterministic and works offline in Docker; the original proprietary font files are not redistributed.
