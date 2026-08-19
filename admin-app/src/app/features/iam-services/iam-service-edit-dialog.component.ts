import { Component, Inject, inject } from "@angular/core";
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from "@angular/forms";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
} from "@his-hope/frontend-foundation/ui";
import { IamServiceDefinition } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  HisHopeActionButtonComponent,
  HisHopeCreateDialogShellComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeMaterialFormFieldComponent } from "@his-hope/frontend-foundation/forms";
export interface IamServiceEditDialogData {
  service: IamServiceDefinition | null;
}
@Component({
  selector: "app-iam-service-edit-dialog",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
    HisHopeMaterialFormFieldComponent,
  ],
  template: `<hh-create-dialog-shell
    [title]="
      (data.service ? 'admin.editService' : 'admin.createService')
        | hhTranslate: (data.service ? 'Edit service' : 'Create service')
    "
    ><div hhCreateDialogContent>
      <form [formGroup]="formGroup" class="dialog-form" (ngSubmit)="save()">
        @if (formGroup.invalid && formGroup.touched) {
          <p class="form-error" role="alert">
            {{
              "admin.validationRequired"
                | hhTranslate: "Complete the required fields."
            }}
          </p>
        }
        <hh-form-layout
          ><hh-form-section
            [title]="'admin.serviceDetails' | hhTranslate: 'Service details'"
            [description]="
              'admin.serviceDetailsDescription'
                | hhTranslate
                  : 'Set the service key, display name, permission prefix, and owner.'
            "
            [span]="2"
            ><hh-mat-form-field
              [control]="formGroup.controls.key"
              [label]="'admin.key' | hhTranslate" />
            <hh-mat-form-field
              [control]="formGroup.controls.displayName"
              [label]="'admin.displayName' | hhTranslate" />
            <hh-mat-form-field
              [control]="formGroup.controls.permissionPrefix"
              [label]="'admin.permissionPrefix' | hhTranslate" />
            <hh-mat-form-field
              [control]="formGroup.controls.owner"
              [label]="'admin.owner' | hhTranslate" /></hh-form-section
        ></hh-form-layout>
      </form>
    </div>
    <div hhCreateDialogFooter>
      <hh-action-button
        kind="secondary"
        icon="close"
        [label]="'admin.cancel' | hhTranslate"
        (pressed)="dialogRef.close()"
      /><hh-action-button
        kind="primary"
        icon="save"
        [label]="(saving ? 'admin.saving' : 'admin.save') | hhTranslate"
        [disabled]="saving"
        (pressed)="save()"
      /></div
  ></hh-create-dialog-shell>`,
  styles: [
    `
      .dialog-form {
        display: grid;
        gap: 16px;
      }
      .form-error {
        margin: 0;
        color: var(--text-danger, #b42318);
        font-size: 0.875rem;
      }
    `,
  ],
})
export class IamServiceEditDialogComponent {
  readonly dialogRef = inject(HisHopeDialogRef<IamServiceEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    key: "",
    displayName: "",
    permissionPrefix: "",
    owner: "identity-service",
  };
  readonly formGroup = new FormGroup({
    key: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    displayName: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    permissionPrefix: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    owner: new FormControl("identity-service", {
      nonNullable: true,
      validators: Validators.required,
    }),
  });
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: IamServiceEditDialogData,
  ) {
    Object.assign(this.draft, data.service ?? {});
    this.formGroup.patchValue(this.draft);
  }
  save(): void {
    this.formGroup.markAllAsTouched();
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
    if (
      this.saving ||
      Object.values(this.draft).some((value) => !String(value).trim())
    )
      return;
    this.saving = true;
    (this.data.service
      ? this.api.updateIamService(this.data.service.id, this.draft)
      : this.api.createIamService(this.draft)
    ).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to save service.");
      },
    });
  }
}
