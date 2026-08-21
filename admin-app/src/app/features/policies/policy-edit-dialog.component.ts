import { Component, Inject, inject } from "@angular/core";
import { FormControl, FormGroup, Validators } from "@angular/forms";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeEntityDialogComponent,
} from "@his-hope/frontend-foundation/ui";
import { AuthorizationPolicy } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeFormFieldSchema,
  HisHopeMaterialFormRendererComponent,
} from "@his-hope/frontend-foundation/forms";
export interface PolicyEditDialogData {
  policy: AuthorizationPolicy | null;
}
@Component({
  selector: "app-policy-edit-dialog",
  standalone: true,
  imports: [HisHopeEntityDialogComponent, HisHopeMaterialFormRendererComponent],
  template: `<hh-entity-dialog
    [title]="data.policy ? 'admin.editPolicy' : 'admin.createPolicy'"
    [titleFallback]="data.policy ? 'Edit policy' : 'Create policy'"
    sectionTitle="admin.policyDetails"
    sectionTitleFallback="Policy details"
    sectionDescription="admin.policyDetailsDescription"
    sectionDescriptionFallback="Set the policy identity, owner, and rules."
    [formGroup]="formGroup"
    [saving]="saving"
    cancelLabel="admin.cancel"
    (save)="save()"
    (cancel)="dialogRef.close()"
  >
    <hh-material-form-renderer [fields]="fields" [form]="formGroup" />
  </hh-entity-dialog>`,
})
export class PolicyEditDialogComponent {
  readonly dialogRef = inject(HisHopeDialogRef<PolicyEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    key: "",
    description: "",
    owner: "identity-service",
    rulesJson: '{\n  "statements": []\n}',
  };
  readonly formGroup = new FormGroup({
    key: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    description: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    owner: new FormControl("identity-service", {
      nonNullable: true,
      validators: Validators.required,
    }),
    rulesJson: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
  });
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: PolicyEditDialogData,
  ) {
    Object.assign(this.draft, data.policy ?? {});
    this.formGroup.patchValue(this.draft);
    if (data.policy) this.formGroup.controls.key.disable();
  }
  get fields(): HisHopeFormFieldSchema<unknown>[] {
    return [
      {
        key: "key",
        label: this.i18n.t("admin.key", "Key"),
        initialValue: "",
        required: true,
      },
      {
        key: "description",
        label: this.i18n.t("admin.description", "Description"),
        initialValue: "",
        required: true,
      },
      {
        key: "owner",
        label: this.i18n.t("admin.owner", "Owner"),
        initialValue: "",
        required: true,
      },
      {
        key: "rulesJson",
        label: this.i18n.t("admin.rulesJson", "Rules JSON"),
        initialValue: "",
        required: true,
        multiline: true,
        rows: 8,
      },
    ];
  }
  save(): void {
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
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
