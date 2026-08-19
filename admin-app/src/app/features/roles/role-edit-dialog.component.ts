import {
  ChangeDetectorRef,
  Component,
  Inject,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatExpansionModule } from "@angular/material/expansion";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import {
  PermissionDefinition,
  Role,
  RoleOwnerOption,
} from "../../core/contracts/admin.contracts";
import { RolesApiService } from "../../core/services/roles-api.service";
import {
  HisHopeCreateDialogShellComponent,
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeFormFieldSchema,
  HisHopeFormSchema,
  createHisHopeFormGroup,
  HisHopeMaterialFormFieldComponent,
} from "@his-hope/frontend-foundation/forms";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { catchError, of } from "rxjs";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-role-edit-dialog",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatExpansionModule,
    MatProgressSpinnerModule,
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
    HisHopeMaterialFormFieldComponent,
  ],
  template: `
    <hh-create-dialog-shell
      [title]="(isEdit ? 'admin.editRole' : 'admin.createRole') | hhTranslate"
      [subtitle]="'admin.roleDialogSubtitle' | hhTranslate"
    >
      <div hhCreateDialogContent>
        <form [formGroup]="formGroup" (ngSubmit)="submitForm()">
          <hh-form-layout>
            <hh-form-section
              [title]="'admin.roleDetails' | hhTranslate"
              [description]="'admin.roleDetailsDescription' | hhTranslate"
              [span]="2"
            >
              <hh-mat-form-field
                [control]="formGroup.controls['name']"
                [label]="fields[0].label"
              />
              <hh-mat-form-field
                [control]="formGroup.controls['description']"
                [label]="fields[1].label"
                [multiline]="true"
                [rows]="3"
              />
              <hh-mat-form-field
                [control]="formGroup.controls['riskTier']"
                [label]="fields[2].label"
                kind="select"
                [options]="riskTierOptions"
              />
              <hh-mat-form-field
                [control]="formGroup.controls['owner']"
                [label]="'admin.permissionOwner' | hhTranslate"
                kind="select"
                [options]="ownerOptions"
              />
            </hh-form-section>
            <hh-form-section
              [title]="'admin.rolePermissions' | hhTranslate"
              [description]="'admin.rolePermissionsDescription' | hhTranslate"
              [span]="2"
            >
              <hh-mat-form-field
                [control]="formGroup.controls['permissionSearch']"
                [label]="'admin.rolePermissionSearch' | hhTranslate"
                [placeholder]="
                  'admin.rolePermissionSearchPlaceholder' | hhTranslate
                "
              />
              <div class="permission-summary" aria-live="polite">
                {{
                  "admin.rolePermissionSelected"
                    | hhTranslate: "" : { count: selectedPermissionCodes.size }
                }}
              </div>
              <mat-spinner
                *ngIf="loadingPermissions"
                diameter="28"
                [attr.aria-label]="'admin.loading' | hhTranslate"
              ></mat-spinner>
              <p
                *ngIf="permissionLoadError"
                class="permission-error"
                role="alert"
              >
                {{ "admin.rolePermissionsLoadFailed" | hhTranslate }}
              </p>
              <p
                *ngIf="
                  !loadingPermissions &&
                  !permissionLoadError &&
                  permissionGroups.length === 0
                "
                class="permission-empty"
              >
                {{ "admin.noPermissions" | hhTranslate }}
              </p>
              <mat-accordion
                *ngIf="!loadingPermissions && !permissionLoadError"
                multi
              >
                <mat-expansion-panel
                  *ngFor="let group of filteredGroups"
                  [expanded]="isGroupExpanded(group.name)"
                  (opened)="expandGroup(group.name)"
                  (closed)="collapseGroup(group.name)"
                >
                  <mat-expansion-panel-header>
                    <mat-panel-title>{{ group.name }}</mat-panel-title>
                    <mat-panel-description>
                      <mat-checkbox
                        class="group-toggle"
                        [checked]="isGroupSelected(group)"
                        [indeterminate]="isGroupPartiallySelected(group)"
                        (click)="$event.stopPropagation()"
                        (change)="toggleGroup(group, $event.checked)"
                      >
                        {{ group.selectedCount }}/{{ group.options.length }}
                      </mat-checkbox>
                    </mat-panel-description>
                  </mat-expansion-panel-header>
                  <mat-checkbox
                    [checked]="isGroupSelected(group)"
                    [indeterminate]="isGroupPartiallySelected(group)"
                    (change)="toggleGroup(group, $event.checked)"
                  >
                    {{ "admin.selectAll" | hhTranslate }}
                  </mat-checkbox>
                  <div class="permission-options">
                    <mat-checkbox
                      *ngFor="let permission of group.options"
                      [checked]="selectedPermissionCodes.has(permission.code)"
                      (change)="
                        togglePermission(permission.code, $event.checked)
                      "
                    >
                      <span>{{ permission.name }}</span>
                      <small
                        >{{ permission.code
                        }}<ng-container *ngIf="permission.description">
                          — {{ permission.description }}</ng-container
                        ></small
                      >
                    </mat-checkbox>
                  </div>
                </mat-expansion-panel>
              </mat-accordion>
            </hh-form-section>
          </hh-form-layout>
        </form>
      </div>
      <div hhCreateDialogFooter>
        <hh-action-button
          kind="secondary"
          icon="close"
          [label]="'admin.cancel' | hhTranslate: 'Cancel'"
          (pressed)="dialogRef.close()"
        />
        <hh-action-button
          [disabled]="
            saving ||
            loadingPermissions ||
            permissionLoadError ||
            loadingOwners ||
            ownerLoadError
          "
          (pressed)="save()"
          kind="primary"
          icon="save"
          [label]="(saving ? 'admin.saving' : 'admin.saveRole') | hhTranslate"
        />
      </div>
    </hh-create-dialog-shell>
  `,
  styles: [
    `
      .full-width {
        width: 100%;
      }
      .permission-summary {
        margin: 0 0 12px;
      }
      .permission-options {
        display: grid;
        gap: 8px;
        padding: 8px 0 16px;
      }
      .permission-options mat-checkbox {
        display: block;
      }
      .permission-options small {
        display: block;
        opacity: 0.72;
      }
      .group-toggle {
        margin-right: 8px;
      }
      .permission-error {
        color: var(--hh-color-danger, inherit);
      }
    `,
  ],
})
export class RoleEditDialogComponent implements OnInit {
  readonly dialogRef = inject(HisHopeDialogRef<RoleEditDialogComponent>);
  private readonly api = inject(RolesApiService);
  private readonly toast = inject(HisHopeToastService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  readonly isEdit: boolean;
  readonly formGroup: FormGroup;
  readonly fields: readonly HisHopeFormFieldSchema<unknown>[];
  saving = false;
  loadingPermissions = true;
  permissionLoadError = false;
  loadingOwners = true;
  ownerLoadError = false;
  permissionSearch = "";
  permissions: PermissionDefinition[] = [];
  roleOwners: RoleOwnerOption[] = [];
  selectedPermissionCodes = new Set<string>();
  private readonly collapsedPermissionGroups = new Set<string>();
  form: Partial<Role> = {
    name: "",
    description: "",
    owner: "identity-service",
    riskTier: "standard",
    reviewCadenceDays: 90,
  };
  readonly riskTierOptions = [
    { value: "standard", label: "Standard" },
    { value: "elevated", label: "Elevated" },
    { value: "critical", label: "Critical" },
  ];

  constructor(@Inject(HIS_HOPE_DIALOG_DATA) data: Role | null) {
    this.isEdit = !!data;
    if (data) this.form = { ...data };
    const schema: HisHopeFormSchema<Record<string, unknown>> = {
      fields: {
        name: {
          key: "name",
          label: this.i18n.t("admin.name"),
          initialValue: this.form.name ?? "",
          required: true,
          disabled: this.isEdit,
        },
        description: {
          key: "description",
          label: this.i18n.t("admin.description"),
          initialValue: this.form.description ?? "",
          type: "textarea",
        },
        riskTier: {
          key: "riskTier",
          label: this.i18n.t("admin.permissionRisk"),
          initialValue: this.form.riskTier ?? "",
          required: true,
        },
        owner: {
          key: "owner",
          label: this.i18n.t("admin.permissionOwner"),
          initialValue: this.form.owner ?? "",
          required: true,
        },
        permissionSearch: {
          key: "permissionSearch",
          label: this.i18n.t("admin.rolePermissionSearch"),
          initialValue: "",
        },
      },
    };
    this.fields = Object.values(schema.fields);
    this.formGroup = createHisHopeFormGroup(schema);
  }

  ngOnInit(): void {
    this.api.getPermissions().subscribe({
      next: (permissions) => {
        this.permissions = permissions.filter(
          (permission) => !permission.isDeprecated,
        );
        this.selectedPermissionCodes = new Set(
          this.permissionCodes(this.form.permissions),
        );
        this.loadingPermissions = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loadingPermissions = false;
        this.permissionLoadError = true;
        this.cdr.markForCheck();
      },
    });
    this.api.getRoleOwners().subscribe({
      next: (owners) => {
        this.roleOwners = owners;
        if (!this.form.owner && owners.length > 0)
          this.form.owner = owners[0].key;
        this.formGroup.controls["owner"].setValue(
          this.form.owner ?? owners[0]?.key ?? "",
        );
        this.loadingOwners = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loadingOwners = false;
        this.ownerLoadError = true;
        this.cdr.markForCheck();
      },
    });
  }

  get ownerOptions() {
    return this.roleOwners.map((owner) => ({
      value: owner.key,
      label: owner.name,
    }));
  }

  get permissionGroups(): Array<{
    name: string;
    options: PermissionDefinition[];
    selectedCount: number;
  }> {
    const groups = new Map<string, PermissionDefinition[]>();
    for (const permission of this.permissions) {
      const current = groups.get(permission.group) ?? [];
      current.push(permission);
      groups.set(permission.group, current);
    }
    return [...groups.entries()]
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([name, options]) => ({
        name,
        options: options.sort((left, right) =>
          left.name.localeCompare(right.name),
        ),
        selectedCount: options.filter((permission) =>
          this.selectedPermissionCodes.has(permission.code),
        ).length,
      }));
  }

