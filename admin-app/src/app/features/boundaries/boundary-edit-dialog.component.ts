import { CommonModule } from "@angular/common";
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
  User,
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
export interface BoundaryEditDialogData {
  scopes: IamScope[];
  users: User[];
  workloadRoles: IamWorkloadRole[];
  permissions: PermissionDefinition[];
}
@Component({
  selector: "app-boundary-edit-dialog",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
    HisHopeMaterialFormFieldComponent,
  ],
  template: `<hh-create-dialog-shell
    [title]="'admin.createBoundary' | hhTranslate: 'Create permission boundary'"
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
            [title]="'admin.boundaryDetails' | hhTranslate: 'Boundary details'"
            [description]="
              'admin.boundaryDetailsDescription'
                | hhTranslate
                  : 'Define the principal, scope, permissions, and resource constraints.'
            "
            [span]="2"
            ><hh-mat-form-field
              [control]="formGroup.controls.principalType"
              [label]="'admin.principalType' | hhTranslate"
              kind="select"
              [options]="principalTypeOptions" />
            <hh-mat-form-field
              [control]="formGroup.controls.principalId"
              [label]="'admin.principalId' | hhTranslate"
              kind="select"
              [options]="principalOptions" />
            <hh-mat-form-field
              [control]="formGroup.controls.scopeId"
              [label]="'admin.scopeId' | hhTranslate"
              kind="select"
              [options]="scopeOptions" />
            <hh-mat-form-field
              [control]="formGroup.controls.allowedPermissions"
              [label]="'admin.permissions' | hhTranslate"
              kind="select"
              [multiple]="true"
              [options]="permissionOptions" />
            <hh-mat-form-field
              [control]="formGroup.controls.resourceConstraintsJson"
              [label]="'admin.resourceConstraints' | hhTranslate"
              [multiline]="true"
              [rows]="4" /></hh-form-section
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
export class BoundaryEditDialogComponent {
  readonly dialogRef = inject(HisHopeDialogRef<BoundaryEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    principalType: "human",
    principalId: "",
    scopeId: "",
    allowedPermissions: [] as string[],
    resourceConstraintsJson: "{}",
  };
  readonly formGroup = new FormGroup({
    principalType: new FormControl("human", { nonNullable: true }),
    principalId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    scopeId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    allowedPermissions: new FormControl<string[]>([], {
      nonNullable: true,
      validators: Validators.required,
    }),
    resourceConstraintsJson: new FormControl("{}", {
      nonNullable: true,
      validators: Validators.required,
    }),
  });
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: BoundaryEditDialogData,
  ) {}
  get activePermissions(): PermissionDefinition[] {
    return this.data.permissions.filter(
      (permission) => !permission.isDeprecated,
    );
  }
  get principalTypeOptions() {
    return [
      { value: "human", label: this.i18n.t("admin.principalHuman", "Human") },
      {
        value: "workload",
        label: this.i18n.t("admin.principalWorkload", "Workload"),
      },
    ];
  }
  get principalOptions() {
    const options =
      this.formGroup.controls.principalType.value === "human"
        ? this.data.users.map((user) => ({
            value: user.id,
            label: user.email || user.userName,
          }))
        : this.data.workloadRoles.map((role) => ({
            value: role.id,
            label: `${role.displayName} · ${role.key}`,
          }));
    return [
      { value: "", label: this.i18n.t("admin.select", "Select") },
      ...options,
    ];
  }
  get scopeOptions() {
    return iamScopeOptions(
      this.data.scopes,
      this.formGroup.controls.scopeId.value,
      this.i18n.t("admin.select", "Select"),
    );
  }
  get permissionOptions() {
    return this.activePermissions.map((permission) => ({
      value: permission.code,
      label: `${permission.code} · ${permission.name}`,
    }));
  }
  save(): void {
    this.formGroup.markAllAsTouched();
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
    if (
      this.saving ||
      !this.draft.principalId ||
      !this.draft.scopeId ||
      !this.draft.allowedPermissions.length ||
      !this.draft.resourceConstraintsJson.trim()
    )
      return;
    this.saving = true;
    this.api.createIamBoundary(this.draft).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to create boundary.");
      },
    });
  }
}
