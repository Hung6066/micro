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
import { IamApiService } from "../../core/services/iam-api.service";
import {
  IamPermissionSet,
  IamScope,
  PermissionDefinition,
} from "../../core/contracts/admin.contracts";
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
import { iamScopeOptions } from "../../core/utils/iam-display.util";
export interface PermissionSetEditDialogData {
  set: IamPermissionSet | null;
  scopes: IamScope[];
  permissions: PermissionDefinition[];
}
@Component({
  selector: "app-permission-set-edit-dialog",
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
  template: `<hh-create-dialog-shell
    [title]="
      (data.set ? 'admin.editPermissionSet' : 'admin.createPermissionSet')
        | hhTranslate
          : (data.set ? 'Edit permission set' : 'Create permission set')
    "
    ><div hhCreateDialogContent>
      <form [formGroup]="formGroup" class="dialog-form" (ngSubmit)="save()">
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
            [title]="
              'admin.permissionSetDetails'
                | hhTranslate: 'Permission set details'
            "
            [description]="
              'admin.permissionSetDetailsDescription'
                | hhTranslate
                  : 'Set the identity, scope, and permissions for this set.'
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
              [control]="formGroup.controls.scopeId"
              [label]="'admin.scopeId' | hhTranslate"
              kind="select"
              [options]="scopeOptions"
            />
            <hh-mat-form-field
              [control]="formGroup.controls.permissions"
              [label]="'admin.permissions' | hhTranslate"
              kind="select"
              [multiple]="true"
              [options]="permissionOptions"
            /> </hh-form-section
        ></hh-form-layout>
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
      /></div
  ></hh-create-dialog-shell>`,
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
export class PermissionSetEditDialogComponent {
  readonly dialogRef = inject(
    HisHopeDialogRef<PermissionSetEditDialogComponent>,
  );
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = {
    key: "",
    displayName: "",
    scopeId: "",
    permissions: [] as string[],
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
    scopeId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    permissions: new FormControl<string[]>([], {
      nonNullable: true,
      validators: Validators.required,
    }),
  });
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA) readonly data: PermissionSetEditDialogData,
  ) {
    Object.assign(this.draft, {
      key: data.set?.key ?? "",
      displayName: data.set?.displayName ?? "",
      scopeId:
        data.set?.scopeId ??
        data.scopes.find((scope) => scope.isActive)?.id ??
        "",
      permissions: data.set ? JSON.parse(data.set.permissionsJson || "[]") : [],
    });
    this.formGroup.patchValue(this.draft);
  }
  get activePermissions(): PermissionDefinition[] {
    return this.data.permissions.filter(
      (permission) => !permission.isDeprecated,
    );
  }
  get scopeOptions() {
    return iamScopeOptions(
      this.data.scopes,
      this.formGroup.controls.scopeId.value,
      this.i18n.t("admin.select", "Select"),
    );
  }
  get permissionOptions() {
    return this.activePermissions.map((permission) => ({
      value: permission.code,
      label: `${permission.code} · ${permission.name}`,
    }));
  }
  save(): void {
    this.formGroup.markAllAsTouched();
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
    if (
      this.saving ||
      !this.draft.key.trim() ||
      !this.draft.displayName.trim() ||
      !this.draft.scopeId ||
      !this.draft.permissions.length
    )
      return;
    this.saving = true;
    const request = { ...this.draft };
    (this.data.set
      ? this.api.updateIamPermissionSet(this.data.set.id, request)
      : this.api.createIamPermissionSet(request)
    ).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to save permission set.");
      },
    });
  }
}
