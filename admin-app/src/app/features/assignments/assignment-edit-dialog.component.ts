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
  IamGroup,
  IamPermissionSet,
  IamScope,
  IamWorkloadRole,
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
    [title]="'admin.createAssignment' | hhTranslate: 'Create assignment'"
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
              'admin.assignmentDetails' | hhTranslate: 'Assignment details'
            "
            [description]="
              'admin.assignmentDetailsDescription'
                | hhTranslate
                  : 'Choose the permission set, principal, and scope for this assignment.'
            "
            [span]="2"
            ><hh-mat-form-field
              [control]="formGroup.controls.permissionSetId"
              [label]="'admin.permissionSet' | hhTranslate"
              kind="select"
              [options]="permissionSetOptions"
            />
            <hh-mat-form-field
              [control]="formGroup.controls.principalType"
              [label]="'admin.principalType' | hhTranslate"
              kind="select"
              [options]="principalTypeOptions"
            />
            <hh-mat-form-field
              [control]="formGroup.controls.principalId"
              [label]="'admin.principalId' | hhTranslate"
              kind="select"
              [options]="principalOptions"
            />
            <hh-mat-form-field
              [control]="formGroup.controls.scopeId"
              [label]="'admin.scopeId' | hhTranslate"
              kind="select"
              [options]="scopeOptions"
            /> </hh-form-section
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
  save(): void {
    this.formGroup.markAllAsTouched();
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
