import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from "@angular/core";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeMobileBottomSheetComponent } from "./his-hope-mobile-components";

/** Formats a mobile client can render without a spreadsheet engine. */
export type HisHopeMobileExportFormat = "csv" | "json";

/** Bottom sheet that asks the operator which export format to generate. */
@Component({
  selector: "hh-mobile-export-sheet",
  standalone: true,
  imports: [HisHopeMobileBottomSheetComponent, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-mobile-bottom-sheet
      [open]="open()"
      [label]="label() | hhTranslate: labelFallback()"
      (close)="close.emit()"
    >
      <p class="hh-mobile-export-sheet__hint">
        {{ hint() | hhTranslate: hintFallback() }}
      </p>
      @for (format of formats(); track format) {
        <button
          type="button"
          class="hh-mobile-export-sheet__option"
          (click)="select(format)"
        >
          <span class="hh-mobile-export-sheet__format">{{
            format.toUpperCase()
          }}</span>
          <span class="hh-mobile-export-sheet__description">{{
            descriptionKey(format) | hhTranslate: descriptionFallback(format)
          }}</span>
        </button>
      }
    </hh-mobile-bottom-sheet>
  `,
  styles: [
    `
      :host {
        display: contents;
      }
      .hh-mobile-export-sheet__hint {
        margin: 0 0 var(--space-md);
        color: var(--text-secondary);
        font-size: var(--font-size-label);
        line-height: 1.5;
      }
      .hh-mobile-export-sheet__option {
        display: grid;
        gap: var(--space-hairline);
        width: 100%;
        min-height: var(--mobile-toolbar-height);
        padding: var(--space-sm) var(--space-md);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-control);
        background: var(--surface-white);
        color: var(--text-primary);
        font: inherit;
        text-align: left;
      }
      .hh-mobile-export-sheet__option + .hh-mobile-export-sheet__option {
        margin-top: var(--space-sm);
      }
      .hh-mobile-export-sheet__format {
        font-weight: var(--font-weight-semibold);
      }
      .hh-mobile-export-sheet__description {
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
    `,
  ],
})
export class HisHopeMobileExportSheetComponent {
  readonly open = input(false);
  readonly label = input("mobile.exportTitle");
  readonly labelFallback = input("Export records");
  readonly hint = input("mobile.exportHint");
  readonly hintFallback = input(
    "The export covers the rows matching the current filters.",
  );
  readonly formats = input<readonly HisHopeMobileExportFormat[]>([
    "csv",
    "json",
  ]);

  readonly close = output<void>();
  readonly exportRequested = output<HisHopeMobileExportFormat>();

  select(format: HisHopeMobileExportFormat): void {
    this.exportRequested.emit(format);
    this.close.emit();
  }

  descriptionKey(format: HisHopeMobileExportFormat): string {
    return format === "csv" ? "mobile.exportCsv" : "mobile.exportJson";
  }

  descriptionFallback(format: HisHopeMobileExportFormat): string {
    return format === "csv"
      ? "Spreadsheet-friendly, one row per record."
      : "Structured payload including nested fields.";
  }
}