  get filteredGroups(): Array<{
    name: string;
    options: PermissionDefinition[];
    selectedCount: number;
  }> {
    const query = String(
      this.formGroup.controls["permissionSearch"].value ?? "",
    )
      .trim()
      .toLowerCase();
    if (!query) return this.permissionGroups;
    return this.permissionGroups
      .map((group) => ({
        ...group,
        options: group.options.filter((permission) =>
          [
            permission.code,
            permission.name,
            permission.description,
            permission.group,
          ].some((value) => value?.toLowerCase().includes(query)),
        ),
      }))
      .filter((group) => group.options.length > 0);
  }

  isGroupSelected(group: { options: PermissionDefinition[] }): boolean {
    return (
      group.options.length > 0 &&
      group.options.every((permission) =>
        this.selectedPermissionCodes.has(permission.code),
      )
    );
  }
  isGroupPartiallySelected(group: {
    options: PermissionDefinition[];
  }): boolean {
    return (
      group.options.some((permission) =>
        this.selectedPermissionCodes.has(permission.code),
      ) && !this.isGroupSelected(group)
    );
  }
  toggleGroup(
    group: { options: PermissionDefinition[] },
    checked: boolean,
  ): void {
    for (const permission of group.options)
      checked
        ? this.selectedPermissionCodes.add(permission.code)
        : this.selectedPermissionCodes.delete(permission.code);
  }
  togglePermission(code: string, checked: boolean): void {
    checked
      ? this.selectedPermissionCodes.add(code)
      : this.selectedPermissionCodes.delete(code);
  }
  isGroupExpanded(name: string): boolean {
    return !this.collapsedPermissionGroups.has(name);
  }
  expandGroup(name: string): void {
    this.collapsedPermissionGroups.delete(name);
  }
  collapseGroup(name: string): void {
    this.collapsedPermissionGroups.add(name);
  }

