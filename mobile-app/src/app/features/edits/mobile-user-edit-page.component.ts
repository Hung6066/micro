import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnInit,
  inject,
} from "@angular/core";
import { FormGroup } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";
import {
  HisHopeFormFieldSchema,
  HisHopeFormSchema,
  createHisHopeFormGroup,
} from "@his-hope/frontend-foundation/forms";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeMobileEntityEditPageComponent,
  HisHopeMobileSchemaFormComponent,
  HisHopeStateComponent,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import { catchError, of } from "rxjs";
import { User } from "../../core/contracts/mobile.contracts";
import { toMobileSchemaFields } from "../../core/mobile-schema.util";
import { UsersApiService } from "../../core/services/users-api.service";

@Component({
  standalone: true,
  imports: [
    HisHopeMobileEntityEditPageComponent,
    HisHopeMobileSchemaFormComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading) {
      <hh-state kind="loading" [message]="'admin.loading' | hhTranslate" />
    } @else {
      <hh-mobile-entity-edit-page
        [title]="isEdit ? 'admin.editUser' : 'admin.createUser'"
        [titleFallback]="isEdit ? 'Edit user' : 'Create user'"
        [formGroup]="formGroup"
        [saving]="saving"
        (save)="save()"
        (cancel)="goBack()"
      >
        <hh-mobile-schema-form [form]="formGroup" [fields]="schemaFields" />
      </hh-mobile-entity-edit-page>
    }
  `,
})
export class MobileUserEditPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(UsersApiService);
  private readonly toast = inject(HisHopeToastService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);

  isEdit = false;
  loading = false;
  saving = false;
  formGroup!: FormGroup;
  schemaFields = toMobileSchemaFields([]);
  private userId = "";
  private concurrencyToken = "";

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get("id");
    this.isEdit = !!id;
    if (this.isEdit && id) {
      this.loading = true;
      this.api.getUser(id).subscribe({
        next: (user) => {
          this.initForm(user);
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.toast.error(
            this.i18n.t("admin.loadUsersFailed", "Failed to load user."),
          );
          this.goBack();
        },
      });
      return;
    }
    this.initForm(null);
  }

  private initForm(data: User | null): void {
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
          hidden: this.isEdit,
          messages: {
            minlength: this.i18n.t(
              "admin.passwordMinLength",
              "Use at least 12 characters.",
            ),
          },
        },
        role: {
          key: "role",
          label: this.i18n.t("admin.role", "Role"),
          initialValue: data?.roles?.[0] ?? "",
        },
      },
    };
    const fields = Object.values(schema.fields) as HisHopeFormFieldSchema<unknown>[];
    this.schemaFields = toMobileSchemaFields(fields);
    this.formGroup = createHisHopeFormGroup(schema);
    this.formGroup.get("password")?.addValidators((control) => {
      const value = String(control.value ?? "");
      return value.length > 0 && value.length < 12 ? { minlength: true } : null;
    });
    this.formGroup.get("password")?.updateValueAndValidity();
  }

  save(): void {
    if (this.saving || this.formGroup.invalid) return;
    this.saving = true;
    const values = this.formGroup.getRawValue();
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
          this.cdr.markForCheck();
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) {
          this.toast.success(
            this.i18n.t("admin.userSaved", "User saved successfully"),
          );
          this.goBack();
        }
      });
  }

  goBack(): void {
    void this.router.navigateByUrl("/admin/users");
  }
}
