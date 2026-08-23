import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormGroup } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatExpansionModule } from "@angular/material/expansion";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import {
  HisHopeFormFieldSchema,
  HisHopeFormSchema,
  HisHopeMaterialFormFieldComponent,
  createHisHopeFormGroup,
} from "@his-hope/frontend-foundation/forms";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
  HisHopeMobileEntityEditPageComponent,
  HisHopeMobileSchemaFormComponent,
  HisHopeStateComponent,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import { catchError, of } from "rxjs";
import {
  PermissionDefinition,
  Role,
  RoleOwnerOption,
} from "../../core/contracts/mobile.contracts";
import { RolesApiService } from "../../core/services/roles-api.service";
import { toMobileSchemaFields } from "../../core/mobile-schema.util";

@Component({
  standalone: true,
  imports: [
    CommonModule,
    MatCheckboxModule,
    MatExpansionModule,
    MatProgressSpinnerModule,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeMaterialFormFieldComponent,
    HisHopeMobileEntityEditPageComponent,
    HisHopeMobileSchemaFormComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loadingRole) {
      <hh-state kind="loading" [message]="'admin.loading' | hhTranslate" />
    } @else {
      <hh-mobile-entity-edit-page
        [title]="isEdit ? 'admin.editRole' : 'admin.createRole'"
        [titleFallback]="isEdit ? 'Edit role' : 'Create role'"
        subtitle="admin.roleDialogSubtitle"
        subtitleFallback="Define role identity, owner, and permission assignments."
        [formGroup]="formGroup"
        [saving]="saving"
        [saveDisabled]="
          loadingPermissions || permissionLoadError || loadingOwners || ownerLoadError
        "
        saveLabel="admin.saveRole"
        saveLabelFallback="Save role"
        (save)="save()"
        (cancel)="goBack()"
      >
        <hh-form-layout>
          <hh-form-section
            [title]="'admin.roleDetails' | hhTranslate"
            [description]="'admin.roleDetailsDescription' | hhTranslate"
            [span]="2"
          >
            <hh-mobile-schema-form
              [form]="formGroup"
              [fields]="roleDetailFields"
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
            @if (loadingPermissions) {
              <mat-spinner
                diameter="28"
                [attr.aria-label]="'admin.loading' | hhTranslate"
              />
            }
            @if (permissionLoadError) {
              <p class="permission-error" role="alert">
                {{ "admin.rolePermissionsLoadFailed" | hhTranslate }}
              </p>
            }
            @if (!loadingPermissions && !permissionLoadError) {
              <mat-accordion multi>
                @for (group of filteredGroups; track group.name) {
                  <mat-expansion-panel
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
                      @for (permission of group.options; track permission.code) {
                        <mat-checkbox
                          [checked]="selectedPermissionCodes.has(permission.code)"
                          (change)="togglePermission(permission.code, $event.checked)"
                        >
                          <span>{{ permission.name }}</span>
                          <small>
                            {{ permission.code }}
                            @if (permission.description) {
                              — {{ permission.description }}
                            }
                          </small>
                        </mat-checkbox>
                      }
                    </div>
                  </mat-expansion-panel>
                }
              </mat-accordion>
            }
          </hh-form-section>
        </hh-form-layout>
      </hh-mobile-entity-edit-page>
    }
  `,
  styles: [
    `
      .permission-summary {
        margin: 0 0 var(--space-md);
      }
      .permission-options {
        display: grid;
        gap: var(--space-sm);
        padding: var(--space-sm) 0 var(--space-lg);
      }
      .permission-options mat-checkbox {
        display: block;
      }
      .permission-options small {
        display: block;
        opacity: 0.72;
      }
      .permission-error {
        color: var(--color-danger, #b42318);
      }
    `,
  ],
})
export class MobileRoleEditPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(RolesApiService);
  private readonly toast = inject(HisHopeToastService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);

  isEdit = false;
  loadingRole = false;
  saving = false;
  loadingPermissions = true;
  permissionLoadError = false;
  loadingOwners = true;
  ownerLoadError = false;
  formGroup!: FormGroup;
  fields: readonly HisHopeFormFieldSchema<unknown>[] = [];
  roleDetailFields = toMobileSchemaFields([]);
  permissions: PermissionDefinition[] = [];
  roleOwners: RoleOwnerOption[] = [];
  selectedPermissionCodes = new Set<string>();
  private readonly collapsedPermissionGroups = new Set<string>();
  private form: Partial<Role> = {
    name: "",
    description: "",
    owner: "identity-service",
    riskTier: "standard",
  };
  readonly riskTierOptions = [
    { value: "standard", label: "Standard" },
    { value: "elevated", label: "Elevated" },
    { value: "critical", label: "Critical" },
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get("id");
    this.isEdit = !!id;
    this.initFormSchema();
    this.loadReferenceData();
    if (this.isEdit && id) {
      this.loadingRole = true;
      this.api.getRole(id).subscribe({
        next: (role) => {
          this.form = { ...role };
          this.patchFormFromRole();
          this.selectedPermissionCodes = new Set(
            this.permissionCodes(role.permissions),
          );
          this.loadingRole = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.toast.error(
            this.i18n.t("admin.loadRolesFailed", "Failed to load role."),
          );
          this.goBack();
        },
      });
    }
  }

  private initFormSchema(): void {
    const schema: HisHopeFormSchema<Record<string, unknown>> = {
      fields: {
        name: {
          key: "name",
          label: this.i18n.t("admin.name", "Name"),
          initialValue: this.form.name ?? "",
          required: true,
          disabled: this.isEdit,
        },
        description: {
          key: "description",
          label: this.i18n.t("admin.description", "Description"),
          initialValue: this.form.description ?? "",
          type: "textarea",
        },
        riskTier: {
          key: "riskTier",
          label: this.i18n.t("admin.permissionRisk", "Risk tier"),
          initialValue: this.form.riskTier ?? "standard",
          required: true,
        },
        owner: {
          key: "owner",
          label: this.i18n.t("admin.permissionOwner", "Owner"),
          initialValue: this.form.owner ?? "",
          required: true,
        },
        permissionSearch: {
          key: "permissionSearch",
          label: this.i18n.t("admin.rolePermissionSearch", "Search permissions"),
          initialValue: "",
        },
      },
    };
    this.fields = Object.values(schema.fields);
    this.formGroup = createHisHopeFormGroup(schema);
    this.updateRoleDetailFields();
  }

  private updateRoleDetailFields(): void {
    this.roleDetailFields = toMobileSchemaFields(
      this.fields.filter((field) => field.key !== "permissionSearch"),
      {
        riskTier: this.riskTierOptions,
        owner: this.ownerOptions,
      },
    );
  }

  private patchFormFromRole(): void {
    this.formGroup.patchValue({
      name: this.form.name ?? "",
      description: this.form.description ?? "",
      riskTier: this.form.riskTier ?? "standard",
      owner: this.form.owner ?? "",
    });
    if (this.isEdit) {
      this.formGroup.controls["name"].disable();
    }
  }

  private loadReferenceData(): void {
    this.api.getPermissions().subscribe({
      next: (permissions) => {
        this.permissions = permissions.filter(
          (permission) => !permission.isDeprecated,
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
        if (!this.form.owner && owners.length > 0) {
          this.form.owner = owners[0].key;
          this.formGroup.controls["owner"].setValue(owners[0].key);
        }
        this.loadingOwners = false;
        this.updateRoleDetailFields();
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

  get filteredGroups() {
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

  isGroupPartiallySelected(group: { options: PermissionDefinition[] }): boolean {
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
    for (const permission of group.options) {
      if (checked) {
        this.selectedPermissionCodes.add(permission.code);
      } else {
        this.selectedPermissionCodes.delete(permission.code);
      }
    }
    this.cdr.markForCheck();
  }

  togglePermission(code: string, checked: boolean): void {
    if (checked) {
      this.selectedPermissionCodes.add(code);
    } else {
      this.selectedPermissionCodes.delete(code);
    }
    this.cdr.markForCheck();
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

  save(): void {
    if (this.saving || this.formGroup.invalid) return;
    this.saving = true;
    const values = this.formGroup.getRawValue();
    const payload = {
      name: String(values["name"] || this.form.name || ""),
      description: String(values["description"] || this.form.description || ""),
      owner: String(values["owner"] || this.form.owner || ""),
      riskTier: String(values["riskTier"] || this.form.riskTier || ""),
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
          );
          this.cdr.markForCheck();
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) {
          this.toast.success(this.i18n.t("admin.roleSaved", "Role saved"));
          this.goBack();
        }
      });
  }

  goBack(): void {
    void this.router.navigateByUrl("/admin/roles");
  }

  private permissionCodes(value: Role["permissions"]): string[] {
    return (value ?? []).map((permission) =>
      typeof permission === "string" ? permission : permission.code,
    );
  }
}
