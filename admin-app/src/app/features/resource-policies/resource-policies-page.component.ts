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
import { forkJoin } from "rxjs";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  IamResourcePolicy,
  IamScope,
  IamServiceDefinition,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { ResourcePolicyEditDialogComponent } from "./resource-policy-edit-dialog.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-resource-policies-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: ` <hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.resourcePolicies' | hhTranslate: 'Resource policies'"
      [subtitle]="
        'admin.resourcePoliciesSubtitle'
          | hhTranslate
            : 'Resource-owned authorization constraints for business services.'
      " /><hh-toolbar
      hhPageToolbar
      [label]="'admin.resourcePolicies' | hhTranslate: 'Resource policies'"
      ><span hhToolbarTitle
        >{{ policies.length }}
        {{ "admin.resourcePolicies" | hhTranslate }}</span
      ><hh-action-button
        *ngIf="canWrite"
        hh-toolbar-actions
        kind="primary"
        icon="add"
        [label]="'admin.create' | hhTranslate"
        (pressed)="openCreate()" /><hh-action-button
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
        (pressed)="load()"
    /></hh-toolbar>
    <div *ngIf="error" class="hh-state hh-state--error" role="alert">
      {{ error }}
    </div>
    <hh-data-table
      [label]="'admin.resourcePolicies' | hhTranslate: 'Resource policies'"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [empty]="!loading && !error && !rows.length"
      ><ng-template hhDataTableCell="actions" let-row
        ><hh-action-button
          *ngIf="canWrite"
          kind="row"
          mode="icon-only"
          icon="edit"
          [label]="'admin.edit' | hhTranslate"
          (pressed)="edit(row)" /></ng-template></hh-data-table
  ></hh-page-layout>`,
})
export class ResourcePoliciesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly dialog = inject(HisHopeDialogService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    policies: IamResourcePolicy[];
    scopes: IamScope[];
    services: IamServiceDefinition[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load resource policies.",
  });
  policies: IamResourcePolicy[] = [];
  scopes: IamScope[] = [];
  services: IamServiceDefinition[] = [];
  rows: Record<string, unknown>[] = [];
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
      { key: "serviceKey", label: this.i18n.t("admin.serviceKey", "Service") },
      {
        key: "resourcePattern",
        label: this.i18n.t("admin.resourcePattern", "Resource pattern"),
      },
      {
        key: "scopeId",
        label: this.i18n.t("admin.scopeId", "Scope"),
        format: { type: "friendlyReference", references: this.scopes },
      },
      { key: "lifecycleStatus", label: this.i18n.t("admin.status", "Status") },
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
  }
  constructor() {
    effect(() => {
      const x = this.state.resource.data();
      if (x) {
        this.policies = x.policies;
        this.scopes = x.scopes;
        this.services = x.services;
        this.rows = x.policies.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(
      forkJoin({
        policies: this.api.getIamResourcePolicies(),
        scopes: this.api.getIamScopes(),
        services: this.api.getIamServices(),
      }),
    );
  }
  openCreate(): void {
    if (this.canWrite)
      this.dialog
        .open(ResourcePolicyEditDialogComponent, {
          width: "680px",
          data: { policy: null, scopes: this.scopes, services: this.services },
        })
        .afterClosed()
        .subscribe((saved) => {
          if (saved) this.load();
        });
  }
  edit(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const item = this.policies.find((x) => x.id === String(row["id"]));
    if (!item) return;
    this.dialog
      .open(ResourcePolicyEditDialogComponent, {
        width: "680px",
        data: { policy: item, scopes: this.scopes, services: this.services },
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.load();
      });
  }
  publish(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    this.api.publishIamResourcePolicy(id).subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to publish resource policy.",
        )),
    });
  }
}
