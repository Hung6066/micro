import {
  ChangeDetectorRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { HisHopeDialogService } from "@his-hope/frontend-foundation/ui";
import { catchError, forkJoin, map, of } from "rxjs";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import {
  HisHopeDataTableColumn,
  HisHopeResourceListPageComponent,
  HisHopeResourceRowActionsDirective,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  IamPermissionBoundary,
  IamScope,
  IamWorkloadRole,
  PermissionDefinition,
  User,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { iamPrincipalLabel } from "../../core/utils/iam-display.util";
import { BoundaryEditDialogComponent } from "./boundary-edit-dialog.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-boundaries-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    HisHopeResourceListPageComponent,
    HisHopeResourceRowActionsDirective,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-resource-list-page
      title="admin.boundaries"
      titleFallback="Permission boundaries"
      subtitle="admin.boundariesSubtitle"
      subtitleFallback="Limit the maximum permissions a principal can receive."
      [count]="boundaries.length"
      [canWrite]="canWrite"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [error]="error"
      (create)="openCreate()"
      (refresh)="load()"
    >
      <ng-template hhResourceRowActions let-row>
        <hh-action-button
          *ngIf="canWrite && row['isActive']"
          (pressed)="toggle(row)"
          kind="danger"
          mode="icon-only"
          icon="toggle_off"
          [label]="'admin.deactivate' | hhTranslate"
        />
      </ng-template>
    </hh-resource-list-page>
  `,
})
export class BoundariesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly dialog = inject(HisHopeDialogService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  boundaries: IamPermissionBoundary[] = [];
  scopes: IamScope[] = [];
  users: User[] = [];
  workloadRoles: IamWorkloadRole[] = [];
  permissionsCatalog: PermissionDefinition[] = [];
  rows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    boundaries: IamPermissionBoundary[];
    scopes: IamScope[];
    users: User[];
    workloadRoles: IamWorkloadRole[];
    permissions: PermissionDefinition[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load boundaries.",
  });
  get loading(): boolean {
    return this.state.loading;
  }
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  constructor() {
    effect(() => {
      const data = this.state.resource.data();
      if (data) {
        this.boundaries = data.boundaries;
        this.scopes = data.scopes;
        this.users = data.users;
        this.workloadRoles = data.workloadRoles;
        this.permissionsCatalog = data.permissions.filter(
          (item) => !item.isDeprecated,
        );
        this.rows = data.boundaries.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      {
        key: "principalType",
        label: this.i18n.t("admin.principalType", "Principal type"),
      },
      {
        key: "principalId",
        label: this.i18n.t("admin.principalId", "Principal"),
        computed: (row) =>
          iamPrincipalLabel(
            String(row["principalId"] ?? ""),
            String(row["principalType"] ?? ""),
            this.users,
            [],
            this.workloadRoles,
          ),
      },
      {
        key: "scopeId",
        label: this.i18n.t("admin.scopeId", "Scope"),
        format: { type: "friendlyReference", references: this.scopes },
      },
      { key: "isActive", label: this.i18n.t("admin.active", "Active") },
      {
        key: "actions",
        label: this.i18n.t("admin.actions", "Actions"),
        sortable: false,
        hideable: false,
      },
    ];
  }
  ngOnInit(): void {
    this.load();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.load());
  }
  openCreate(): void {
    if (this.canWrite)
      this.dialog
        .open(BoundaryEditDialogComponent, {
          width: "680px",
          data: {
            scopes: this.scopes,
            users: this.users,
            workloadRoles: this.workloadRoles,
            permissions: this.permissionsCatalog,
          },
        })
        .afterClosed()
        .subscribe((saved) => {
          if (saved) this.load();
        });
  }
  load(): void {
    this.state.load(
      forkJoin({
        boundaries: this.api.getIamBoundaries(),
        scopes: this.api.getIamScopes(),
        users: this.api.getUsers(),
        workloadRoles: this.api.getIamWorkloadRoles(),
        permissions: this.api.getPermissions(),
      }).pipe(
        map((result) => ({
          ...result,
          scopes: this.tenantContext.filterScopes(result.scopes),
        })),
        catchError(() => {
          this.error = this.i18n.t(
            "admin.iamLoadFailed",
            "Unable to load boundaries.",
          );
          return of({
            boundaries: [],
            scopes: [],
            users: [],
            workloadRoles: [],
            permissions: [],
          });
        }),
      ),
    );
  }

  toggle(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    const call = row["isActive"]
      ? this.api.deactivateIamBoundary(id)
      : this.api.activateIamBoundary(id);
    call.subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to update boundary.",
        )),
    });
  }
}
