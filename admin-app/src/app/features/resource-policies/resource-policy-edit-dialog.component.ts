import { Component, Inject, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  IamResourcePolicy,
  IamScope,
  IamServiceDefinition,
} from "../../core/contracts/admin.contracts";
import {
  HisHopeActionButtonComponent,
  HisHopeCreateDialogShellComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
export interface ResourcePolicyEditDialogData {
  policy: IamResourcePolicy | null;
  scopes: IamScope[];
  services: IamServiceDefinition[];
}
@Component({
  selector: "app-resource-policy-edit-dialog",
  standalone: true,
  imports: [
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-create-dialog-shell
    [title]="
      (data.policy ? 'admin.editResourcePolicy' : 'admin.createResourcePolicy')
        | hhTranslate
          : (data.policy ? 'Edit resource policy' : 'Create resource policy')
    "
    ><div hhCreateDialogContent>
      <form class="dialog-form">
        <label
          >{{ "admin.scopeId" | hhTranslate
          }}<select name="scopeId" [(ngModel)]="draft.scopeId" required>
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <option *ngFor="let scope of data.scopes" [value]="scope.id">
              {{ scope.displayName }} · {{ scope.key }}
            </option>
          </select></label
        ><label
          >{{ "admin.serviceKey" | hhTranslate
          }}<select name="serviceKey" [(ngModel)]="draft.serviceKey" required>
            <option value="">{{ "admin.select" | hhTranslate }}</option>
            <option *ngFor="let service of data.services" [value]="service.key">
              {{ service.key }}
            </option>
          </select></label
        ><label
          >{{ "admin.resourcePattern" | hhTranslate
          }}<input
            name="resourcePattern"
            [(ngModel)]="draft.resourcePattern"
            required /></label
        ><label
          >{{ "admin.statementsJson" | hhTranslate
          }}<textarea
            name="statementsJson"
            rows="8"
            [(ngModel)]="draft.statementsJson"
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
export class ResourcePolicyEditDialogComponent {
  readonly dialogRef = inject(MatDialogRef<ResourcePolicyEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    scopeId: "",
    serviceKey: "",
    resourcePattern: "",
    statementsJson: '{\n  "statements": []\n}',
  };
  constructor(
    @Inject(MAT_DIALOG_DATA) readonly data: ResourcePolicyEditDialogData,
  ) {
    Object.assign(
      this.draft,
      data.policy ?? {
        scopeId: data.scopes.find((scope) => scope.isActive)?.id ?? "",
        serviceKey:
          data.services.find((service) => service.isActive)?.key ?? "",
      },
    );
  }
  save(): void {
    if (
      this.saving ||
      !this.draft.scopeId ||
      !this.draft.serviceKey ||
      !this.draft.resourcePattern.trim() ||
      !this.draft.statementsJson.trim()
    )
      return;
    this.saving = true;
    (this.data.policy
      ? this.api.updateIamResourcePolicy(this.data.policy.id, this.draft)
      : this.api.createIamResourcePolicy(this.draft)
    ).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to save resource policy.");
      },
    });
  }
}
