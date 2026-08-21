import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
} from "@angular/core";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeActionButtonComponent } from "./his-hope-action-button.component";
import { HisHopeFormValidationSummaryComponent } from "./his-hope-form-validation-summary.component";
import { focusFirstInvalidControl } from "./his-hope-form-focus.util";
import { HisHopeMobileIconComponent } from "./his-hope-mobile-icon.component";

/**
 * Full-screen mobile create/edit page: sticky header with back, scrollable form
 * body, and sticky save/cancel footer.
 */
@Component({
  selector: "hh-mobile-entity-edit-page",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    HisHopeActionButtonComponent,
    HisHopeFormValidationSummaryComponent,
    HisHopeMobileIconComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-mobile-entity-edit" [attr.aria-busy]="saving()">
      <header class="hh-mobile-entity-edit__header">
        <button
          type="button"
          class="hh-mobile-entity-edit__back"
          [attr.aria-label]="backLabel() | hhTranslate: backLabelFallback()"
          (click)="cancel.emit()"
        >
          <hh-mobile-icon name="next" />
        </button>
        <div class="hh-mobile-entity-edit__titles">
          <h1>{{ title() | hhTranslate: titleFallback() }}</h1>
          @if (subtitle()) {
            <p>{{ subtitle() | hhTranslate: subtitleFallback() }}</p>
          }
        </div>
      </header>

      <div class="hh-mobile-entity-edit__body">
        <form
          [formGroup]="formGroup()"
          class="hh-mobile-entity-edit__form"
          (ngSubmit)="onSave()"
        >
          <hh-form-validation-summary
            [form]="formGroup()"
            [messageKey]="validationMessageKey()"
            [messageFallback]="validationMessageFallback()"
          />
          <ng-content />
        </form>
        <ng-content select="[hhMobileEntityEditExtra]" />
      </div>

      <footer class="hh-mobile-entity-edit__footer">
        <hh-action-button
          kind="secondary"
          icon="close"
          [label]="cancelLabel() | hhTranslate: cancelLabelFallback()"
          (pressed)="cancel.emit()"
        />
        <hh-action-button
          kind="primary"
          icon="save"
          [label]="
            (saving() ? savingLabel() : saveLabel())
              | hhTranslate: (saving() ? savingLabelFallback() : saveLabelFallback())
          "
          [disabled]="saving() || saveDisabled()"
          (pressed)="onSave()"
        />
      </footer>
    </section>
  `,
  styles: [
    `
      :host {
        display: block;
        min-height: 100%;
      }
      .hh-mobile-entity-edit {
        display: grid;
        grid-template-rows: auto 1fr auto;
        min-height: calc(100dvh - var(--mobile-toolbar-height) - env(safe-area-inset-top));
        background: var(--bg-warm);
      }
      .hh-mobile-entity-edit__header {
        position: sticky;
        top: 0;
        z-index: 6;
        display: flex;
        align-items: flex-start;
        gap: var(--space-md);
        padding: var(--space-md);
        border-bottom: 1px solid var(--border-default);
        background: color-mix(in srgb, var(--bg-warm) 94%, transparent);
        backdrop-filter: blur(var(--blur-toolbar));
      }
      .hh-mobile-entity-edit__back {
        display: grid;
        place-items: center;
        flex: 0 0 var(--control-height-touch);
        width: var(--control-height-touch);
        height: var(--control-height-touch);
        margin: 0;
        padding: 0;
        border: 0;
        border-radius: var(--radius-control);
        background: var(--surface-white);
        color: var(--text-primary);
        transform: rotate(180deg);
      }
      .hh-mobile-entity-edit__titles {
        min-width: 0;
        flex: 1 1 auto;
      }
      .hh-mobile-entity-edit__titles h1 {
        margin: 0;
        font-size: var(--font-size-section);
        line-height: 1.2;
        letter-spacing: -0.01em;
      }
      .hh-mobile-entity-edit__titles p {
        margin: var(--space-2xs) 0 0;
        color: var(--text-secondary);
        font-size: var(--font-size-label);
        line-height: 1.35;
      }
      .hh-mobile-entity-edit__body {
        overflow-y: auto;
        padding: var(--space-lg) var(--space-md)
          calc(var(--dialog-footer-min-height) + 70px + max(var(--space-inset), env(safe-area-inset-bottom)));
      }
      .hh-mobile-entity-edit__form {
        display: grid;
        gap: var(--space-lg);
      }
      .hh-mobile-entity-edit__footer {
        position: fixed;
        right: 0;
        bottom: calc(70px + max(var(--space-inset), env(safe-area-inset-bottom)));
        left: 0;
        z-index: 12;
        display: flex;
        justify-content: flex-end;
        gap: var(--space-md);
        padding: var(--space-md);
        border-top: 1px solid var(--border-default);
        background: color-mix(in srgb, var(--surface-white) 96%, transparent);
        backdrop-filter: blur(var(--blur-toolbar));
      }
    `,
  ],
})
export class HisHopeMobileEntityEditPageComponent {
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly title = input.required<string>();
  readonly titleFallback = input("");
  readonly subtitle = input("");
  readonly subtitleFallback = input("");
  readonly formGroup = input.required<FormGroup>();
  readonly saving = input(false);
  readonly saveDisabled = input(false);
  readonly backLabel = input("common.back");
  readonly backLabelFallback = input("");
  readonly cancelLabel = input("common.cancel");
  readonly cancelLabelFallback = input("");
  readonly saveLabel = input("admin.save");
  readonly saveLabelFallback = input("");
  readonly savingLabel = input("admin.saving");
  readonly savingLabelFallback = input("");
  readonly validationMessageKey = input("errors.validationFailed");
  readonly validationMessageFallback = input(
    "Validation failed. Please check your input.",
  );

  readonly save = output<void>();
  readonly cancel = output<void>();

  onSave(): void {
    const form = this.formGroup();
    form.markAllAsTouched();
    if (form.invalid || this.saving() || this.saveDisabled()) {
      if (form.invalid) {
        focusFirstInvalidControl(this.host.nativeElement);
      }
      return;
    }
    this.save.emit();
  }
}
