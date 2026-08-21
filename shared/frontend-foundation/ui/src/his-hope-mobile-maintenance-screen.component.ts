import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from "@angular/core";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeMobileIconComponent } from "./his-hope-mobile-icon.component";

/**
 * Blocking maintenance screen shown while the platform reports a maintenance
 * window. It covers the viewport so no admin action can be attempted.
 */
@Component({
  selector: "hh-mobile-maintenance-screen",
  standalone: true,
  imports: [HisHopeMobileIconComponent, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section
      class="hh-mobile-maintenance"
      role="alertdialog"
      aria-modal="true"
      aria-live="assertive"
      [attr.aria-label]="title() | hhTranslate: titleFallback()"
    >
      <hh-mobile-icon name="offline" size="large" />
      <h1>{{ title() | hhTranslate: titleFallback() }}</h1>
      <p>{{ message() | hhTranslate: messageFallback() }}</p>
      @if (detail()) {
        <p class="hh-mobile-maintenance__detail">{{ detail() }}</p>
      }
      @if (showRetry()) {
        <button
          type="button"
          class="hh-mobile-maintenance__retry"
          [disabled]="retrying()"
          (click)="retry.emit()"
        >
          {{
            (retrying() ? retryingLabel() : retryLabel())
              | hhTranslate
                : (retrying() ? retryingLabelFallback() : retryLabelFallback())
          }}
        </button>
      }
    </section>
  `,
  styles: [
    `
      :host {
        display: contents;
      }
      .hh-mobile-maintenance {
        position: fixed;
        inset: 0;
        z-index: 200;
        display: grid;
        justify-items: center;
        align-content: center;
        gap: var(--space-md);
        padding: var(--space-2xl);
        text-align: center;
        background: var(--bg-warm);
        color: var(--text-primary);
      }
      .hh-mobile-maintenance hh-mobile-icon {
        width: var(--mobile-toolbar-height);
        height: var(--mobile-toolbar-height);
        color: var(--color-primary);
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
      .hh-mobile-maintenance__detail {
        font-size: var(--font-size-label);
      }
      .hh-mobile-maintenance__retry {
        min-height: var(--control-height-touch);
        margin-top: var(--space-md);
        padding: 0 var(--space-xl);
        border: 0;
        border-radius: var(--radius-control);
        background: var(--button-primary-bg);
        color: var(--button-primary-text);
        font: inherit;
        font-weight: var(--font-weight-semibold);
      }
      .hh-mobile-maintenance__retry:disabled {
        opacity: 0.6;
        cursor: wait;
      }
    `,
  ],
})
export class HisHopeMobileMaintenanceScreenComponent {
  readonly title = input("mobile.maintenanceTitle");
  readonly titleFallback = input("Maintenance in progress");
  readonly message = input("mobile.maintenanceMessage");
  readonly messageFallback = input(
    "The identity platform is temporarily unavailable. Please try again shortly.",
  );
  readonly detail = input("");
  readonly showRetry = input(true);
  readonly retrying = input(false);
  readonly retryLabel = input("common.retry");
  readonly retryLabelFallback = input("Try again");
  readonly retryingLabel = input("common.loading");
  readonly retryingLabelFallback = input("Checking…");

  readonly retry = output<void>();
}
