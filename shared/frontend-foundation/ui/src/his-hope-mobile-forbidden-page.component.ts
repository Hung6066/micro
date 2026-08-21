import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from "@angular/core";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeMobileIconComponent } from "./his-hope-mobile-icon.component";

/**
 * Full-screen mobile "access denied" page. The hosting app owns navigation, so
 * the back affordance emits `back` instead of binding a route.
 */
@Component({
  selector: "hh-mobile-forbidden-page",
  standalone: true,
  imports: [HisHopeMobileIconComponent, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-mobile-forbidden" role="alert" aria-live="assertive">
      <hh-mobile-icon name="forbidden" size="large" />
      <h1>{{ title() | hhTranslate: titleFallback() }}</h1>
      <p>{{ message() | hhTranslate: messageFallback() }}</p>
      @if (resource()) {
        <p class="hh-mobile-forbidden__resource">
          {{ resourceLabel() | hhTranslate: resourceLabelFallback() }}
          <code>{{ resource() }}</code>
        </p>
      }
      <button
        type="button"
        class="hh-mobile-forbidden__back"
        (click)="back.emit()"
      >
        {{ backLabel() | hhTranslate: backLabelFallback() }}
      </button>
    </section>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-mobile-forbidden {
        display: grid;
        justify-items: center;
        align-content: center;
        gap: var(--space-md);
        min-height: calc(100dvh - var(--mobile-toolbar-height) - env(safe-area-inset-top));
        padding: var(--space-2xl);
        text-align: center;
      }
      .hh-mobile-forbidden hh-mobile-icon {
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
      .hh-mobile-forbidden__resource code {
        font-family: var(--font-family-mono, monospace);
        overflow-wrap: anywhere;
      }
      .hh-mobile-forbidden__back {
        min-height: var(--control-height-touch);
        margin-top: var(--space-md);
        padding: 0 var(--space-xl);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-control);
        background: var(--surface-white);
        color: var(--color-primary);
        font: inherit;
        font-weight: var(--font-weight-semibold);
      }
    `,
  ],
})
export class HisHopeMobileForbiddenPageComponent {
  readonly title = input("mobile.forbiddenTitle");
  readonly titleFallback = input("Access denied");
  readonly message = input("mobile.forbiddenMessage");
  readonly messageFallback = input(
    "Your account does not have the required permission.",
  );
  readonly resource = input("");
  readonly resourceLabel = input("mobile.forbiddenResource");
  readonly resourceLabelFallback = input("Requested resource");
  readonly backLabel = input("common.back");
  readonly backLabelFallback = input("Go back");

  readonly back = output<void>();
}
