import { Component, Inject, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from "@angular/forms";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
} from "@his-hope/frontend-foundation/ui";
import { IamScope } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import {
  HisHopeActionButtonComponent,
  HisHopeCreateDialogShellComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeMaterialFormFieldComponent } from "@his-hope/frontend-foundation/forms";

export interface IamScopeEditDialogData {
  scope: IamScope | null;
  scopes: IamScope[];
}

@Component({
  selector: "app-iam-scope-edit-dialog",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeMaterialFormFieldComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-create-dialog-shell
    [title]="
      (data.scope ? 'admin.editScope' : 'admin.createScope')
        | hhTranslate: (data.scope ? 'Edit scope' : 'Create scope')
    "
  >
    <div hhCreateDialogContent>
      <form [formGroup]="formGroup" (ngSubmit)="save()" class="dialog-form">
        @if (formGroup.invalid && formGroup.touched) {
          <p class="form-error" role="alert">
            {{
              "admin.validationRequired"
                | hhTranslate: "Complete the required fields."
            }}
          </p>
        }
        <hh-form-layout>
          <hh-form-section
            [title]="'admin.scopeDetails' | hhTranslate: 'Scope details'"
            [description]="
              'admin.scopeDetailsDescription'
                | hhTranslate: 'Define the scope identity and hierarchy.'
            "
            [span]="2"
            ><hh-mat-form-field
              [control]="formGroup.controls.key"
              [label]="'admin.key' | hhTranslate"
            />
            <hh-mat-form-field
              [control]="formGroup.controls.displayName"
              [label]="'admin.displayName' | hhTranslate"
            />
            <hh-mat-form-field
              [control]="formGroup.controls.kind"
              [label]="'admin.kind' | hhTranslate"
              kind="select"
              [options]="kindOptions"
            />
            <hh-mat-form-field
              [control]="formGroup.controls.parentId"
              [label]="'admin.parentScope' | hhTranslate"
              *ngIf="draft.kind !== 'organization'"
              kind="select"
              [options]="parentOptionsList"
            />
          </hh-form-section>
        </hh-form-layout>
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
        [label]="(saving ? 'admin.saving' : 'admin.save') | hhTranslate"
        [disabled]="saving"
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
      .form-error {
        margin: 0;
        color: var(--text-danger, #b42318);
        font-size: 0.875rem;
      }
    `,
  ],
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
  save(): void {
    this.formGroup.markAllAsTouched();
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
