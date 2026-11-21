import { Component, Inject, inject } from "@angular/core";
import { FormControl, FormGroup, Validators } from "@angular/forms";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeEntityDialogComponent,
} from "@his-hope/frontend-foundation/ui";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  IamPermissionSet,
  IamScope,
  PermissionDefinition,
} from "../../core/contracts/admin.contracts";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeFormFieldSchema,
  HisHopeMaterialFormRendererComponent,
} from "@his-hope/frontend-foundation/forms";
import { iamScopeOptions } from "../../core/utils/iam-display.util";
import { PermissionExplorerComponent } from "./permission-explorer.component";
export interface PermissionSetEditDialogData {
  set: IamPermissionSet | null;
  scopes: IamScope[];
  permissions: PermissionDefinition[];
}
@Component({
  selector: "app-permission-set-edit-dialog",
  standalone: true,
  imports: [
    HisHopeEntityDialogComponent,
    HisHopeMaterialFormRendererComponent,
    PermissionExplorerComponent,
  ],
  template: `<hh-entity-dialog
    [title]="
      data.set ? 'admin.editPermissionSet' : 'admin.createPermissionSet'
    "
    [titleFallback]="
      data.set ? 'Edit permission set' : 'Create permission set'
    "
    sectionTitle="admin.permissionSetDetails"
    sectionTitleFallback="Permission set details"
    sectionDescription="admin.permissionSetDetailsDescription"
    sectionDescriptionFallback="Set the identity, scope, and permissions for this set."
    [formGroup]="formGroup"
    [saving]="saving"
    cancelLabel="admin.cancel"
    (save)="save()"
    (cancel)="dialogRef.close()"
  >
    <hh-material-form-renderer [fields]="fields" [form]="formGroup" />
    <app-permission-explorer
      [permissions]="activePermissions"
      [value]="formGroup.controls.permissions.value"
      (valueChange)="formGroup.controls.permissions.setValue($event)"
    />
  </hh-entity-dialog>`,
})
export class PermissionSetEditDialogComponent {
  readonly dialogRef = inject(
    HisHopeDialogRef<PermissionSetEditDialogComponent>,
  );
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    key: "",
    displayName: "",
    scopeId: "",
    permissions: [] as string[],
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
    permissions: new FormControl<string[]>([], {
      nonNullable: true,
      validators: Validators.required,
    }),
  });
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: PermissionSetEditDialogData,
  ) {
    Object.assign(this.draft, {
      key: data.set?.key ?? "",
      displayName: data.set?.displayName ?? "",
      scopeId:
        data.set?.scopeId ??
        data.scopes.find((scope) => scope.isActive)?.id ??
        "",
      permissions: data.set ? JSON.parse(data.set.permissionsJson || "[]") : [],
    });
    this.formGroup.patchValue(this.draft);
  }
  get activePermissions(): PermissionDefinition[] {
    return this.data.permissions.filter(
      (permission) => !permission.isDeprecated,
    );
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
    ];
  }
  save(): void {
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
    if (
      this.saving ||
      !this.draft.key.trim() ||
      !this.draft.displayName.trim() ||
      !this.draft.scopeId ||
      !this.draft.permissions.length
    )
      return;
    this.saving = true;
    const request = { ...this.draft };
    (this.data.set
      ? this.api.updateIamPermissionSet(this.data.set.id, request)
      : this.api.createIamPermissionSet(request)
    ).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to save permission set.");
      },
    });
  }
}
