import { Component, Inject, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  IamPermissionSet,
  IamScope,
  PermissionDefinition,
} from "../../core/contracts/admin.contracts";
import {
  HisHopeActionButtonComponent,
  HisHopeCreateDialogShellComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
export interface PermissionSetEditDialogData {
  set: IamPermissionSet | null;
  scopes: IamScope[];
  permissions: PermissionDefinition[];
}
@Component({
  selector: "app-permission-set-edit-dialog",
  standalone: true,
  imports: [
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-create-dialog-shell
    [title]="
      (data.set ? 'admin.editPermissionSet' : 'admin.createPermissionSet')
        | hhTranslate
          : (data.set ? 'Edit permission set' : 'Create permission set')
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
          >{{ "admin.permissions" | hhTranslate
          }}<select
            name="permissions"
            [(ngModel)]="draft.permissions"
            multiple
            size="8"
            required
          >
            <option
              *ngFor="let permission of activePermissions"
              [value]="permission.code"
            >
              {{ permission.code }} · {{ permission.name }}
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
        padding: 8px 12px;
      }
    `,
  ],
})
export class PermissionSetEditDialogComponent {
  readonly dialogRef = inject(MatDialogRef<PermissionSetEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    key: "",
    displayName: "",
    scopeId: "",
    permissions: [] as string[],
  };
  constructor(
    @Inject(MAT_DIALOG_DATA) readonly data: PermissionSetEditDialogData,
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
  }
  get activePermissions(): PermissionDefinition[] {
    return this.data.permissions.filter(
      (permission) => !permission.isDeprecated,
    );
  }
  save(): void {
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
