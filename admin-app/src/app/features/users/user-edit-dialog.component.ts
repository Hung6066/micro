import { Component, Inject, inject } from "@angular/core";
import { FormGroup } from "@angular/forms";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import { MatSnackBar } from "@angular/material/snack-bar";
import { User } from "../../core/contracts/admin.contracts";
import { UsersApiService } from "../../core/services/users-api.service";
import {
  HisHopeFormFieldSchema,
  HisHopeFormRendererComponent,
  HisHopeFormSchema,
  createHisHopeFormGroup,
} from "@his-hope/frontend-foundation";
import {
  HisHopeCreateDialogShellComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { catchError, of } from "rxjs";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-user-edit-dialog",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormRendererComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-create-dialog-shell
      [title]="
        (isEdit ? 'admin.editUser' : 'admin.createUser')
          | hhTranslate: (isEdit ? 'Edit user' : 'Create user')
      "
    >
      <div hhCreateDialogContent>
        <hh-form-layout>
          <hh-form-section
            [title]="'admin.basicInformation' | hhTranslate"
            [span]="2"
          >
            <hh-form-renderer
              [fields]="fields"
              [form]="formGroup"
              (submitted)="save($event)"
            />
          </hh-form-section>
        </hh-form-layout>
      </div>
      <div hhCreateDialogFooter>
        <hh-action-button
          (pressed)="dialogRef.close()"
          kind="secondary"
          icon="close"
          [label]="'admin.cancel' | hhTranslate"
        />
        <hh-action-button
          [disabled]="saving || formGroup.invalid"
          (pressed)="submitForm()"
          kind="primary"
          icon="save"
          [label]="
            saving
              ? ('admin.saving' | hhTranslate)
              : ('admin.save' | hhTranslate)
          "
        />
      </div>
    </hh-create-dialog-shell>
  `,
  styles: [
    `
      .hh-button {
        min-height: var(--button-height);
        padding: 0 16px;
        border: 1px solid transparent;
        border-radius: var(--radius-button);
        font: inherit;
        font-weight: var(--button-font-weight);
        cursor: pointer;
      }
      .hh-button--primary {
        color: var(--button-primary-text);
        background: var(--button-primary-bg);
      }
      .hh-button--secondary {
        color: var(--button-secondary-text);
        border-color: var(--button-secondary-border);
        background: var(--button-secondary-bg);
      }
      .hh-button:disabled {
        cursor: not-allowed;
        opacity: 0.65;
      }
    `,
  ],
})
export class UserEditDialogComponent {
  readonly dialogRef = inject(MatDialogRef<UserEditDialogComponent>);
  private readonly api = inject(UsersApiService);
  private readonly snack = inject(MatSnackBar);
  private readonly i18n = inject(HisHopeI18nService);
  readonly isEdit: boolean;
  readonly formGroup: FormGroup;
  readonly fields: readonly HisHopeFormFieldSchema<unknown>[];
  private readonly userId: string;
  private readonly concurrencyToken: string;
  saving = false;

  constructor(@Inject(MAT_DIALOG_DATA) data: User | null) {
    this.isEdit = !!data;
    this.userId = data?.id ?? "";
    this.concurrencyToken = data?.concurrencyToken ?? "";
    const schema: HisHopeFormSchema<Record<string, unknown>> = {
      fields: {
        username: {
          key: "username",
          label: this.i18n.t("admin.username", "Username"),
          initialValue: data?.userName ?? "",
          disabled: this.isEdit,
          required: true,
        },
        email: {
          key: "email",
          label: this.i18n.t("admin.email", "Email"),
          initialValue: data?.email ?? "",
          type: "email",
          required: true,
        },
        firstName: {
          key: "firstName",
          label: this.i18n.t("admin.firstName", "First name"),
          initialValue: data?.firstName ?? "",
          required: true,
        },
        lastName: {
          key: "lastName",
          label: this.i18n.t("admin.lastName", "Last name"),
          initialValue: data?.lastName ?? "",
          required: true,
        },
        phoneNumber: {
          key: "phoneNumber",
          label: this.i18n.t("admin.phoneNumber", "Phone number"),
          initialValue: data?.phoneNumber ?? "",
        },
        password: {
          key: "password",
          label: this.i18n.t("admin.password", "Password"),
          initialValue: "",
          type: "password",
          required: !this.isEdit,
          messages: {
            minlength: this.i18n.t(
              "admin.passwordMinLength",
              "Use at least 12 characters.",
            ),
          },
        },
        role: {
          key: "role",
          label: this.i18n.t("admin.role"),
          initialValue: data?.roles?.[0] ?? "",
        },
      },
    };
    this.fields = Object.values(schema.fields);
    this.formGroup = createHisHopeFormGroup(schema);
    this.formGroup.get("password")?.addValidators((control) => {
      const value = String(control.value ?? "");
      return value.length > 0 && value.length < 12 ? { minlength: true } : null;
    });
    this.formGroup.get("password")?.updateValueAndValidity();
  }

  submitForm(): void {
    this.formGroup.markAllAsTouched();
    if (this.formGroup.valid) this.save(this.formGroup.getRawValue());
  }

  save(values: Record<string, unknown>): void {
    this.saving = true;
    const request = this.isEdit
      ? this.api.updateUser(this.userId, {
          firstName: String(values["firstName"] ?? ""),
          lastName: String(values["lastName"] ?? ""),
          email: String(values["email"] ?? ""),
          phoneNumber: String(values["phoneNumber"] ?? ""),
          role: String(values["role"] ?? ""),
          concurrencyToken: this.concurrencyToken,
        })
      : this.api.createUser({
          username: String(values["username"] ?? ""),
          email: String(values["email"] ?? ""),
          password: String(values["password"] ?? ""),
          firstName: String(values["firstName"] ?? ""),
          lastName: String(values["lastName"] ?? ""),
          phoneNumber: String(values["phoneNumber"] ?? ""),
          role: String(values["role"] ?? ""),
        });
    request
      .pipe(
        catchError(() => {
          this.snack.open(
            this.i18n.t("admin.saveUserFailed", "Failed to save user"),
            this.i18n.t("admin.close", "Close"),
            { duration: 3000 },
          );
          this.saving = false;
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) this.dialogRef.close(true);
      });
  }
}
