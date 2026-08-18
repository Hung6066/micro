import { CommonModule } from "@angular/common";
import { Component, Inject, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
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
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
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
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-create-dialog-shell
    [title]="'admin.createAssignment' | hhTranslate: 'Create assignment'"
    ><div hhCreateDialogContent>
      <form class="dialog-form">
        <label
          >{{ "admin.permissionSet" | hhTranslate
          }}<select
            name="permissionSetId"
            [(ngModel)]="draft.permissionSetId"
            required
          >
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <option *ngFor="let set of data.sets" [value]="set.id">
              {{ set.key }} · {{ set.displayName }}
            </option>
          </select></label
        ><label
          >{{ "admin.principalType" | hhTranslate
          }}<select name="principalType" [(ngModel)]="draft.principalType">
            <option value="human">
              {{ "admin.principalHuman" | hhTranslate }}
            </option>
            <option value="group">
              {{ "admin.principalGroup" | hhTranslate }}
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
              ><ng-container *ngSwitchCase="'group'"
                ><option *ngFor="let group of data.groups" [value]="group.id">
                  {{ group.displayName }} · {{ group.key }}
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
        >
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
      select {
        min-height: 40px;
        padding: 0 12px;
      }
    `,
  ],
})
export class AssignmentEditDialogComponent {
  readonly dialogRef = inject(MatDialogRef<AssignmentEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    permissionSetId: "",
    principalType: "human",
    principalId: "",
    scopeId: "",
  };
  constructor(
    @Inject(MAT_DIALOG_DATA) readonly data: AssignmentEditDialogData,
  ) {}
  save(): void {
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
