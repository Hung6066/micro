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
import { IamWorkloadRole, User } from "../../core/contracts/admin.contracts";
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
import { iamPrincipalLabel } from "../../core/utils/iam-display.util";
export interface IamRevocationEditDialogData {
  users: User[];
  workloadRoles: IamWorkloadRole[];
}
@Component({
  selector: "app-iam-revocation-edit-dialog",
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
    [title]="'admin.createRevocation' | hhTranslate: 'Create revocation'"
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
              'admin.revocationDetails' | hhTranslate: 'Revocation details'
            "
            [description]="
              'admin.revocationDetailsDescription'
                | hhTranslate
                  : 'Identify the principal and document why access is being revoked.'
            "
            [span]="2"
            ><hh-mat-form-field
              [control]="formGroup.controls.principalId"
              [label]="'admin.principalId' | hhTranslate"
              kind="select"
              [options]="principalOptions"
            />
            <hh-mat-form-field
              [control]="formGroup.controls.principalType"
              [label]="'admin.principalType' | hhTranslate"
              kind="select"
              [options]="principalTypeOptions"
            />
            <hh-mat-form-field
              [control]="formGroup.controls.reason"
              [label]="'admin.reason' | hhTranslate"
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
        icon="link_off"
        [label]="(saving ? 'admin.saving' : 'admin.revoke') | hhTranslate"
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
export class IamRevocationEditDialogComponent {
  readonly dialogRef = inject(
    HisHopeDialogRef<IamRevocationEditDialogComponent>,
  );
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  readonly draft = { principalId: "", principalType: "human", reason: "" };
  readonly principalTypeOptions = [
    { value: "human", label: "Human" },
    { value: "workload", label: "Workload" },
  ];
  constructor(
    @Inject(HIS_HOPE_DIALOG_DATA)
    readonly data: IamRevocationEditDialogData,
  ) {}
  readonly formGroup = new FormGroup({
    principalId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    principalType: new FormControl("human", { nonNullable: true }),
    reason: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
  });
  get principalOptions() {
    const principalType = this.formGroup.controls.principalType.value;
    const options =
      principalType === "human"
        ? this.data.users.map((user) => ({
            value: user.id,
            label: user.email || user.userName,
          }))
        : this.data.workloadRoles.map((role) => ({
            value: role.id,
            label: `${role.displayName} · ${role.key}`,
          }));
    const selected = this.formGroup.controls.principalId.value;
    if (selected && !options.some((option) => option.value === selected)) {
      options.push({ value: selected, label: "Unknown principal" });
    }
    return [
      { value: "", label: this.i18n.t("admin.select", "Select") },
      ...options,
    ];
  }
  save(): void {
    this.formGroup.markAllAsTouched();
    if (this.formGroup.invalid || this.saving) return;
    Object.assign(this.draft, this.formGroup.getRawValue());
    if (
      this.saving ||
      !this.draft.principalId.trim() ||
      !this.draft.reason.trim()
    )
      return;
    this.saving = true;
    this.api.createIamRevocation(this.draft).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => {
        this.saving = false;
        this.i18n.t("admin.iamSaveFailed", "Unable to create revocation.");
      },
    });
  }
}
