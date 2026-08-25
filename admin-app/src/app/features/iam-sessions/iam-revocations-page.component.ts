import {
  ChangeDetectorRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { HisHopeDialogService } from "@his-hope/frontend-foundation/ui";
import { catchError, forkJoin, of } from "rxjs";
import {
  HisHopeDataTableColumn,
  HisHopeResourceListPageComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  IamRevocation,
  IamWorkloadRole,
  User,
} from "../../core/contracts/admin.contracts";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { IamRevocationEditDialogComponent } from "./iam-revocation-edit-dialog.component";
import { iamPrincipalLabel } from "../../core/utils/iam-display.util";

@Component({
  selector: "app-iam-revocations-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [HisHopeResourceListPageComponent],
  template: `
    <hh-resource-list-page
      title="admin.revocations"
      titleFallback="Revocations"
      subtitle="admin.revocationsSubtitle"
      subtitleFallback="Record and inspect explicit principal revocations."
      [count]="rows.length"
      [canWrite]="canWrite"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [error]="error"
      (create)="openCreate()"
      (refresh)="load()"
    />
  `,
})
export class IamRevocationsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly dialog = inject(HisHopeDialogService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.sessions.revoke");
  }
  rows: Record<string, unknown>[] = [];
  users: User[] = [];
  workloadRoles: IamWorkloadRole[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    revocations: IamRevocation[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load revocations.",
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
        this.rows = data.revocations.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
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
        key: "principalType",
        label: this.i18n.t("admin.principalType", "Type"),
      },
      { key: "reason", label: this.i18n.t("admin.reason", "Reason") },
      {
        key: "occurredAt",
        label: this.i18n.t("admin.createdAt", "Occurred"),
        format: "dateTime",
      },
    ];
  }
  ngOnInit(): void {
    this.load();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.load());
  }
  load(): void {
    this.state.load(
      this.api.getIamRevocations().pipe(
          catchError(() => {
            this.error = this.i18n.t(
              "admin.iamLoadFailed",
              "Unable to load revocations.",
            );
            return of({ schemaVersion: "", evaluatedAt: "", revocations: [] });
          }),
        ),
    );
  }
  openCreate(): void {
    if (this.canWrite)
      forkJoin({
        users: this.api.getUsers(),
        workloadRoles: this.api.getIamWorkloadRoles(),
      }).subscribe({
        next: (data) =>
          this.dialog
            .open(IamRevocationEditDialogComponent, { width: "560px", data })
            .afterClosed()
            .subscribe((saved) => {
              if (saved) this.load();
            }),
        error: () => {
          this.error = this.i18n.t(
            "admin.iamLoadFailed",
            "Unable to load principals.",
          );
        },
      });
  }
}
