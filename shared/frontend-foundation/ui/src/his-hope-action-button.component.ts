import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from "@angular/core";

export type HisHopeActionKind =
  | "primary"
  | "secondary"
  | "diagnostic"
  | "danger"
  | "row";
export type HisHopeActionMode = "label" | "icon-only";

@Component({
  selector: "hh-action-button",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      [class]="classes()"
      [disabled]="disabled() || loading()"
      [attr.aria-label]="mode() === 'icon-only' ? label() : null"
      [attr.aria-busy]="loading() || null"
      [attr.data-hh-action]="kind()"
      (click)="pressed.emit()"
    >
      <span class="material-icons" aria-hidden="true">{{ icon() }}</span>
      @if (mode() === "label") {
        <span>{{ label() }}</span>
      }
    </button>
  `,
  styles: [
    `
      :host {
        display: inline-block;
      }
      button {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        gap: var(--space-sm);
        min-height: var(--control-height);
        padding: 0 var(--space-lg);
        border: 1px solid transparent;
        border-radius: var(--radius-button);
        font: inherit;
        font-weight: var(--font-weight-semibold);
        cursor: pointer;
        transition:
          background-color 0.15s ease,
          border-color 0.15s ease,
          opacity 0.15s ease;
      }
      button:disabled {
        cursor: not-allowed;
        opacity: 0.62;
      }
      .hh-action--primary {
        background: var(--color-primary);
        color: var(--color-on-primary);
      }
      .hh-action--secondary,
      .hh-action--diagnostic {
        border-color: var(--border-default);
        background: var(--surface-white);
        color: var(--text-primary);
      }
      .hh-action--diagnostic {
        border-style: dashed;
      }
      .hh-action--danger {
        background: var(--color-danger);
        color: var(--color-on-danger);
      }
      .hh-action--row {
        width: var(--control-height);
        min-height: var(--control-height);
        padding: 0;
        background: transparent;
        color: var(--text-secondary);
      }
      button:hover:not(:disabled) {
        filter: brightness(0.97);
      }
      .hh-action--row:hover:not(:disabled) {
        background: var(--surface-muted);
        color: var(--text-primary);
      }
    `,
  ],
})
export class HisHopeActionButtonComponent {
  readonly kind = input<HisHopeActionKind>("secondary");
  readonly mode = input<HisHopeActionMode>("label");
  readonly icon = input.required<string>();
  readonly label = input.required<string>();
  readonly disabled = input(false);
  readonly loading = input(false);
  readonly pressed = output<void>();

  classes(): string {
    return `hh-action--${this.kind()}${this.mode() === "icon-only" ? " hh-action--icon-only" : ""}`;
  }
}
