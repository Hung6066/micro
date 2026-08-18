import { CommonModule } from "@angular/common";
import { Component, Inject, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
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
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
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
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-create-dialog-shell
    [title]="'admin.createBoundary' | hhTranslate: 'Create permission boundary'"
    ><div hhCreateDialogContent>
      <form class="dialog-form">
        <label
          >{{ "admin.principalType" | hhTranslate
          }}<select name="principalType" [(ngModel)]="draft.principalType">
            <option value="human">
              {{ "admin.principalHuman" | hhTranslate }}
            </option>
            <option value="workload">
              {{ "admin.principalWorkload" | hhTranslate }}
            </option>
          </select></label
        ><label
          >{{ "admin.principalId" | hhTranslate
          }}<select name="principalId" [(ngModel)]="draft.principalId" required>
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <ng-container [ngSwitch]="draft.principalType"
              ><ng-container *ngSwitchCase="'human'"
                ><option *ngFor="let user of data.users" [value]="user.id">
                  {{ user.email || user.userName }}
                </option></ng-container
              ><ng-container *ngSwitchCase="'workload'"
                ><option
                  *ngFor="let role of data.workloadRoles"
                  [value]="role.id"
                >
                  {{ role.displayName }} · {{ role.key }}
                </option></ng-container
              ></ng-container
            >
          </select></label
        ><label
          >{{ "admin.scopeId" | hhTranslate
          }}<select name="scopeId" [(ngModel)]="draft.scopeId" required>
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <option *ngFor="let scope of data.scopes" [value]="scope.id">
              {{ scope.displayName }} · {{ scope.key }}
            </option>
          </select></label
        ><label
          >{{ "admin.permissions" | hhTranslate
          }}<select
            name="allowedPermissions"
            [(ngModel)]="draft.allowedPermissions"
            multiple
            size="6"
            required
          >
            <option
              *ngFor="let permission of activePermissions"
              [value]="permission.code"
            >
              {{ permission.code }} · {{ permission.name }}
            </option>
          </select></label
        ><label
          >{{ "admin.resourceConstraints" | hhTranslate
          }}<textarea
            name="resourceConstraintsJson"
            rows="4"
            [(ngModel)]="draft.resourceConstraintsJson"
            required
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
export class BoundaryEditDialogComponent {
  readonly dialogRef = inject(MatDialogRef<BoundaryEditDialogComponent>);
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
  constructor(@Inject(MAT_DIALOG_DATA) readonly data: BoundaryEditDialogData) {}
  get activePermissions(): PermissionDefinition[] {
    return this.data.permissions.filter(
      (permission) => !permission.isDeprecated,
    );
  }
  save(): void {
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
