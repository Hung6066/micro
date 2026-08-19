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
import { IamApiService } from "../../core/services/iam-api.service";
import {
  IamResourcePolicy,
  IamScope,
  IamServiceDefinition,
} from "../../core/contracts/admin.contracts";
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
import { iamScopeOptions } from "../../core/utils/iam-display.util";
export interface ResourcePolicyEditDialogData {
  policy: IamResourcePolicy | null;
  scopes: IamScope[];
  services: IamServiceDefinition[];
}
@Component({
  selector: "app-resource-policy-edit-dialog",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeMaterialFormFieldComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-create-dialog-shell
    [title]="
      (data.policy ? 'admin.editResourcePolicy' : 'admin.createResourcePolicy')
        | hhTranslate
          : (data.policy ? 'Edit resource policy' : 'Create resource policy')
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
            [title]="
              'admin.resourcePolicyDetails'
                | hhTranslate: 'Resource policy details'
            "
            [description]="
              'admin.resourcePolicyDetailsDescription'
                | hhTranslate
                  : 'Choose the scope and service, then define the resource statements.'
            "
            [span]="2"
            ><hh-mat-form-field
              [control]="formGroup.controls.scopeId"
              [label]="'admin.scopeId' | hhTranslate"
              kind="select"
              [options]="scopeOptions" />
            <hh-mat-form-field
              [control]="formGroup.controls.serviceKey"
              [label]="'admin.serviceKey' | hhTranslate"
              kind="select"
              [options]="serviceOptions" />
            <hh-mat-form-field
              [control]="formGroup.controls.resourcePattern"
              [label]="'admin.resourcePattern' | hhTranslate" />
            <hh-mat-form-field
              [control]="formGroup.controls.statementsJson"
              [label]="'admin.statementsJson' | hhTranslate"
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
export class ResourcePolicyEditDialogComponent {
  readonly dialogRef = inject(
    HisHopeDialogRef<ResourcePolicyEditDialogComponent>,
  );
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    scopeId: "",
    serviceKey: "",
    resourcePattern: "",
    statementsJson: '{\n  "statements": []\n}',
  };
  readonly formGroup = new FormGroup({
    scopeId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    serviceKey: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    resourcePattern: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    statementsJson: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
  });
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: ResourcePolicyEditDialogData,
  ) {
    Object.assign(
      this.draft,
      data.policy ?? {
        scopeId: data.scopes.find((scope) => scope.isActive)?.id ?? "",
        serviceKey:
          data.services.find((service) => service.isActive)?.key ?? "",
      },
    );
    this.formGroup.patchValue(this.draft);
  }
  get scopeOptions() {
    return iamScopeOptions(
      this.data.scopes,
      this.formGroup.controls.scopeId.value,
      this.i18n.t("admin.select", "Select"),
    );
  }
  get serviceOptions() {
    return [
      { value: "", label: this.i18n.t("admin.select", "Select") },
      ...this.data.services.map((service) => ({
        value: service.key,
        label: service.key,
      })),
    ];
  }
  save(): void {
    this.formGroup.markAllAsTouched();
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
    if (
      this.saving ||
      !this.draft.scopeId ||
      !this.draft.serviceKey ||
      !this.draft.resourcePattern.trim() ||
      !this.draft.statementsJson.trim()
    )
      return;
    this.saving = true;
    (this.data.policy
      ? this.api.updateIamResourcePolicy(this.data.policy.id, this.draft)
      : this.api.createIamResourcePolicy(this.draft)
    ).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to save resource policy.");
      },
    });
  }
}
