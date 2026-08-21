import { Component, Inject, inject } from "@angular/core";
import { FormControl, FormGroup, Validators } from "@angular/forms";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeEntityDialogComponent,
} from "@his-hope/frontend-foundation/ui";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  IamResourcePolicy,
  IamScope,
  IamServiceDefinition,
} from "../../core/contracts/admin.contracts";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeFormFieldSchema,
  HisHopeMaterialFormRendererComponent,
} from "@his-hope/frontend-foundation/forms";
import { iamScopeOptions } from "../../core/utils/iam-display.util";
export interface ResourcePolicyEditDialogData {
  policy: IamResourcePolicy | null;
  scopes: IamScope[];
  services: IamServiceDefinition[];
}
@Component({
  selector: "app-resource-policy-edit-dialog",
  standalone: true,
  imports: [HisHopeEntityDialogComponent, HisHopeMaterialFormRendererComponent],
  template: `<hh-entity-dialog
    [title]="
      data.policy ? 'admin.editResourcePolicy' : 'admin.createResourcePolicy'
    "
    [titleFallback]="
      data.policy ? 'Edit resource policy' : 'Create resource policy'
    "
    sectionTitle="admin.resourcePolicyDetails"
    sectionTitleFallback="Resource policy details"
    sectionDescription="admin.resourcePolicyDetailsDescription"
    sectionDescriptionFallback="Choose the scope and service, then define the resource statements."
    [formGroup]="formGroup"
    [saving]="saving"
    cancelLabel="admin.cancel"
    (save)="save()"
    (cancel)="dialogRef.close()"
  >
    <hh-material-form-renderer [fields]="fields" [form]="formGroup" />
  </hh-entity-dialog>`,
})
export class ResourcePolicyEditDialogComponent {
  readonly dialogRef = inject(
    HisHopeDialogRef<ResourcePolicyEditDialogComponent>,
  );
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    scopeId: "",
    serviceKey: "",
    resourcePattern: "",
    statementsJson: '{\n  "statements": []\n}',
  };
  readonly formGroup = new FormGroup({
    scopeId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    serviceKey: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    resourcePattern: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    statementsJson: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
  });
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: ResourcePolicyEditDialogData,
  ) {
    Object.assign(
      this.draft,
      data.policy ?? {
        scopeId: data.scopes.find((scope) => scope.isActive)?.id ?? "",
        serviceKey:
          data.services.find((service) => service.isActive)?.key ?? "",
      },
    );
    this.formGroup.patchValue(this.draft);
  }
  get scopeOptions() {
    return iamScopeOptions(
      this.data.scopes,
      this.formGroup.controls.scopeId.value,
      this.i18n.t("admin.select", "Select"),
    );
  }
  get serviceOptions() {
    return [
      { value: "", label: this.i18n.t("admin.select", "Select") },
      ...this.data.services.map((service) => ({
        value: service.key,
        label: service.key,
      })),
    ];
  }
  get fields(): HisHopeFormFieldSchema<unknown>[] {
    return [
      {
        key: "scopeId",
        label: this.i18n.t("admin.scopeId", "Scope"),
        initialValue: "",
        required: true,
        type: "select",
        options: this.scopeOptions,
      },
      {
        key: "serviceKey",
        label: this.i18n.t("admin.serviceKey", "Service"),
        initialValue: "",
        required: true,
        type: "select",
        options: this.serviceOptions,
      },
      {
        key: "resourcePattern",
        label: this.i18n.t("admin.resourcePattern", "Resource pattern"),
        initialValue: "",
        required: true,
      },
      {
        key: "statementsJson",
        label: this.i18n.t("admin.statementsJson", "Statements JSON"),
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
