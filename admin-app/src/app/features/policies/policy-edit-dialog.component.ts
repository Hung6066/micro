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
import { AuthorizationPolicy } from "../../core/contracts/admin.contracts";
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
export interface PolicyEditDialogData {
  policy: AuthorizationPolicy | null;
}
@Component({
  selector: "app-policy-edit-dialog",
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
      (data.policy ? 'admin.editPolicy' : 'admin.createPolicy')
        | hhTranslate: (data.policy ? 'Edit policy' : 'Create policy')
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
            [title]="'admin.policyDetails' | hhTranslate: 'Policy details'"
            [description]="
              'admin.policyDetailsDescription'
                | hhTranslate: 'Set the policy identity, owner, and rules.'
            "
            [span]="2"
            ><hh-mat-form-field
              [control]="formGroup.controls.key"
              [label]="'admin.key' | hhTranslate" />
            <hh-mat-form-field
              [control]="formGroup.controls.description"
              [label]="'admin.description' | hhTranslate" />
            <hh-mat-form-field
              [control]="formGroup.controls.owner"
              [label]="'admin.owner' | hhTranslate" />
            <hh-mat-form-field
              [control]="formGroup.controls.rulesJson"
              [label]="'admin.rulesJson' | hhTranslate"
              [multiline]="true"
              [rows]="8" /></hh-form-section
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
export class PolicyEditDialogComponent {
  readonly dialogRef = inject(HisHopeDialogRef<PolicyEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    key: "",
    description: "",
    owner: "identity-service",
    rulesJson: '{\n  "statements": []\n}',
  };
  readonly formGroup = new FormGroup({
    key: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    description: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    owner: new FormControl("identity-service", {
      nonNullable: true,
      validators: Validators.required,
    }),
    rulesJson: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
  });
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: PolicyEditDialogData,
  ) {
    Object.assign(this.draft, data.policy ?? {});
    this.formGroup.patchValue(this.draft);
    if (data.policy) this.formGroup.controls.key.disable();
  }
  save(): void {
    this.formGroup.markAllAsTouched();
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
    if (
      this.saving ||
      !this.draft.description.trim() ||
      !this.draft.owner.trim() ||
      !this.draft.rulesJson.trim()
    )
      return;
    this.saving = true;
    const call = this.data.policy
      ? this.api.updateAuthorizationPolicy(this.data.policy.id, {
          description: this.draft.description,
          owner: this.draft.owner,
          rulesJson: this.draft.rulesJson,
        })
      : this.api.createAuthorizationPolicy(this.draft);
    call.subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to save policy.");
      },
    });
  }
}
