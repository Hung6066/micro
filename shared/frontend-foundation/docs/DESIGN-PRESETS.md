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

## Source and attribution

The inspiration catalog is [getdesign.md](https://getdesign.md/). Each registry item exposes its source URL. The local implementation intentionally stores only a compact, neutral design brief and tokens so product code does not depend on third-party logos, proprietary fonts or copied website content.
