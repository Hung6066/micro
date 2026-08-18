import { Component, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MatDialogRef } from "@angular/material/dialog";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  HisHopeActionButtonComponent,
  HisHopeCreateDialogShellComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
@Component({
  selector: "app-iam-revocation-edit-dialog",
  standalone: true,
  imports: [
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-create-dialog-shell
    [title]="'admin.createRevocation' | hhTranslate: 'Create revocation'"
    ><div hhCreateDialogContent>
      <form class="dialog-form">
        <label
          >{{ "admin.principalId" | hhTranslate
          }}<input
            name="principalId"
            [(ngModel)]="draft.principalId"
            required /></label
        ><label
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
          >{{ "admin.reason" | hhTranslate
          }}<input name="reason" [(ngModel)]="draft.reason" required
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
        icon="link_off"
        [disabled]="saving"
        [label]="(saving ? 'admin.saving' : 'admin.revoke') | hhTranslate"
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
export class IamRevocationEditDialogComponent {
  readonly dialogRef = inject(MatDialogRef<IamRevocationEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = { principalId: "", principalType: "human", reason: "" };
  save(): void {
    if (
      this.saving ||
      !this.draft.principalId.trim() ||
      !this.draft.reason.trim()
    )
      return;
    this.saving = true;
    this.api.createIamRevocation(this.draft).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to create revocation.");
      },
    });
  }
}
