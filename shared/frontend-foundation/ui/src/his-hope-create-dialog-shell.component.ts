import { ChangeDetectionStrategy, Component, input } from "@angular/core";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({
  selector: "hh-create-dialog-shell",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hh-create-dialog-shell">
      <header class="hh-create-dialog-shell__header">
        <h2 class="hh-create-dialog-shell__title">
          {{ title() | hhTranslate }}
        </h2>
        @if (subtitle()) {
          <p class="hh-create-dialog-shell__subtitle">
            {{ subtitle() | hhTranslate }}
          </p>
        }
      </header>
      <section
        class="hh-create-dialog-shell__content"
        [attr.aria-label]="'common.formContent' | hhTranslate"
      >
        <ng-content select="[hhCreateDialogContent]" />
      </section>
      <footer class="hh-create-dialog-shell__footer">
        <div class="hh-create-dialog-shell__actions">
          <ng-content select="[hhCreateDialogFooter]" />
        </div>
      </footer>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
        width: 100%;
        min-width: 0;
      }
      .hh-create-dialog-shell {
        display: flex;
        flex-direction: column;
        width: 100%;
        max-width: var(--form-dialog-max-width);
        min-width: 0;
        max-height: min(80vh, 760px);
        color: var(--text-primary);
        background: var(--surface-white);
        border-radius: inherit;
      }
      .hh-create-dialog-shell__header {
        flex: 0 0 auto;
        padding: var(--form-dialog-padding-block)
          var(--form-dialog-padding-inline) var(--space-sm);
        border-bottom: 1px solid var(--border-default);
        background: color-mix(
          in srgb,
          var(--surface-muted, #fafafa) 40%,
          var(--surface-white)
        );
      }
      .hh-create-dialog-shell__title {
        margin: 0;
        font-size: var(--font-size-title);
        line-height: var(--leading-tight);
        font-weight: var(--font-weight-semibold);
      }
      .hh-create-dialog-shell__subtitle {
        margin: var(--space-sm) 0 0;
        color: var(--text-secondary);
        font-size: var(--font-size-body);
        line-height: var(--leading-body);
      }
      .hh-create-dialog-shell__content {
        min-height: 0;
        overflow-y: auto;
        padding: var(--space-md) var(--form-dialog-padding-inline) var(--space-2xl);
      }
      .hh-create-dialog-shell__footer {
        display: flex;
        flex: 0 0 auto;
        flex-wrap: nowrap;
        align-items: center;
        justify-content: flex-end;
        gap: var(--space-md);
        min-height: var(--shell-header-height);
        padding: var(--space-md) var(--form-dialog-padding-inline);
        border-top: 1px solid var(--border-default);
        background: var(--surface-white);
      }
      .hh-create-dialog-shell__actions {
        display: flex;
        flex: 0 0 auto;
        flex-wrap: nowrap;
        align-items: center;
        justify-content: flex-end;
        gap: var(--space-md);
        min-width: 0;
      }
      .hh-create-dialog-shell__actions ::ng-deep [hhCreateDialogFooter] {
        display: flex;
        flex: 0 0 auto;
        flex-wrap: nowrap;
        align-items: center;
        gap: var(--space-md);
      }
      .hh-create-dialog-shell__actions ::ng-deep [hhCreateDialogFooter] > * {
        flex: 0 0 auto;
        white-space: nowrap;
      }
      @media (max-width: 720px) {
        .hh-create-dialog-shell {
          max-height: 100dvh;
        }
        .hh-create-dialog-shell__header {
          padding: var(--space-xl) var(--space-lg) var(--space-sm);
        }
        .hh-create-dialog-shell__content {
          padding-inline: var(--space-lg);
        }
        .hh-create-dialog-shell__footer {
          padding-inline: var(--space-lg);
        }
      }
    `,
  ],
})
export class HisHopeCreateDialogShellComponent {
  readonly title = input.required<string>();
  readonly subtitle = input("");
}
