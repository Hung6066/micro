import { Component, Inject, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import { IamScope } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  HisHopeActionButtonComponent,
  HisHopeCreateDialogShellComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";

export interface IamScopeEditDialogData {
  scope: IamScope | null;
  scopes: IamScope[];
}

@Component({
  selector: "app-iam-scope-edit-dialog",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-create-dialog-shell
    [title]="
      (data.scope ? 'admin.editScope' : 'admin.createScope')
        | hhTranslate: (data.scope ? 'Edit scope' : 'Create scope')
    "
  >
    <div hhCreateDialogContent>
      <form #scopeForm="ngForm" (ngSubmit)="save()" class="dialog-form">
        <label
          >{{ "admin.key" | hhTranslate
          }}<input name="key" [(ngModel)]="draft.key" required
        /></label>
        <label
          >{{ "admin.displayName" | hhTranslate
          }}<input name="displayName" [(ngModel)]="draft.displayName" required
        /></label>
        <label
          >{{ "admin.kind" | hhTranslate
          }}<select name="kind" [(ngModel)]="draft.kind" required>
            <option value="organization">
              {{ "admin.organization" | hhTranslate }}
            </option>
            <option value="tenant">{{ "admin.tenant" | hhTranslate }}</option>
            <option value="account">{{ "admin.account" | hhTranslate }}</option>
            <option value="environment">
              {{ "admin.environment" | hhTranslate }}
            </option>
          </select></label
        >
        <label *ngIf="draft.kind !== 'organization'"
          >{{ "admin.parentScope" | hhTranslate
          }}<select name="parentId" [(ngModel)]="draft.parentId">
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <option *ngFor="let scope of parentOptions" [value]="scope.id">
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
      />
    </div>
  </hh-create-dialog-shell>`,
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
export class IamScopeEditDialogComponent {
  readonly dialogRef = inject(MatDialogRef<IamScopeEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    key: "",
    displayName: "",
    kind: "organization",
    parentId: "",
  };
  constructor(@Inject(MAT_DIALOG_DATA) readonly data: IamScopeEditDialogData) {
    Object.assign(this.draft, {
      key: data.scope?.key ?? "",
      displayName: data.scope?.displayName ?? "",
      kind: data.scope?.kind ?? "organization",
      parentId: data.scope?.parentId ?? "",
    });
  }
  get parentOptions(): IamScope[] {
    return this.data.scopes.filter(
      (scope) => scope.isActive && scope.id !== this.data.scope?.id,
    );
  }
  save(): void {
    if (this.saving || !this.draft.key.trim() || !this.draft.displayName.trim())
      return;
    this.saving = true;
    const request = { ...this.draft, parentId: this.draft.parentId || null };
    (this.data.scope
      ? this.api.updateIamScope(this.data.scope.id, request)
      : this.api.createIamScope(request)
    ).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to save scope.");
      },
    });
  }
}
