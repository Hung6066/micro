import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
} from "@angular/core";
import { NgTemplateOutlet } from "@angular/common";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { HisHopeFormValidationSummaryComponent } from "./his-hope-form-validation-summary.component";
import { focusFirstInvalidControl } from "./his-hope-form-focus.util";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeActionButtonComponent } from "./his-hope-action-button.component";
import { HisHopeCreateDialogShellComponent } from "./his-hope-create-dialog-shell.component";
import { HisHopeFormLayoutComponent } from "./his-hope-form-layout.component";
import { HisHopeFormSectionComponent } from "./his-hope-form-section.component";

/**
 * Entity create/edit dialog chrome: titled shell, optional form section,
 * validation banner, and default Cancel/Save footer.
 */
@Component({
  selector: "hh-entity-dialog",
  standalone: true,
  imports: [
    NgTemplateOutlet,
    ReactiveFormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeFormValidationSummaryComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-create-dialog-shell
      [title]="title() | hhTranslate: titleFallback()"
      [subtitle]="subtitle() | hhTranslate: subtitleFallback()"
    >
      <div hhCreateDialogContent>
        <form
          [formGroup]="formGroup()"
          class="hh-entity-dialog__form"
          (ngSubmit)="onSave()"
        >
          <hh-form-validation-summary
            [form]="formGroup()"
            [messageKey]="validationMessageKey()"
            [messageFallback]="validationMessageFallback()"
          />
          @if (sectionTitle()) {
            <hh-form-layout>
              <hh-form-section
                [title]="sectionTitle() | hhTranslate: sectionTitleFallback()"
                [description]="
                  sectionDescription()
                    | hhTranslate: sectionDescriptionFallback()
                "
                [span]="sectionSpan()"
              >
                <ng-container [ngTemplateOutlet]="dialogContent" />
              </hh-form-section>
            </hh-form-layout>
          } @else {
            <ng-container [ngTemplateOutlet]="dialogContent" />
          }
        </form>
      </div>
      <div hhCreateDialogFooter>
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
      </div>
    </hh-create-dialog-shell>
    <ng-template #dialogContent><ng-content /></ng-template>
  `,
  styles: [
    `
      .hh-entity-dialog__form {
        display: grid;
        gap: var(--space-lg);
      }
    `,
  ],
})
export class HisHopeEntityDialogComponent {
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly title = input.required<string>();
  readonly titleFallback = input("");
  readonly subtitle = input("");
  readonly subtitleFallback = input("");
  readonly sectionTitle = input("");
  readonly sectionTitleFallback = input("");
  readonly sectionDescription = input("");
  readonly sectionDescriptionFallback = input("");
  readonly sectionSpan = input<1 | 2>(2);
  readonly formGroup = input.required<FormGroup>();
  readonly saving = input(false);
  readonly saveDisabled = input(false);
  readonly cancelLabel = input("common.cancel");
  readonly cancelLabelFallback = input("");
  readonly saveLabel = input("admin.save");
  readonly saveLabelFallback = input("");
  readonly savingLabel = input("admin.saving");
  readonly savingLabelFallback = input("");
  readonly validationMessageKey = input("admin.validationRequired");
  readonly validationMessageFallback = input("Complete the required fields.");

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
