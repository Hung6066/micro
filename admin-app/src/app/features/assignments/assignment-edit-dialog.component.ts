import { Component, Inject, inject } from "@angular/core";
import { FormControl, FormGroup, Validators } from "@angular/forms";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeEntityDialogComponent,
} from "@his-hope/frontend-foundation/ui";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  IamGroup,
  IamPermissionSet,
  IamScope,
  IamWorkloadRole,
  User,
} from "../../core/contracts/admin.contracts";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeFormFieldSchema,
  HisHopeMaterialFormRendererComponent,
} from "@his-hope/frontend-foundation/forms";
export interface AssignmentEditDialogData {
  sets: IamPermissionSet[];
  scopes: IamScope[];
  users: User[];
  groups: IamGroup[];
  workloadRoles: IamWorkloadRole[];
}
@Component({
  selector: "app-assignment-edit-dialog",
  standalone: true,
  imports: [HisHopeEntityDialogComponent, HisHopeMaterialFormRendererComponent],
  template: `<hh-entity-dialog
    title="admin.createAssignment"
    titleFallback="Create assignment"
    sectionTitle="admin.assignmentDetails"
    sectionTitleFallback="Assignment details"
    sectionDescription="admin.assignmentDetailsDescription"
    sectionDescriptionFallback="Choose the permission set, principal, and scope for this assignment."
    [formGroup]="formGroup"
    [saving]="saving"
    cancelLabel="admin.cancel"
    (save)="save()"
    (cancel)="dialogRef.close()"
  >
    <hh-material-form-renderer [fields]="fields" [form]="formGroup" />
  </hh-entity-dialog>`,
})
export class AssignmentEditDialogComponent {
  readonly dialogRef = inject(HisHopeDialogRef<AssignmentEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    permissionSetId: "",
    principalType: "human",
    principalId: "",
    scopeId: "",
  };
  readonly formGroup = new FormGroup({
    permissionSetId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    principalType: new FormControl("human", { nonNullable: true }),
    principalId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    scopeId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
  });
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: AssignmentEditDialogData,
  ) {}
  get permissionSetOptions() {
    return [
      { value: "", label: this.i18n.t("admin.select", "Select") },
      ...this.data.sets.map((set) => ({
        value: set.id,
        label: `${set.key} · ${set.displayName}`,
      })),
    ];
  }
  get principalTypeOptions() {
    return [
      { value: "human", label: this.i18n.t("admin.principalHuman", "Human") },
      { value: "group", label: this.i18n.t("admin.principalGroup", "Group") },
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
        : this.formGroup.controls.principalType.value === "group"
          ? this.data.groups.map((group) => ({
              value: group.id,
              label: `${group.displayName} · ${group.key}`,
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
    return [
      { value: "", label: this.i18n.t("admin.select", "Select") },
      ...this.data.scopes.map((scope) => ({
        value: scope.id,
        label: `${scope.displayName} · ${scope.key}`,
      })),
    ];
  }
  get fields(): HisHopeFormFieldSchema<unknown>[] {
    return [
      {
        key: "permissionSetId",
        label: this.i18n.t("admin.permissionSet", "Permission set"),
        initialValue: "",
        required: true,
        type: "select",
        options: this.permissionSetOptions,
      },
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
    ];
  }
  save(): void {
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
    if (
      this.saving ||
      !this.draft.permissionSetId ||
      !this.draft.principalId ||
      !this.draft.scopeId
    )
      return;
    this.saving = true;
    this.api
      .createIamAssignment(this.draft.permissionSetId, {
        principalType: this.draft.principalType,
        principalId: this.draft.principalId,
        scopeId: this.draft.scopeId,
      })
      .subscribe({
        next: () => this.dialogRef.close(true),
        error: () => {
          this.saving = false;
          this.i18n.t("admin.iamSaveFailed", "Unable to create assignment.");
        },
      });
  }
}
