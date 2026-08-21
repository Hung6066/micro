import { Component, Inject, inject } from "@angular/core";
import { FormControl, FormGroup, Validators } from "@angular/forms";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeEntityDialogComponent,
} from "@his-hope/frontend-foundation/ui";
import { IamServiceDefinition } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeFormFieldSchema,
  HisHopeMaterialFormRendererComponent,
} from "@his-hope/frontend-foundation/forms";
export interface IamServiceEditDialogData {
  service: IamServiceDefinition | null;
}
@Component({
  selector: "app-iam-service-edit-dialog",
  standalone: true,
  imports: [HisHopeEntityDialogComponent, HisHopeMaterialFormRendererComponent],
  template: `<hh-entity-dialog
    [title]="data.service ? 'admin.editService' : 'admin.createService'"
    [titleFallback]="data.service ? 'Edit service' : 'Create service'"
    sectionTitle="admin.serviceDetails"
    sectionTitleFallback="Service details"
    sectionDescription="admin.serviceDetailsDescription"
    sectionDescriptionFallback="Set the service key, display name, permission prefix, and owner."
    [formGroup]="formGroup"
    [saving]="saving"
    cancelLabel="admin.cancel"
    (save)="save()"
    (cancel)="dialogRef.close()"
  >
    <hh-material-form-renderer [fields]="fields" [form]="formGroup" />
  </hh-entity-dialog>`,
})
export class IamServiceEditDialogComponent {
  readonly dialogRef = inject(HisHopeDialogRef<IamServiceEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    key: "",
    displayName: "",
    permissionPrefix: "",
    owner: "identity-service",
  };
  readonly formGroup = new FormGroup({
    key: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    displayName: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    permissionPrefix: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    owner: new FormControl("identity-service", {
      nonNullable: true,
      validators: Validators.required,
    }),
  });
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: IamServiceEditDialogData,
  ) {
    Object.assign(this.draft, data.service ?? {});
    this.formGroup.patchValue(this.draft);
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
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
        initialValue: "",
        required: true,
      },
      {
        key: "permissionPrefix",
        label: this.i18n.t("admin.permissionPrefix", "Permission prefix"),
        initialValue: "",
        required: true,
      },
      {
        key: "owner",
        label: this.i18n.t("admin.owner", "Owner"),
        initialValue: "",
        required: true,
      },
    ];
  }
  save(): void {
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
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
