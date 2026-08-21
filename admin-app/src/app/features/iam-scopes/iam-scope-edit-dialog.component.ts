import { Component, Inject, inject } from "@angular/core";
import { FormControl, FormGroup, Validators } from "@angular/forms";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeEntityDialogComponent,
} from "@his-hope/frontend-foundation/ui";
import { IamScope } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeFormFieldSchema,
  HisHopeMaterialFormRendererComponent,
} from "@his-hope/frontend-foundation/forms";

export interface IamScopeEditDialogData {
  scope: IamScope | null;
  scopes: IamScope[];
}

@Component({
  selector: "app-iam-scope-edit-dialog",
  standalone: true,
  imports: [HisHopeEntityDialogComponent, HisHopeMaterialFormRendererComponent],
  template: `<hh-entity-dialog
    [title]="data.scope ? 'admin.editScope' : 'admin.createScope'"
    [titleFallback]="data.scope ? 'Edit scope' : 'Create scope'"
    sectionTitle="admin.scopeDetails"
    sectionTitleFallback="Scope details"
    sectionDescription="admin.scopeDetailsDescription"
    sectionDescriptionFallback="Define the scope identity and hierarchy."
    [formGroup]="formGroup"
    [saving]="saving"
    cancelLabel="admin.cancel"
    (save)="save()"
    (cancel)="dialogRef.close()"
  >
    <hh-material-form-renderer [fields]="fields" [form]="formGroup" />
  </hh-entity-dialog>`,
})
export class IamScopeEditDialogComponent {
  readonly dialogRef = inject(HisHopeDialogRef<IamScopeEditDialogComponent>);
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    key: "",
    displayName: "",
    kind: "organization",
    parentId: "",
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
    kind: new FormControl("organization", {
      nonNullable: true,
      validators: Validators.required,
    }),
    parentId: new FormControl("", { nonNullable: true }),
  });
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: IamScopeEditDialogData,
  ) {
    Object.assign(this.draft, {
      key: data.scope?.key ?? "",
      displayName: data.scope?.displayName ?? "",
      kind: data.scope?.kind ?? "organization",
      parentId: data.scope?.parentId ?? "",
    });
    this.formGroup.patchValue(this.draft);
  }
  get parentOptions(): IamScope[] {
    return this.data.scopes.filter(
      (scope) => scope.isActive && scope.id !== this.data.scope?.id,
    );
  }
  readonly kindOptions = [
    { value: "organization", label: "Organization" },
    { value: "tenant", label: "Tenant" },
    { value: "account", label: "Account" },
    { value: "environment", label: "Environment" },
  ];
  get parentOptionsList() {
    return [
      { value: "", label: this.i18n.t("admin.select", "Select") },
      ...this.parentOptions.map((scope) => ({
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
        key: "kind",
        label: this.i18n.t("admin.kind", "Kind"),
        initialValue: "organization",
        required: true,
        type: "select",
        options: this.kindOptions,
      },
      {
        key: "parentId",
        label: this.i18n.t("admin.parentScope", "Parent scope"),
        initialValue: "",
        type: "select",
        options: this.parentOptionsList,
        hidden: this.formGroup.controls.kind.value === "organization",
      },
    ];
  }
  save(): void {
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
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
