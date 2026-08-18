import { Component, Inject, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import { AuthorizationPolicy } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  HisHopeActionButtonComponent,
  HisHopeCreateDialogShellComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
export interface PolicyEditDialogData {
  policy: AuthorizationPolicy | null;
}
@Component({
  selector: "app-policy-edit-dialog",
  standalone: true,
  imports: [
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-create-dialog-shell
    [title]="
      (data.policy ? 'admin.editPolicy' : 'admin.createPolicy')
        | hhTranslate: (data.policy ? 'Edit policy' : 'Create policy')
    "
    ><div hhCreateDialogContent>
      <form class="dialog-form">
        <label
          >{{ "admin.key" | hhTranslate
          }}<input
            name="key"
            [(ngModel)]="draft.key"
            [disabled]="!!data.policy"
            required /></label
        ><label
          >{{ "admin.description" | hhTranslate
          }}<input
            name="description"
            [(ngModel)]="draft.description"
            required /></label
        ><label
          >{{ "admin.owner" | hhTranslate
          }}<input name="owner" [(ngModel)]="draft.owner" required /></label
        ><label
          >{{ "admin.rulesJson" | hhTranslate
          }}<textarea
            name="rulesJson"
            rows="8"
            [(ngModel)]="draft.rulesJson"
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
      textarea {
        min-height: 40px;
        padding: 8px 12px;
      }
    `,
  ],
})
export class PolicyEditDialogComponent {
  readonly dialogRef = inject(MatDialogRef<PolicyEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    key: "",
    description: "",
    owner: "identity-service",
    rulesJson: '{\n  "statements": []\n}',
  };
  constructor(@Inject(MAT_DIALOG_DATA) readonly data: PolicyEditDialogData) {
    Object.assign(this.draft, data.policy ?? {});
  }
  save(): void {
    if (
      this.saving ||
      !this.draft.description.trim() ||
      !this.draft.owner.trim() ||
      !this.draft.rulesJson.trim()
    )
      return;
    this.saving = true;
    const call = this.data.policy
      ? this.api.updateAuthorizationPolicy(this.data.policy.id, {
          description: this.draft.description,
          owner: this.draft.owner,
          rulesJson: this.draft.rulesJson,
        })
      : this.api.createAuthorizationPolicy(this.draft);
    call.subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to save policy.");
      },
    });
  }
}
