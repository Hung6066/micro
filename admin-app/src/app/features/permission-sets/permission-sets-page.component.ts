import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
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
  IamPermissionSet,
  IamScope,
  PermissionDefinition,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { iamScopeLabel } from "../../core/utils/iam-display.util";
import { PermissionSetEditDialogComponent } from "./permission-set-edit-dialog.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-permission-sets-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    MatDialogModule,
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
      [title]="'admin.permissionSets' | hhTranslate: 'Permission sets'"
      [subtitle]="
        'admin.permissionSetsSubtitle'
          | hhTranslate: 'Governed bundles of canonical permissions.'
      " /><hh-toolbar
      hhPageToolbar
      [label]="'admin.permissionSets' | hhTranslate: 'Permission sets'"
      ><span hhToolbarTitle
        >{{ sets.length }} {{ "admin.permissionSets" | hhTranslate }}</span
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
      [label]="'admin.permissionSets' | hhTranslate: 'Permission sets'"
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
          (pressed)="edit(row)" /><hh-action-button
          *ngIf="canWrite && row['lifecycleStatus'] !== 'published'"
          kind="row"
          mode="icon-only"
          icon="publish"
          [label]="'admin.publish' | hhTranslate"
          (pressed)="publish(row)" /></ng-template></hh-data-table
  ></hh-page-layout>`,
})
export class PermissionSetsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly dialog = inject(MatDialog);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    sets: IamPermissionSet[];
    scopes: IamScope[];
    permissions: PermissionDefinition[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load permission sets.",
  });
  sets: IamPermissionSet[] = [];
  scopes: IamScope[] = [];
  permissionsCatalog: PermissionDefinition[] = [];
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
      { key: "key", label: this.i18n.t("admin.key", "Key"), sortable: true },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
      },
      { key: "scopeId", label: this.i18n.t("admin.scopeId", "Scope") },
      { key: "version", label: this.i18n.t("admin.version", "Version") },
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
        this.sets = x.sets;
        this.scopes = x.scopes;
        this.permissionsCatalog = x.permissions.filter(
          (item) => !item.isDeprecated,
        );
        this.rows = x.sets.map((item) => ({
          ...item,
          scopeId: iamScopeLabel(item.scopeId, x.scopes),
        }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(
      forkJoin({
        sets: this.api.getIamPermissionSets(),
        scopes: this.api.getIamScopes(),
        permissions: this.api.getPermissions(),
      }),
    );
  }
  openCreate(): void {
    if (this.canWrite)
      this.dialog
        .open(PermissionSetEditDialogComponent, {
          width: "680px",
          data: {
            set: null,
            scopes: this.scopes,
            permissions: this.permissionsCatalog,
          },
        })
        .afterClosed()
        .subscribe((saved) => {
          if (saved) this.load();
        });
  }
  edit(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const item = this.sets.find((x) => x.id === String(row["id"]));
    if (!item) return;
    this.dialog
      .open(PermissionSetEditDialogComponent, {
        width: "680px",
        data: {
          set: item,
          scopes: this.scopes,
          permissions: this.permissionsCatalog,
        },
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.load();
      });
  }
  publish(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    if (!id) return;
    this.api.publishIamPermissionSet(id).subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to publish permission set.",
        )),
    });
  }
}
