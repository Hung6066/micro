import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from "@angular/core";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeMobileIconComponent } from "./his-hope-mobile-icon.component";

/** A single device-integrity signal that caused the block. */
export interface HisHopeMobileDeviceBlockReason {
  key: string;
  fallback?: string;
}

/**
 * Terminal block screen for devices that failed the integrity check
 * (rooted/jailbroken, emulator, tampered). There is no dismiss action: the
 * only supported exit is signing out.
 */
@Component({
  selector: "hh-mobile-device-blocked-screen",
  standalone: true,
  imports: [HisHopeMobileIconComponent, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section
      class="hh-mobile-device-blocked"
      role="alertdialog"
      aria-modal="true"
      aria-live="assertive"
      [attr.aria-label]="title() | hhTranslate: titleFallback()"
    >
      <hh-mobile-icon name="security" size="large" />
      <h1>{{ title() | hhTranslate: titleFallback() }}</h1>
      <p>{{ message() | hhTranslate: messageFallback() }}</p>
      @if (reasons().length) {
        <ul class="hh-mobile-device-blocked__reasons">
          @for (reason of reasons(); track reason.key) {
            <li>{{ reason.key | hhTranslate: (reason.fallback ?? "") }}</li>
          }
        </ul>
      }
      @if (showSignOut()) {
        <button
          type="button"
          class="hh-mobile-device-blocked__sign-out"
          (click)="signOut.emit()"
        >
          {{ signOutLabel() | hhTranslate: signOutLabelFallback() }}
        </button>
      }
    </section>
  `,
  styles: [
    `
      :host {
        display: contents;
      }
      .hh-mobile-device-blocked {
        position: fixed;
        inset: 0;
        z-index: 210;
        display: grid;
        justify-items: center;
        align-content: center;
        gap: var(--space-md);
        padding: var(--space-2xl);
        text-align: center;
        background: var(--bg-warm);
        color: var(--text-primary);
      }
      .hh-mobile-device-blocked hh-mobile-icon {
        width: var(--mobile-toolbar-height);
        height: var(--mobile-toolbar-height);
        color: var(--color-danger, #b3261e);
      }
      h1 {
        margin: var(--space-2xs) 0 0;
        font-size: var(--font-size-section);
      }
      p {
        margin: 0;
        max-width: 34ch;
        color: var(--text-secondary);
        line-height: 1.5;
      }
      .hh-mobile-device-blocked__reasons {
        display: grid;
        gap: var(--space-2xs);
        margin: var(--space-2xs) 0 0;
        padding: 0;
        color: var(--color-danger, #b3261e);
        font-size: var(--font-size-label);
        list-style: none;
      }
      .hh-mobile-device-blocked__sign-out {
        min-height: var(--control-height-touch);
        margin-top: var(--space-md);
        padding: 0 var(--space-xl);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-control);
        background: var(--surface-white);
        color: var(--color-danger, #b3261e);
        font: inherit;
        font-weight: var(--font-weight-semibold);
      }
    `,
  ],
})
export class HisHopeMobileDeviceBlockedScreenComponent {
  readonly title = input("mobile.deviceBlockedTitle");
  readonly titleFallback = input("Device not allowed");
  readonly message = input("mobile.deviceBlockedMessage");
  readonly messageFallback = input(
    "This device failed the security check. Use a managed device to access administration.",
  );
  readonly reasons = input<readonly HisHopeMobileDeviceBlockReason[]>([]);
  readonly showSignOut = input(true);
  readonly signOutLabel = input("admin.logout");
  readonly signOutLabelFallback = input("Sign out");

  readonly signOut = output<void>();
}