  submitForm(): void {
    this.formGroup.markAllAsTouched();
    if (this.formGroup.valid && !this.saving) {
      this.save(this.formGroup.getRawValue());
    }
  }

  save(values: Record<string, unknown> = this.formGroup.getRawValue()): void {
    this.formGroup.markAllAsTouched();
    if (this.saving) return;
    this.saving = true;
    this.form = {
      ...this.form,
      name: String(values["name"] || this.form.name || ""),
      description: String(values["description"] || this.form.description || ""),
      riskTier: String(values["riskTier"] || this.form.riskTier || ""),
    };
    const payload = {
      name: this.form.name ?? "",
      description: this.form.description,
      owner: String(values["owner"] || this.form.owner || ""),
      riskTier: this.form.riskTier,
      permissions: [...this.selectedPermissionCodes].sort(),
      concurrencyToken: this.form.concurrencyToken,
    };
    const request =
      this.isEdit && this.form.id
        ? this.api.updateRole(this.form.id, payload)
        : this.api.createRole(payload);
    request
      .pipe(
        catchError(() => {
          this.saving = false;
          this.toast.error(
            this.i18n.t("admin.roleSaveFailed", "Unable to save role"),
            { duration: 3000 },
          );
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) {
          this.toast.success(this.i18n.t("admin.roleSaved", "Role saved"), {
            duration: 2000,
          });
          this.dialogRef.close(true);
        }
      });
  }

  private permissionCodes(value: Role["permissions"]): string[] {
    return (value ?? []).map((permission) =>
      typeof permission === "string" ? permission : permission.code,
    );
  }
}
