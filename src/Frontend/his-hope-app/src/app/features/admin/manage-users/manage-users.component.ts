import {
  Component,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  inject,
} from "@angular/core";
import { FormControl, ReactiveFormsModule } from "@angular/forms";
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from "rxjs";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatSelectModule } from "@angular/material/select";
import { MatMenuModule } from "@angular/material/menu";
import { MatTooltipModule } from "@angular/material/tooltip";
import { CommonModule } from "@angular/common";
import { AdminService } from "@core/services/admin.service";
import { AdminUser } from "@core/models/admin.model";
import { PagedResult } from "@core/models/paged-result.model";
import { UserFormDialogComponent, UserFormData } from "./user-form.dialog";
import {
  AssignRolesDialogComponent,
  AssignRolesData,
} from "./assign-roles.dialog";
import { HisHopePageQuery } from "@his-hope/frontend-foundation";
import {
  HisHopeTranslatePipe,
  HisHopeI18nService,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeConfirmDialogComponent,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeDataTableCellDirective,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
} from "@his-hope/frontend-foundation/ui";

@Component({
  selector: "app-manage-users",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    MatMenuModule,
    MatTooltipModule,
    MatDialogModule,
    MatSnackBarModule,
    HisHopeDataTableComponent,
    HisHopeDataTableCellDirective,
    HisHopeConfirmDialogComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./manage-users.component.html",
  styleUrls: ["./manage-users.component.scss"],
})
export class ManageUsersComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  users: AdminUser[] = [];
  totalCount = 0;
  loading = true;
  error: string | null = null;
  query: HisHopePageQuery = { page: 1, pageSize: 20 };

  roleFilter = new FormControl("");

  readonly columns: HisHopeDataTableColumn[] = [
    {
      key: "id",
      label: this.i18n.t("manageUsers.column.id", "ID"),
      computed: (row) => String(row["id"]).slice(0, 12),
    },
    {
      key: "fullName",
      label: this.i18n.t("manageUsers.column.fullName", "Họ và tên"),
    },
    { key: "email", label: this.i18n.t("manageUsers.column.email", "Email") },
    { key: "roles", label: this.i18n.t("manageUsers.column.roles", "Vai trò") },
    {
      key: "status",
      label: this.i18n.t("manageUsers.column.status", "Trạng thái"),
    },
    {
      key: "actions",
      label: this.i18n.t("manageUsers.column.actions", "Thao tác"),
    },
  ];
  rows: Record<string, unknown>[] = [];
  readonly roleFilters = [
    { value: "", label: this.i18n.t("manageUsers.roleAll", "Tất cả vai trò") },
    {
      value: "admin",
      label: this.i18n.t("manageUsers.role.admin", "Quản trị viên"),
    },
    {
      value: "doctor",
      label: this.i18n.t("manageUsers.role.doctor", "Bác sĩ"),
    },
    {
      value: "nurse",
      label: this.i18n.t("manageUsers.role.nurse", "Điều dưỡng"),
    },
    {
      value: "pharmacist",
      label: this.i18n.t("manageUsers.role.pharmacist", "Dược sĩ"),
    },
    {
      value: "receptionist",
      label: this.i18n.t("manageUsers.role.receptionist", "Lễ tân"),
    },
    {
      value: "manager",
      label: this.i18n.t("manageUsers.role.manager", "Quản lý"),
    },
  ];

  // Confirm dialog state (replaces MatDialog-based ConfirmDialogComponent)
  showConfirmDialog = false;
  confirmDialogTitle = "";
  confirmDialogMessage = "";
  confirmDialogConfirmLabel = "";
  private pendingUserAction: { user: AdminUser; activate: boolean } | null =
    null;

  constructor(
    private adminService: AdminService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadUsers();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadUsers(): void {
    this.loading = true;
    this.error = null;
    this.cdr.markForCheck();

    this.adminService
      .getUsers({
        search: this.query.search || "",
        role: this.roleFilter.value || "",
        page: this.query.page,
        pageSize: this.query.pageSize,
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: PagedResult<AdminUser>) => {
          this.users = result.items;
          this.totalCount = result.totalCount;
          this.rows = result.items.map((u) => ({
            id: u.id,
            fullName: u.fullName,
            email: u.email,
            roles: u.roles,
            isActive: u.isActive,
            entity: u,
          }));
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.error = this.i18n.t(
            "manageUsers.loadFailed",
            "Không thể tải danh sách người dùng",
          );
          this.snackBar.open(this.error, this.i18n.t("common.close", "Đóng"), {
            duration: 5000,
          });
          this.cdr.markForCheck();
        },
      });
  }

  onQueryChange(query: HisHopePageQuery): void {
    this.query = query;
    this.loadUsers();
  }

  onFilterChange(): void {
    this.query = { ...this.query, page: 1 };
    this.loadUsers();
  }

  getRoleLabel(role: string): string {
    return this.i18n.t(`manageUsers.role.${role}`, role);
  }

  userFromRow(row: Record<string, unknown>): AdminUser {
    return row["entity"] as AdminUser;
  }

  openAddUserDialog(): void {
    const dialogRef = this.dialog.open<UserFormDialogComponent, UserFormData>(
      UserFormDialogComponent,
      {
        width: "520px",
        data: { mode: "create" },
      },
    );

    dialogRef
      .afterClosed()
      .pipe(takeUntil(this.destroy$))
      .subscribe((result) => {
        if (result) this.loadUsers();
      });
  }

  openEditUserDialog(user: AdminUser): void {
    const dialogRef = this.dialog.open<UserFormDialogComponent, UserFormData>(
      UserFormDialogComponent,
      {
        width: "520px",
        data: { user, mode: "edit" },
      },
    );

    dialogRef
      .afterClosed()
      .pipe(takeUntil(this.destroy$))
      .subscribe((result) => {
        if (result) this.loadUsers();
      });
  }

  openAssignRolesDialog(user: AdminUser): void {
    const dialogRef = this.dialog.open<
      AssignRolesDialogComponent,
      AssignRolesData
    >(AssignRolesDialogComponent, {
      width: "520px",
      data: { user },
    });

    dialogRef
      .afterClosed()
      .pipe(takeUntil(this.destroy$))
      .subscribe((result) => {
        if (result) this.loadUsers();
      });
  }

  toggleUserStatus(user: AdminUser): void {
    const activate = !user.isActive;
    this.confirmDialogTitle = this.i18n.t(
      activate
        ? "manageUsers.confirmActivateTitle"
        : "manageUsers.confirmDeactivateTitle",
      activate ? "Kích hoạt người dùng" : "Vô hiệu hóa người dùng",
    );
    this.confirmDialogMessage = this.i18n.t(
      "manageUsers.confirmMessage",
      'Bạn có chắc muốn {{action}} người dùng "{{name}}"?',
      {
        action: this.i18n.t(
          activate
            ? "manageUsers.actionActivate"
            : "manageUsers.actionDeactivate",
          activate ? "kích hoạt" : "vô hiệu hóa",
        ),
        name: user.fullName,
      },
    );
    this.confirmDialogConfirmLabel = this.i18n.t(
      activate ? "adminPage.activate" : "adminPage.deactivate",
      activate ? "Kích hoạt" : "Vô hiệu hóa",
    );
    this.pendingUserAction = { user, activate };
    this.showConfirmDialog = true;
    this.cdr.markForCheck();
  }

  onConfirmDialogConfirmed(): void {
    this.showConfirmDialog = false;
    const pending = this.pendingUserAction;
    this.pendingUserAction = null;
    if (!pending) return;

    const activate = pending.activate;
    const obs$ = activate
      ? this.adminService.activateUser(pending.user.id)
      : this.adminService.deactivateUser(pending.user.id);

    obs$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.snackBar.open(
          this.i18n.t(
            activate
              ? "manageUsers.activateSuccess"
              : "manageUsers.deactivateSuccess",
            activate
              ? "Đã kích hoạt người dùng thành công"
              : "Đã vô hiệu hóa người dùng thành công",
          ),
          this.i18n.t("common.close", "Đóng"),
          { duration: 3000 },
        );
        this.loadUsers();
      },
      error: () => {
        this.snackBar.open(
          this.i18n.t(
            activate
              ? "manageUsers.activateFailed"
              : "manageUsers.deactivateFailed",
            activate
              ? "Không thể kích hoạt người dùng"
              : "Không thể vô hiệu hóa người dùng",
          ),
          this.i18n.t("common.close", "Đóng"),
          { duration: 5000 },
        );
      },
    });
  }

  onConfirmDialogCancelled(): void {
    this.showConfirmDialog = false;
    this.pendingUserAction = null;
  }
}
