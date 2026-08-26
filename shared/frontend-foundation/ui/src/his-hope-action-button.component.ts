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
      [type]="type()"
      [class]="classes()"
      [disabled]="disabled() || loading()"
      [attr.aria-label]="mode() === 'icon-only' ? label() : null"
      [attr.aria-pressed]="pressedState() === null ? null : pressedState()"
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
        display: inline-flex;
        vertical-align: middle;
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
          background-color var(--motion-fast) var(--ease-standard),
          border-color var(--motion-fast) var(--ease-standard),
          color var(--motion-fast) var(--ease-standard),
          opacity var(--motion-fast) var(--ease-standard);
      }
      button:disabled {
        cursor: not-allowed;
        opacity: 0.62;
      }
      .hh-action--primary {
        background: var(--button-primary-bg, var(--color-primary));
        color: var(--color-on-primary);
      }
      .hh-action--secondary,
      .hh-action--diagnostic {
        border-color: var(--button-secondary-border, var(--border-default));
        background: var(--button-secondary-bg, var(--surface-white));
        color: var(--button-secondary-text, var(--text-primary));
      }
      .hh-action--diagnostic {
        border-style: dashed;
      }
      .hh-action--danger {
        background: var(--button-danger-bg, var(--color-danger));
        color: var(--color-on-danger);
      }
      .hh-action--icon-only {
        width: var(--touch-target);
        min-width: var(--touch-target);
        padding: 0;
      }
      .hh-action--row {
        width: var(--touch-target);
        min-width: var(--touch-target);
        min-height: var(--touch-target);
        padding: 0;
        background: transparent;
        color: var(--text-secondary);
      }
      button:hover:not(:disabled) {
        border-color: var(--border-strong);
      }
      .hh-action--primary:hover:not(:disabled) {
        background: var(--button-primary-hover, var(--color-primary-hover));
      }
      .hh-action--secondary:hover:not(:disabled),
      .hh-action--diagnostic:hover:not(:disabled) {
        background: var(--button-secondary-hover, var(--surface-muted));
      }
      .hh-action--danger:hover:not(:disabled) {
        background: color-mix(in srgb, var(--button-danger-bg, var(--color-danger)) 88%, black);
      }
      button:focus-visible {
        outline: var(--focus-ring-width) solid var(--color-focus);
        outline-offset: var(--focus-ring-offset);
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
  readonly type = input<"button" | "submit" | "reset">("button");
  readonly pressedState = input<boolean | null>(null);
  readonly pressed = output<void>();

  classes(): string {
    return `hh-action--${this.kind()}${this.mode() === "icon-only" ? " hh-action--icon-only" : ""}`;
  }
}
