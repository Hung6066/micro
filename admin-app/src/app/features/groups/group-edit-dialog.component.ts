import { Component, Inject, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import {
  HisHopeCreateDialogShellComponent,
  HisHopeActionButtonComponent,
} from "@his-hope/frontend-foundation/ui";

import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { IamGroup, IamScope } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";

export interface GroupEditDialogData {
  group: IamGroup | null;
  scopes: IamScope[];
}

@Component({
  selector: "app-group-edit-dialog",
  standalone: true,
  imports: [
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-create-dialog-shell
      [title]="
        (data.group ? 'admin.editGroup' : 'admin.createGroup')
          | hhTranslate: (data.group ? 'Edit group' : 'Create group')
      "
      [subtitle]="
        'admin.groupsSubtitle'
          | hhTranslate: 'Manage groups and their scope assignments'
      "
    >
      <div hhCreateDialogContent>
        <form #groupForm="ngForm" (ngSubmit)="save()" class="group-form">
          <label>
            {{ "admin.key" | hhTranslate: "Key" }}
            <input name="key" [(ngModel)]="draft.key" required />
          </label>
          <label>
            {{ "admin.displayName" | hhTranslate: "Display name" }}
            <input
              name="displayName"
              [(ngModel)]="draft.displayName"
              required
            />
          </label>
          <label>
            {{ "admin.scopeId" | hhTranslate: "Scope" }}
            <select name="scopeId" [(ngModel)]="draft.scopeId" required>
              <option value="">
                {{ "admin.select" | hhTranslate: "Select" }}
              </option>
              @for (scope of data.scopes; track scope.id) {
                <option [value]="scope.id">
                  {{ scope.displayName }} · {{ scope.key }}
                </option>
              }
            </select>
          </label>
        </form>
      </div>
      <div hhCreateDialogFooter>
        <hh-action-button
          kind="secondary"
          icon="close"
          [label]="'common.cancel' | hhTranslate"
          (pressed)="dialogRef.close()"
        />
        <hh-action-button
          kind="primary"
          icon="save"
          [label]="(saving ? 'admin.saving' : 'admin.save') | hhTranslate"
          [disabled]="saving"
          (pressed)="save()"
        />
      </div>
    </hh-create-dialog-shell>
  `,
  styles: [
    `
      .group-form {
        display: grid;
        gap: 16px;
      }
      label {
        display: grid;
        gap: 6px;
        color: var(--text-primary);
        font-weight: 600;
      }
      input,
      select {
        min-height: 40px;
        padding: 0 12px;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-button);
        background: var(--surface-white);
        color: var(--text-primary);
        font: inherit;
      }
    `,
  ],
})
export class GroupEditDialogComponent {
  readonly dialogRef = inject(MatDialogRef<GroupEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  readonly draft: { key: string; displayName: string; scopeId: string };
  saving = false;

  constructor(@Inject(MAT_DIALOG_DATA) readonly data: GroupEditDialogData) {
    this.draft = {
      key: data.group?.key ?? "",
      displayName: data.group?.displayName ?? "",
      scopeId:
        data.group?.scopeId ??
        data.scopes.find((scope) => scope.isActive)?.id ??
        "",
    };
  }

  save(): void {
    if (
      this.saving ||
      !this.draft.key.trim() ||
      !this.draft.displayName.trim() ||
      !this.draft.scopeId
    ) {
      return;
    }

    this.saving = true;
    const request = this.data.group
      ? this.api.updateIamGroup(this.data.group.id, this.draft)
      : this.api.createIamGroup(this.draft);
    request.subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to save group.");
      },
    });
  }
}
