import { Component, Inject, OnInit, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { FormGroup } from "@angular/forms";
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from "@angular/material/dialog";
import { MatButtonModule } from "@angular/material/button";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatExpansionModule } from "@angular/material/expansion";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import {
  PermissionDefinition,
  Role,
  RoleOwnerOption,
} from "../../core/contracts/admin.contracts";
import { RolesApiService } from "../../core/services/roles-api.service";
import {
  HisHopeCreateDialogShellComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeFormFieldSchema,
  HisHopeFormRendererComponent,
  HisHopeFormSchema,
  createHisHopeFormGroup,
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
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatExpansionModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    HisHopeCreateDialogShellComponent,
    HisHopeFormRendererComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-create-dialog-shell
      [title]="(isEdit ? 'admin.editRole' : 'admin.createRole') | hhTranslate"
      [subtitle]="'admin.roleDialogSubtitle' | hhTranslate"
    >
      <div hhCreateDialogContent>
        <hh-form-layout>
          <hh-form-section
            [title]="'admin.roleDetails' | hhTranslate"
            [description]="'admin.roleDetailsDescription' | hhTranslate"
            [span]="2"
          >
            <hh-form-renderer
              [fields]="fields"
              [form]="formGroup"
              (submitted)="save($event)"
            />
            <mat-form-field appearance="outline" class="full-width"
              ><mat-label>{{ "admin.permissionOwner" | hhTranslate }}</mat-label
              ><mat-select
                name="owner"
                [(ngModel)]="form.owner"
                required
                [disabled]="loadingOwners || ownerLoadError"
                ><mat-option
                  *ngFor="let owner of roleOwners"
                  [value]="owner.key"
                  >{{ owner.name }}</mat-option
                ></mat-select
              ></mat-form-field
            >
          </hh-form-section>
          <hh-form-section
            [title]="'admin.rolePermissions' | hhTranslate"
            [description]="'admin.rolePermissionsDescription' | hhTranslate"
            [span]="2"
          >
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>{{
                "admin.rolePermissionSearch" | hhTranslate
              }}</mat-label>
              <input
                matInput
                name="permissionSearch"
                [(ngModel)]="permissionSearch"
                [placeholder]="
                  'admin.rolePermissionSearchPlaceholder' | hhTranslate
                "
              />
            </mat-form-field>
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
                    (change)="togglePermission(permission.code, $event.checked)"
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
            formGroup.invalid ||
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
  readonly dialogRef = inject(MatDialogRef<RoleEditDialogComponent>);
  private readonly api = inject(RolesApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(HisHopeI18nService);
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

  constructor(@Inject(MAT_DIALOG_DATA) data: Role | null) {
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
      },
      error: () => {
        this.loadingPermissions = false;
        this.permissionLoadError = true;
      },
    });
    this.api.getRoleOwners().subscribe({
      next: (owners) => {
        this.roleOwners = owners;
        if (!this.form.owner && owners.length > 0)
          this.form.owner = owners[0].key;
        this.loadingOwners = false;
      },
      error: () => {
        this.loadingOwners = false;
        this.ownerLoadError = true;
      },
    });
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
    const query = this.permissionSearch.trim().toLowerCase();
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

  save(values: Record<string, unknown> = this.formGroup.getRawValue()): void {
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
      owner: this.form.owner,
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
          this.snackBar.open(
            this.i18n.t("admin.roleSaveFailed", "Unable to save role"),
            this.i18n.t("admin.close", "Close"),
            { duration: 3000 },
          );
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) {
          this.snackBar.open(
            this.i18n.t("admin.roleSaved", "Role saved"),
            this.i18n.t("admin.close", "Close"),
            { duration: 2000 },
          );
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
