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
} from "../../core/contracts/admin.contracts";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeFormFieldSchema,
  HisHopeMaterialFormRendererComponent,
} from "@his-hope/frontend-foundation/forms";
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
  imports: [HisHopeEntityDialogComponent, HisHopeMaterialFormRendererComponent],
  template: `<hh-entity-dialog
    [title]="data.role ? 'admin.edit' : 'admin.create'"
    [titleFallback]="data.role ? 'Edit' : 'Create'"
    sectionTitle="admin.workloadRoleDetails"
    sectionTitleFallback="Workload role details"
    sectionDescription="admin.workloadRoleDetailsDescription"
    sectionDescriptionFallback="Configure the workload role identity, access, and trust settings."
    [formGroup]="formGroup"
    [saving]="saving"
    cancelLabel="admin.cancel"
    (save)="save()"
    (cancel)="dialogRef.close()"
  >
    <hh-material-form-renderer [fields]="fields" [form]="formGroup" />
  </hh-entity-dialog>`,
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
  get fields(): HisHopeFormFieldSchema<unknown>[] {
    const sessionRangeMessage = this.i18n.t(
      "admin.workloadRoleSessionRange",
      "Use a value between 300 and 86400 seconds.",
    );
    return [
      {
        key: "key",
        label: this.i18n.t("admin.key", "Key"),
        initialValue: "",
        required: true,
      },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
        initialValue: "",
        required: true,
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
        key: "audience",
        label: this.i18n.t("admin.audience", "Audience"),
        initialValue: "",
        required: true,
      },
      {
        key: "maxSessionSeconds",
        label: this.i18n.t("admin.maxSessionSeconds", "Max session seconds"),
        initialValue: 3600,
        required: true,
        type: "number",
        messages: { min: sessionRangeMessage, max: sessionRangeMessage },
      },
      {
        key: "permissions",
        label: this.i18n.t("admin.permissions", "Permissions"),
        initialValue: [],
        type: "select",
        multiple: true,
        options: this.permissionOptions,
      },
      {
        key: "trustPolicyJson",
        label: this.i18n.t("admin.trustPolicy", "Trust policy"),
        initialValue: "{}",
        multiline: true,
        rows: 5,
        messages: {
          invalidJson: this.i18n.t("admin.invalidJson", "Enter valid JSON."),
        },
      },
    ];
  }
  save(): void {
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
