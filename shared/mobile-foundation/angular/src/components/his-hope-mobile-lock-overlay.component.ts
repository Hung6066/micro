import { Component, inject, output, signal } from "@angular/core";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeMobileIconComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeMobileLockService } from "../services/lock.service";

/** Full-screen idle/background lock overlay with biometric and PIN unlock. */
@Component({
  selector: "hh-mobile-lock-overlay",
  standalone: true,
  imports: [HisHopeMobileIconComponent, HisHopeTranslatePipe],
  template: `
    @if (lock.locked()) {
      <div
        class="lock-overlay"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="'mobile.sessionLocked' | hhTranslate"
      >
        <hh-mobile-icon name="biometric" size="large" />
        <h2>{{ "mobile.sessionLocked" | hhTranslate }}</h2>
        <p>{{ "mobile.unlockContinue" | hhTranslate }}</p>
        <button class="hh-button hh-button--primary" type="button" (click)="attemptUnlock()">
          {{ "mobile.unlock" | hhTranslate }}
        </button>
        @if (unlockFailed()) {
          <p class="lock-overlay__error">{{ "mobile.unlockFailed" | hhTranslate }}</p>
        }
        @if (pinConfigured()) {
          @if (showPinEntry()) {
            <label class="lock-overlay__pin">
              <span>{{ "mobile.pinPlaceholder" | hhTranslate }}</span>
              <input
                inputmode="numeric"
                autocomplete="current-password"
                maxlength="12"
                [value]="pinDraft()"
                (input)="pinDraft.set($any($event.target).value)"
              />
            </label>
            <button
              class="hh-button hh-button--secondary"
              type="button"
              [disabled]="pinDraft().length < 4"
              (click)="attemptPinUnlock()"
            >
              {{ "mobile.unlockWithPin" | hhTranslate }}
            </button>
            @if (pinUnlockFailed()) {
              <p class="lock-overlay__error">{{ "mobile.pinUnlockFailed" | hhTranslate }}</p>
            }
          } @else {
            <button class="lock-overlay__signout" type="button" (click)="showPinEntry.set(true)">
              {{ "mobile.unlockWithPin" | hhTranslate }}
            </button>
          }
        }
        <button class="lock-overlay__signout" type="button" (click)="signOut.emit()">
          {{ "mobile.signOutInstead" | hhTranslate }}
        </button>
      </div>
    }
  `,
  styles: [
    `
      .lock-overlay {
        position: fixed;
        inset: 0;
        z-index: 50;
        display: grid;
        justify-items: center;
        align-content: center;
        gap: var(--space-inset, 0.75rem);
        padding: var(--space-2xl, 1.5rem);
        text-align: center;
        background: color-mix(in srgb, var(--bg-warm, #f6f4ef) 96%, transparent);
        backdrop-filter: blur(var(--blur-overlay, 12px));
      }
      .lock-overlay hh-mobile-icon {
        width: var(--mobile-toolbar-height, 3rem);
        height: var(--mobile-toolbar-height, 3rem);
        color: var(--color-primary, #006b4f);
      }
      .lock-overlay h2 {
        margin: var(--space-2xs, 0.25rem) 0 0;
      }
      .lock-overlay p {
        margin: 0;
        color: var(--text-secondary, #68756d);
        max-width: 32ch;
      }
      .lock-overlay__error {
        color: var(--color-danger, #b3261e);
        font-size: var(--font-size-label, 0.8125rem);
      }
      .lock-overlay__signout {
        margin-top: var(--space-sm, 0.5rem);
        border: 0;
        background: transparent;
        color: var(--text-secondary, #68756d);
        text-decoration: underline;
      }
      .lock-overlay__pin {
        display: grid;
        gap: var(--space-xs, 0.35rem);
        width: min(100%, var(--max-width-mobile-nav-label, 16rem));
        text-align: left;
        font-size: var(--font-size-label, 0.8125rem);
      }
      .lock-overlay__pin input {
        min-height: var(--touch-target, 2.75rem);
        padding: 0 var(--space-md, 0.75rem);
        border: 1px solid var(--border-default, #c7d1cb);
        border-radius: var(--radius-chip, 0.5rem);
        font: inherit;
      }
    `,
  ],
})
export class HisHopeMobileLockOverlayComponent {
  readonly lock = inject(HisHopeMobileLockService);
  readonly signOut = output<void>();

  readonly pinConfigured = signal(false);
  readonly showPinEntry = signal(false);
  readonly pinDraft = signal("");
  readonly unlockFailed = signal(false);
  readonly pinUnlockFailed = signal(false);

  constructor() {
    void this.syncPinConfigured();
  }

  private async syncPinConfigured(): Promise<void> {
    this.pinConfigured.set(await this.lock.pinConfigured());
  }

  async attemptUnlock(): Promise<void> {
    this.unlockFailed.set(false);
    this.pinUnlockFailed.set(false);
    const ok = await this.lock.unlock();
    this.unlockFailed.set(!ok);
    if (!ok) {
      this.pinConfigured.set(await this.lock.pinConfigured());
    }
  }

  async attemptPinUnlock(): Promise<void> {
    this.pinUnlockFailed.set(false);
    const ok = await this.lock.unlockWithPin(this.pinDraft());
    if (ok) {
      this.showPinEntry.set(false);
      this.pinDraft.set("");
      this.unlockFailed.set(false);
      return;
    }
    this.pinUnlockFailed.set(true);
  }
}
