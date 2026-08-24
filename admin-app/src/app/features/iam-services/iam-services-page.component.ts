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
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { IamServiceDefinition } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { IamServiceEditDialogComponent } from "./iam-service-edit-dialog.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-iam-services-page",
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
      title="admin.services"
      titleFallback="Service catalog"
      subtitle="admin.servicesSubtitle"
      subtitleFallback="Register business services and their permission namespaces."
      countLabel="admin.services"
      countLabelFallback="Services"
      [count]="services.length"
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
          *ngIf="canWrite"
          kind="row"
          mode="icon-only"
          icon="edit"
          [label]="'admin.edit' | hhTranslate"
          (pressed)="edit(row)"
        />
        <hh-action-button
          *ngIf="canWrite && row['isActive']"
          kind="danger"
          mode="icon-only"
          icon="toggle_off"
          [label]="'admin.deactivate' | hhTranslate"
          (pressed)="toggle(row)"
        />
        <hh-action-button
          *ngIf="canWrite && !row['isActive']"
          kind="row"
          mode="icon-only"
          icon="toggle_on"
          [label]="'admin.activate' | hhTranslate"
          (pressed)="toggle(row)"
        />
      </ng-template>
    </hh-resource-list-page>
  `,
})
export class IamServicesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly dialog = inject(HisHopeDialogService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<IamServiceDefinition[]>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load services.",
  });
  services: IamServiceDefinition[] = [];
  rows: Record<string, unknown>[] = [];
  saving = false;
  get loading(): boolean {
    return this.state.loading;
  }
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "key", label: this.i18n.t("admin.key", "Key"), sortable: true },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
      },
      {
        key: "permissionPrefix",
        label: this.i18n.t("admin.permissionPrefix", "Permission prefix"),
      },
      { key: "owner", label: this.i18n.t("admin.owner", "Owner") },
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
  constructor() {
    effect(() => {
      const services = this.state.resource.data();
      if (services) {
        this.services = services;
        this.rows = services.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(this.api.getIamServices());
  }
  openCreate(): void {
    if (this.canWrite)
      this.dialog
        .open(IamServiceEditDialogComponent, {
          width: "560px",
          data: { service: null },
        })
        .afterClosed()
        .subscribe((saved) => {
          if (saved) this.load();
        });
  }
  edit(row: Record<string, unknown>): void {
    const item = this.services.find((x) => x.id === String(row["id"]));
    if (!item || !this.canWrite) return;
    this.dialog
      .open(IamServiceEditDialogComponent, {
        width: "560px",
        data: { service: item },
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.load();
      });
  }
  toggle(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    const call = row["isActive"]
      ? this.api.deactivateIamService(id)
      : this.api.activateIamService(id);
    call.subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to update service.",
        )),
    });
  }
}
