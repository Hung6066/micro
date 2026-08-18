import { Component, Inject, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import { IamServiceDefinition } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  HisHopeActionButtonComponent,
  HisHopeCreateDialogShellComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
export interface IamServiceEditDialogData {
  service: IamServiceDefinition | null;
}
@Component({
  selector: "app-iam-service-edit-dialog",
  standalone: true,
  imports: [
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-create-dialog-shell
    [title]="
      (data.service ? 'admin.editService' : 'admin.createService')
        | hhTranslate: (data.service ? 'Edit service' : 'Create service')
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
          >{{ "admin.permissionPrefix" | hhTranslate
          }}<input
            name="permissionPrefix"
            [(ngModel)]="draft.permissionPrefix"
            required /></label
        ><label
          >{{ "admin.owner" | hhTranslate
          }}<input name="owner" [(ngModel)]="draft.owner" required
        /></label>
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
      input {
        min-height: 40px;
        padding: 0 12px;
      }
    `,
  ],
})
export class IamServiceEditDialogComponent {
  readonly dialogRef = inject(MatDialogRef<IamServiceEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    key: "",
    displayName: "",
    permissionPrefix: "",
    owner: "identity-service",
  };
  constructor(
    @Inject(MAT_DIALOG_DATA) readonly data: IamServiceEditDialogData,
  ) {
    Object.assign(this.draft, data.service ?? {});
  }
  save(): void {
    if (
      this.saving ||
      Object.values(this.draft).some((value) => !String(value).trim())
    )
      return;
    this.saving = true;
    (this.data.service
      ? this.api.updateIamService(this.data.service.id, this.draft)
      : this.api.createIamService(this.draft)
    ).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to save service.");
      },
    });
  }
}
