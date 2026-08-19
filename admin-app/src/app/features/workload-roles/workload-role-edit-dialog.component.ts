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
  IamScope,
  IamWorkloadRole,
  PermissionDefinition,
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
export interface WorkloadRoleEditDialogData {
  role: IamWorkloadRole | null;
  scopes: IamScope[];
  permissions: PermissionDefinition[];
  servicePrincipal?: boolean;
}
@Component({
  selector: "app-workload-role-edit-dialog",
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
      (data.role ? 'admin.edit' : 'admin.create')
        | hhTranslate: (data.role ? 'Edit' : 'Create')
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
              'admin.workloadRoleDetails' | hhTranslate: 'Workload role details'
            "
            [description]="
              'admin.workloadRoleDetailsDescription'
                | hhTranslate
                  : 'Configure the workload role identity, access, and trust settings.'
            "
            [span]="2"
            ><hh-mat-form-field
              [control]="formGroup.controls.key"
              [label]="'admin.key' | hhTranslate" />
            <hh-mat-form-field
              [control]="formGroup.controls.displayName"
              [label]="'admin.displayName' | hhTranslate" />
            <hh-mat-form-field
              [control]="formGroup.controls.scopeId"
              [label]="'admin.scopeId' | hhTranslate"
              kind="select"
              [options]="scopeOptions" />
            <hh-mat-form-field
              [control]="formGroup.controls.audience"
              [label]="'admin.audience' | hhTranslate" />
            <hh-mat-form-field
              [control]="formGroup.controls.maxSessionSeconds"
              [label]="'admin.maxSessionSeconds' | hhTranslate"
              [messages]="{
                min:
                  ('admin.workloadRoleSessionRange'
                  | hhTranslate: 'Use a value between 300 and 86400 seconds.'),
                max:
                  ('admin.workloadRoleSessionRange'
                  | hhTranslate: 'Use a value between 300 and 86400 seconds.'),
              }" />
            <hh-mat-form-field
              [control]="formGroup.controls.permissions"
              [label]="'admin.permissions' | hhTranslate"
              kind="select"
              [multiple]="true"
              [options]="permissionOptions" />
            <hh-mat-form-field
              [control]="formGroup.controls.trustPolicyJson"
              [label]="'admin.trustPolicy' | hhTranslate"
              [multiline]="true"
              [rows]="5"
              [messages]="{
                invalidJson:
                  ('admin.invalidJson' | hhTranslate: 'Enter valid JSON.'),
              }" /></hh-form-section
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
export class WorkloadRoleEditDialogComponent {
  readonly dialogRef = inject(
    HisHopeDialogRef<WorkloadRoleEditDialogComponent>,
  );
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  trustPolicyJsonError = false;
  readonly draft = {
    key: "",
    displayName: "",
    scopeId: "",
    audience: "",
    maxSessionSeconds: 3600,
    permissions: [] as string[],
    trustPolicyJson: "{}",
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
    scopeId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    audience: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    maxSessionSeconds: new FormControl(3600, {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.min(300),
        Validators.max(86400),
      ],
    }),
    permissions: new FormControl<string[]>([], { nonNullable: true }),
    trustPolicyJson: new FormControl("{}", { nonNullable: true }),
  });
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: WorkloadRoleEditDialogData,
  ) {
    const permissions = data.role
      ? JSON.parse(data.role.permissionsJson || "[]")
      : [];
    Object.assign(
      this.draft,
      data.role
        ? {
            key: data.role.key,
            displayName: data.role.displayName,
            scopeId: data.role.scopeId,
            audience: data.role.audience,
            maxSessionSeconds: data.role.maxSessionSeconds,
            permissions,
            trustPolicyJson: data.role.trustPolicyJson,
          }
        : { scopeId: data.scopes.find((scope) => scope.isActive)?.id ?? "" },
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
  get permissionOptions() {
    return this.data.permissions
      .filter((permission) => !permission.isDeprecated)
      .map((permission) => ({
        value: permission.code,
        label: `${permission.code} · ${permission.name}`,
      }));
  }
  save(): void {
    this.formGroup.markAllAsTouched();
    this.trustPolicyJsonError = false;
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
    if (
      this.saving ||
      !this.draft.key.trim() ||
      !this.draft.displayName.trim() ||
      !this.draft.scopeId ||
      !this.draft.audience.trim()
    )
      return;
    try {
      JSON.parse(this.draft.trustPolicyJson);
    } catch {
      this.trustPolicyJsonError = true;
      return;
    }
    this.saving = true;
    const request = {
      ...this.draft,
      permissions: this.draft.permissions,
    };
    (this.data.role
      ? this.api.updateIamWorkloadRole(this.data.role.id, request)
      : this.api.createIamWorkloadRole(request)
    ).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to save workload role.");
      },
    });
  }
}
