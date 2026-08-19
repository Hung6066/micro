import { Component, Inject, inject } from "@angular/core";
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
import {
  HisHopeCreateDialogShellComponent,
  HisHopeActionButtonComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
} from "@his-hope/frontend-foundation/ui";

import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeMaterialFormFieldComponent } from "@his-hope/frontend-foundation/forms";
import { IamGroup, IamScope } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";

export interface GroupEditDialogData {
  group: IamGroup | null;
  scopes: IamScope[];
}

@Component({
  selector: "app-group-edit-dialog",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
    HisHopeMaterialFormFieldComponent,
  ],
  template: `
    <hh-create-dialog-shell
      [title]="
        (data.group ? 'admin.editGroup' : 'admin.createGroup')
          | hhTranslate: (data.group ? 'Edit group' : 'Create group')
      "
    >
      <div hhCreateDialogContent>
        <form [formGroup]="formGroup" (ngSubmit)="save()" class="group-form">
          @if (formGroup.invalid && formGroup.touched) {
            <p class="form-error" role="alert">
              {{
                "admin.validationRequired"
                  | hhTranslate: "Complete the required fields."
              }}
            </p>
          }
          <hh-form-layout
            ><hh-form-section
              [title]="'admin.groupDetails' | hhTranslate: 'Group details'"
              [description]="
                'admin.groupDetailsDescription'
                  | hhTranslate
                    : 'Define the group identity and scope assignment.'
              "
              [span]="2"
              ><hh-mat-form-field
                [control]="formGroup.controls.key"
                [label]="'admin.key' | hhTranslate: 'Key'" />
              <hh-mat-form-field
                [control]="formGroup.controls.displayName"
                [label]="'admin.displayName' | hhTranslate: 'Display name'" />
              <hh-mat-form-field
                [control]="formGroup.controls.scopeId"
                [label]="'admin.scopeId' | hhTranslate: 'Scope'"
                kind="select"
                [options]="scopeOptions" /></hh-form-section
          ></hh-form-layout>
        </form>
      </div>
      <div hhCreateDialogFooter>
        <hh-action-button
          kind="secondary"
          icon="close"
          [label]="'common.cancel' | hhTranslate"
          (pressed)="dialogRef.close()"
        />
        <hh-action-button
          kind="primary"
          icon="save"
          [label]="(saving ? 'admin.saving' : 'admin.save') | hhTranslate"
          [disabled]="saving"
          (pressed)="save()"
        />
      </div>
    </hh-create-dialog-shell>
  `,
  styles: [
    `
      .group-form {
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

  save(): void {
    this.formGroup.markAllAsTouched();
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
