import { Component, Inject, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  IamScope,
  IamWorkloadRole,
} from "../../core/contracts/admin.contracts";
import {
  HisHopeActionButtonComponent,
  HisHopeCreateDialogShellComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
export interface WorkloadRoleEditDialogData {
  role: IamWorkloadRole | null;
  scopes: IamScope[];
  servicePrincipal?: boolean;
}
@Component({
  selector: "app-workload-role-edit-dialog",
  standalone: true,
  imports: [
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-create-dialog-shell
    [title]="
      (data.role ? 'admin.edit' : 'admin.create')
        | hhTranslate: (data.role ? 'Edit' : 'Create')
    "
    ><div hhCreateDialogContent>
      <form class="dialog-form">
        <label
          >{{ "admin.key" | hhTranslate
          }}<input name="key" [(ngModel)]="draft.key" required /></label
        ><label
          >{{ "admin.displayName" | hhTranslate
          }}<input
            name="displayName"
            [(ngModel)]="draft.displayName"
            required /></label
        ><label
          >{{ "admin.scopeId" | hhTranslate
          }}<select name="scopeId" [(ngModel)]="draft.scopeId" required>
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <option *ngFor="let scope of data.scopes" [value]="scope.id">
              {{ scope.displayName }} · {{ scope.key }}
            </option>
          </select></label
        ><label
          >{{ "admin.audience" | hhTranslate
          }}<input
            name="audience"
            [(ngModel)]="draft.audience"
            required /></label
        ><label
          >{{ "admin.maxSessionSeconds" | hhTranslate
          }}<input
            type="number"
            min="300"
            max="86400"
            name="maxSessionSeconds"
            [(ngModel)]="draft.maxSessionSeconds"
            required /></label
        ><label
          >{{ "admin.permissionsCsv" | hhTranslate
          }}<input name="permissions" [(ngModel)]="draft.permissions" /></label
        ><label
          >{{ "admin.trustPolicy" | hhTranslate
          }}<textarea
            name="trustPolicyJson"
            rows="5"
            [(ngModel)]="draft.trustPolicyJson"
          ></textarea>
        </label>
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
        [disabled]="saving"
        [label]="(saving ? 'admin.saving' : 'admin.save') | hhTranslate"
        (pressed)="save()"
      /></div
  ></hh-create-dialog-shell>`,
  styles: [
    `
      .dialog-form {
        display: grid;
        gap: 16px;
      }
      label {
        display: grid;
        gap: 6px;
      }
      input,
      select,
      textarea {
        min-height: 40px;
        padding: 8px 12px;
      }
    `,
  ],
})
export class WorkloadRoleEditDialogComponent {
  readonly dialogRef = inject(MatDialogRef<WorkloadRoleEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    key: "",
    displayName: "",
    scopeId: "",
    audience: "",
    maxSessionSeconds: 3600,
    permissions: "",
    trustPolicyJson: "{}",
  };
  constructor(
    @Inject(MAT_DIALOG_DATA) readonly data: WorkloadRoleEditDialogData,
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
            permissions: permissions.join(", "),
            trustPolicyJson: data.role.trustPolicyJson,
          }
        : { scopeId: data.scopes.find((scope) => scope.isActive)?.id ?? "" },
    );
  }
  save(): void {
    if (
      this.saving ||
      !this.draft.key.trim() ||
      !this.draft.displayName.trim() ||
      !this.draft.scopeId ||
      !this.draft.audience.trim()
    )
      return;
    this.saving = true;
    const request = {
      ...this.draft,
      permissions: this.draft.permissions
        .split(",")
        .map((value) => value.trim())
        .filter(Boolean),
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
