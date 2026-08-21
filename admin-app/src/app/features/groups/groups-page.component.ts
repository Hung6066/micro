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
import { IamGroup, IamScope } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { finalize, forkJoin, take } from "rxjs";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeDialogService } from "@his-hope/frontend-foundation/ui";
import { HisHopeToastService } from "@his-hope/frontend-foundation/ui";
import { GroupEditDialogComponent } from "./group-edit-dialog.component";
@Component({
  selector: "app-groups-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    HisHopeActionButtonComponent,
    HisHopeResourceListPageComponent,
    HisHopeResourceRowActionsDirective,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-resource-list-page
      title="admin.groups"
      subtitle="admin.groupsSubtitle"
      subtitleFallback="Manage identity groups and memberships."
      refreshLabel="common.refresh"
      refreshLabelFallback="Refresh"
      [count]="groups.length"
      [canWrite]="canWrite"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [error]="error"
      (create)="openCreateDialog()"
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
          [disabled]="busy"
        />
        <hh-action-button
          *ngIf="canWrite && !row['isActive']"
          kind="row"
          mode="icon-only"
          icon="toggle_on"
          [label]="'admin.activate' | hhTranslate"
          (pressed)="toggle(row)"
          [disabled]="busy"
        />
      </ng-template>
    </hh-resource-list-page>
  `,
})
export class GroupsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly dialog = inject(HisHopeDialogService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly toast = inject(HisHopeToastService);
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  groups: IamGroup[] = [];
  scopes: IamScope[] = [];
  rows: Record<string, unknown>[] = [];
  busy = false;
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    groups: IamGroup[];
    scopes: IamScope[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load groups.",
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
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "key", label: this.i18n.t("admin.key", "Key"), sortable: true },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
        sortable: true,
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
        width: "128px",
        minWidth: 128,
        align: "center" as const,
      },
    ];
  }
  ngOnInit(): void {
    this.load();
  }
  constructor() {
    effect(() => {
      const data = this.state.resource.data();
      if (data) {
        this.groups = data.groups;
        this.scopes = data.scopes;
        this.rows = data.groups.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(
      forkJoin({
        groups: this.api.getIamGroups().pipe(take(1)),
        scopes: this.api.getIamScopes().pipe(take(1)),
      }),
    );
  }
  openCreateDialog(): void {
    if (!this.canWrite) return;
    this.dialog
      .open(GroupEditDialogComponent, {
        width: "min(560px, calc(100vw - 32px))",
        data: { group: null, scopes: this.scopes },
      })
      .afterClosed()
      .subscribe((saved) => saved && this.load());
  }
  edit(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const item = this.groups.find((x) => x.id === String(row["id"]));
    if (!item) return;
    this.dialog
      .open(GroupEditDialogComponent, {
        width: "min(560px, calc(100vw - 32px))",
        data: { group: item, scopes: this.scopes },
      })
      .afterClosed()
      .subscribe((saved) => saved && this.load());
  }
  toggle(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    if (!id) return;
    const request = row["isActive"]
      ? this.api.deactivateIamGroup(id)
      : this.api.activateIamGroup(id);
    this.busy = true;
    request
      .pipe(
        finalize(() => {
          this.busy = false;
          this.cdr.markForCheck();
        }),
      )
      .subscribe({
        next: () => {
          this.toast.success(
            this.i18n.t("admin.groupUpdated", "Group updated."),
            { duration: 3000 },
          );
          this.load();
        },
        error: () => {
          this.error = this.i18n.t(
            "admin.iamSaveFailed",
            "Unable to update group.",
          );
          this.toast.error(this.error, { duration: 5000 });
        },
      });
  }
}
