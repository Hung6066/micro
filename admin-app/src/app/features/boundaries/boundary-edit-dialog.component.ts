import { Component, Inject, inject } from "@angular/core";
import { FormControl, FormGroup, Validators } from "@angular/forms";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeEntityDialogComponent,
} from "@his-hope/frontend-foundation/ui";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  IamScope,
  IamWorkloadRole,
  PermissionDefinition,
  User,
} from "../../core/contracts/admin.contracts";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeFormFieldSchema,
  HisHopeMaterialFormRendererComponent,
} from "@his-hope/frontend-foundation/forms";
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
  imports: [HisHopeEntityDialogComponent, HisHopeMaterialFormRendererComponent],
  template: `<hh-entity-dialog
    title="admin.createBoundary"
    titleFallback="Create permission boundary"
    sectionTitle="admin.boundaryDetails"
    sectionTitleFallback="Boundary details"
    sectionDescription="admin.boundaryDetailsDescription"
    sectionDescriptionFallback="Define the principal, scope, permissions, and resource constraints."
    [formGroup]="formGroup"
    [saving]="saving"
    cancelLabel="admin.cancel"
    (save)="save()"
    (cancel)="dialogRef.close()"
  >
    <hh-material-form-renderer [fields]="fields" [form]="formGroup" />
  </hh-entity-dialog>`,
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
  get fields(): HisHopeFormFieldSchema<unknown>[] {
    return [
      {
        key: "principalType",
        label: this.i18n.t("admin.principalType", "Principal type"),
        initialValue: "human",
        type: "select",
        options: this.principalTypeOptions,
      },
      {
        key: "principalId",
        label: this.i18n.t("admin.principalId", "Principal"),
        initialValue: "",
        required: true,
        type: "select",
        options: this.principalOptions,
      },
      {
        key: "scopeId",
        label: this.i18n.t("admin.scopeId", "Scope"),
        initialValue: "",
        required: true,
        type: "select",
        options: this.scopeOptions,
      },
      {
        key: "allowedPermissions",
        label: this.i18n.t("admin.permissions", "Permissions"),
        initialValue: [],
        required: true,
        type: "select",
        multiple: true,
        options: this.permissionOptions,
      },
      {
        key: "resourceConstraintsJson",
        label: this.i18n.t("admin.resourceConstraints", "Resource constraints"),
        initialValue: "{}",
        required: true,
        multiline: true,
        rows: 4,
      },
    ];
  }
  save(): void {
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
