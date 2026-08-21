import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  input,
  output,
} from "@angular/core";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

/**
 * Modal shown when the API rejects a request with 401. Unlike the idle-timeout
 * dialog there is nothing left to extend, so the only forward action is a new
 * sign-in.
 */
@Component({
  selector: "hh-mobile-session-expired-dialog",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="hh-mobile-session-expired__backdrop" role="presentation">
        <section
          class="hh-mobile-session-expired"
          role="alertdialog"
          aria-modal="true"
          aria-live="assertive"
          [attr.aria-label]="title() | hhTranslate: titleFallback()"
        >
          <div class="hh-mobile-session-expired__icon material-icons" aria-hidden="true">
            lock_clock
          </div>
          <h2>{{ title() | hhTranslate: titleFallback() }}</h2>
          <p>{{ message() | hhTranslate: messageFallback() }}</p>
          <div class="hh-mobile-session-expired__actions">
            @if (showDismiss()) {
              <button
                type="button"
                class="hh-button hh-button--secondary"
                (click)="dismiss.emit()"
              >
                {{ dismissLabel() | hhTranslate: dismissLabelFallback() }}
              </button>
            }
            <button
              type="button"
              class="hh-button hh-button--primary"
              (click)="reLogin.emit()"
            >
              {{ reLoginLabel() | hhTranslate: reLoginLabelFallback() }}
            </button>
          </div>
        </section>
      </div>
    }
  `,
  styles: [
    `
      :host {
        display: contents;
      }
      .hh-mobile-session-expired__backdrop {
        position: fixed;
        inset: 0;
        z-index: 150;
        display: grid;
        place-items: center;
        padding: var(--space-2xl);
        background: color-mix(in srgb, #000 46%, transparent);
      }
      .hh-mobile-session-expired {
        display: grid;
        justify-items: center;
        gap: var(--space-sm);
        width: min(100%, 360px);
        padding: 26px var(--size-config-nav-indicator) calc(var(--size-config-nav-indicator) + env(safe-area-inset-bottom));
        border-radius: var(--radius-sheet);
        background: var(--surface-white);
        color: var(--text-primary);
        text-align: center;
      }
      .hh-mobile-session-expired__icon {
        color: var(--color-warning, #b45309);
        font-size: var(--font-size-display-md);
      }
      h2 {
        margin: var(--space-2xs) 0 0;
        font-size: var(--font-size-subhead);
      }
      p {
        margin: 0;
        color: var(--text-secondary);
        line-height: 1.5;
      }
      .hh-mobile-session-expired__actions {
        display: flex;
        gap: var(--space-md);
        margin-top: var(--space-md);
      }
      .hh-button {
        min-height: var(--control-height-touch);
        padding: 0 var(--space-lg);
        border: 1px solid transparent;
        border-radius: var(--radius-button);
        font: inherit;
        font-weight: var(--button-font-weight);
      }
      .hh-button--primary {
        background: var(--button-primary-bg);
        color: var(--button-primary-text);
      }
      .hh-button--secondary {
        border-color: var(--button-secondary-border);
        background: var(--button-secondary-bg);
        color: var(--button-secondary-text);
      }
    `,
  ],
})
export class HisHopeMobileSessionExpiredDialogComponent {
  readonly open = input(false);
  readonly title = input("mobile.sessionExpiredTitle");
  readonly titleFallback = input("Your session expired");
  readonly message = input("mobile.sessionExpiredMessage");
  readonly messageFallback = input(
    "Sign in again to continue administering identities.",
  );
  readonly reLoginLabel = input("mobile.signInAgain");
  readonly reLoginLabelFallback = input("Sign in again");
  readonly showDismiss = input(false);
  readonly dismissLabel = input("common.cancel");
  readonly dismissLabelFallback = input("Not now");

  readonly reLogin = output<void>();
  readonly dismiss = output<void>();

  @HostListener("document:keydown.escape")
  onEscape(): void {
    if (this.open() && this.showDismiss()) this.dismiss.emit();
  }
}
