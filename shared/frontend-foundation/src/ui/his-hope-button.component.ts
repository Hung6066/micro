import { ChangeDetectionStrategy, Component, input } from "@angular/core";

export type HisHopeButtonVariant = "primary" | "secondary" | "danger" | "ghost";
export type HisHopeButtonSize = "small" | "medium" | "large";

@Component({
  selector: "hh-button",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      [attr.type]="type()"
      [class]="classes()"
      [disabled]="disabled() || loading()"
      [attr.aria-label]="iconOnly() ? ariaLabel() : null"
      [attr.aria-busy]="loading() || null"
    >
      @if (loading()) {
        <span class="hh-button__spinner" aria-hidden="true"></span>
      }
      <span [class.hh-button__content--hidden]="loading()"><ng-content /></span>
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
        gap: 8px;
        min-height: 40px;
        padding: 0 16px;
        border: 1px solid transparent;
        border-radius: var(--radius-button);
        font: inherit;
        font-weight: 600;
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
      .hh-button--small {
        min-height: 32px;
        padding: 0 12px;
        font-size: var(--font-size-caption);
      }
      .hh-button--large {
        min-height: 48px;
        padding: 0 20px;
      }
      .hh-button--primary {
        background: var(--color-primary);
        color: var(--text-on-primary, #fff);
      }
      .hh-button--secondary {
        border-color: var(--border-default);
        background: var(--surface-white);
        color: var(--text-primary);
      }
      .hh-button--danger {
        background: var(--color-danger);
        color: #fff;
      }
      .hh-button--ghost {
        background: transparent;
        color: var(--text-primary);
      }
      .hh-button--icon-only {
        width: 40px;
        padding: 0;
      }
      .hh-button__content--hidden {
        visibility: hidden;
      }
      .hh-button__spinner {
        width: 16px;
        height: 16px;
        border: 2px solid currentColor;
        border-right-color: transparent;
        border-radius: 50%;
        animation: hh-button-spin 0.7s linear infinite;
        position: absolute;
      }
      @keyframes hh-button-spin {
        to {
          transform: rotate(360deg);
        }
      }
    `,
  ],
})
export class HisHopeButtonComponent {
  readonly variant = input<HisHopeButtonVariant>("primary");
  readonly size = input<HisHopeButtonSize>("medium");
  readonly loading = input(false);
  readonly iconOnly = input(false);
  readonly disabled = input(false);
  readonly type = input<"button" | "submit" | "reset">("button");
  readonly ariaLabel = input("Action");

  classes(): string {
    return `hh-button--${this.variant()} hh-button--${this.size()}${this.iconOnly() ? " hh-button--icon-only" : ""}`;
  }
}
