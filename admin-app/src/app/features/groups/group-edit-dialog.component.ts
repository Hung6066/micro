import { Component, Inject, inject } from "@angular/core";
import { FormControl, FormGroup, Validators } from "@angular/forms";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeEntityDialogComponent,
} from "@his-hope/frontend-foundation/ui";

import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeFormFieldSchema,
  HisHopeMaterialFormRendererComponent,
} from "@his-hope/frontend-foundation/forms";
import { IamGroup, IamScope } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";

export interface GroupEditDialogData {
  group: IamGroup | null;
  scopes: IamScope[];
}

@Component({
  selector: "app-group-edit-dialog",
  standalone: true,
  imports: [HisHopeEntityDialogComponent, HisHopeMaterialFormRendererComponent],
  template: `
    <hh-entity-dialog
      [title]="data.group ? 'admin.editGroup' : 'admin.createGroup'"
      [titleFallback]="data.group ? 'Edit group' : 'Create group'"
      sectionTitle="admin.groupDetails"
      sectionTitleFallback="Group details"
      sectionDescription="admin.groupDetailsDescription"
      sectionDescriptionFallback="Define the group identity and scope assignment."
      [formGroup]="formGroup"
      [saving]="saving"
      (save)="save()"
      (cancel)="dialogRef.close()"
    >
      <hh-material-form-renderer [fields]="fields" [form]="formGroup" />
    </hh-entity-dialog>
  `,
})
export class GroupEditDialogComponent {
  readonly dialogRef = inject(HisHopeDialogRef<GroupEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  readonly draft: { key: string; displayName: string; scopeId: string };
  readonly formGroup = new FormGroup({
    key: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    displayName: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    scopeId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
  });
  saving = false;

  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: GroupEditDialogData,
  ) {
    this.draft = {
      key: data.group?.key ?? "",
      displayName: data.group?.displayName ?? "",
      scopeId:
        data.group?.scopeId ??
        data.scopes.find((scope) => scope.isActive)?.id ??
        "",
    };
    this.formGroup.patchValue(this.draft);
  }

  get scopeOptions() {
    return [
      { value: "", label: this.i18n.t("admin.select", "Select") },
      ...this.data.scopes.map((scope) => ({
        value: scope.id,
        label: `${scope.displayName} · ${scope.key}`,
      })),
    ];
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
        key: "scopeId",
        label: this.i18n.t("admin.scopeId", "Scope"),
        initialValue: "",
        required: true,
        type: "select",
        options: this.scopeOptions,
      },
    ];
  }

  save(): void {
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
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
