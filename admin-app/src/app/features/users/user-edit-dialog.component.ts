import { Component, inject } from "@angular/core";
import { FormGroup } from "@angular/forms";
import { User } from "../../core/contracts/admin.contracts";
import { UsersApiService } from "../../core/services/users-api.service";
import {
  HisHopeFormFieldSchema,
  HisHopeFormSchema,
  createHisHopeFormGroup,
  HisHopeMaterialFormFieldComponent,
} from "@his-hope/frontend-foundation/forms";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeEntityDialogComponent,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { catchError, of } from "rxjs";

@Component({
  selector: "app-user-edit-dialog",
  standalone: true,
  imports: [HisHopeEntityDialogComponent, HisHopeMaterialFormFieldComponent],
  template: `
    <hh-entity-dialog
      [title]="isEdit ? 'admin.editUser' : 'admin.createUser'"
      [titleFallback]="isEdit ? 'Edit user' : 'Create user'"
      sectionTitle="admin.basicInformation"
      sectionTitleFallback="Basic information"
      [sectionSpan]="2"
      [formGroup]="formGroup"
      [saving]="saving"
      (save)="save(formGroup.getRawValue())"
      (cancel)="dialogRef.close()"
    >
      <div class="form-grid">
        <hh-mat-form-field
          [control]="formGroup.controls['username']"
          [label]="fields[0].label"
        />
        <hh-mat-form-field
          [control]="formGroup.controls['email']"
          [label]="fields[1].label"
          type="email"
        />
        <hh-mat-form-field
          [control]="formGroup.controls['firstName']"
          [label]="fields[2].label"
        />
        <hh-mat-form-field
          [control]="formGroup.controls['lastName']"
          [label]="fields[3].label"
        />
        <hh-mat-form-field
          [control]="formGroup.controls['phoneNumber']"
          [label]="fields[4].label"
        />
        <hh-mat-form-field
          [control]="formGroup.controls['password']"
          [label]="fields[5].label"
          type="password"
        />
        <hh-mat-form-field
          [control]="formGroup.controls['role']"
          [label]="fields[6].label"
        />
      </div>
    </hh-entity-dialog>
  `,
  styles: [
    `
      .form-grid {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: var(--space-lg);
      }
      @media (max-width: 720px) {
        .form-grid {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class UserEditDialogComponent {
  readonly dialogRef = inject(HisHopeDialogRef<boolean>);
  private readonly api = inject(UsersApiService);
  private readonly toast = inject(HisHopeToastService);
  private readonly i18n = inject(HisHopeI18nService);
  readonly isEdit: boolean;
  readonly formGroup: FormGroup;
  readonly fields: readonly HisHopeFormFieldSchema<unknown>[];
  private readonly userId: string;
  private readonly concurrencyToken: string;
  saving = false;

  constructor() {
    const data = inject(HIS_HOPE_DIALOG_DATA) as User | null;
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

  save(values: Record<string, unknown>): void {
    if (this.saving || this.formGroup.invalid) return;
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
          this.toast.error(
            this.i18n.t("admin.saveUserFailed", "Failed to save user"),
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
