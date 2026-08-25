import {
  ChangeDetectorRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { catchError, of } from "rxjs";
import {
  HisHopeDataTableColumn,
  HisHopeResourceListPageComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { TenantContextService } from "../../core/services/tenant-context.service";

@Component({
  selector: "app-iam-unused-permissions-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [HisHopeResourceListPageComponent],
  template: `
    <hh-resource-list-page
      title="admin.unusedPermissions"
      titleFallback="Unused permissions"
      subtitle="admin.unusedPermissionsSubtitle"
      subtitleFallback="Analyze permissions with no observed usage."
      [showCreate]="false"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [error]="error"
      (refresh)="load()"
    />
  `,
})
export class IamUnusedPermissionsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly tenantContext = inject(TenantContextService);
  rows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    unusedPermissions: string[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamAnalyzerFailed",
    loadErrorFallback: "Analyzer failed.",
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
        this.rows = data.unusedPermissions.map((permission) => ({
          permission,
        }));
        this.cdr.markForCheck();
      }
    });
  }
  get columns(): HisHopeDataTableColumn[] {
    return [
      {
        key: "permission",
        label: this.i18n.t("admin.permission", "Permission"),
      },
    ];
  }
  ngOnInit(): void {
    this.load();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.load());
  }
  load(): void {
    this.error = "";
    this.state.load(
      this.api.analyzeIamUnusedPermissions().pipe(
          catchError(() => {
            this.error = this.i18n.t(
              "admin.iamAnalyzerFailed",
              "Analyzer failed.",
            );
            return of({ unusedPermissions: [] });
          }),
        ),
    );
  }
}
