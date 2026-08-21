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
import { catchError, of } from "rxjs";
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
import { AdminSessionCenterResponse } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-iam-sessions-page",
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
      [title]="'admin.activeSessions' | hhTranslate: 'Active sessions'"
      [subtitle]="
        'admin.activeSessionsSubtitle'
          | hhTranslate: 'Review and revoke human sessions from the server.'
      " /><hh-toolbar
      hhPageToolbar
      [label]="'admin.activeSessions' | hhTranslate: 'Active sessions'"
      ><span hhToolbarTitle
        >{{ rows.length }}
        {{ "admin.sessions" | hhTranslate: "Sessions" }}</span
      ><hh-action-button
        (pressed)="load()"
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
    /></hh-toolbar>
    <hh-data-table
      [label]="'admin.activeSessions' | hhTranslate"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [error]="error"
      [empty]="!loading && !error && !rows.length"
      ><ng-template hhDataTableCell="actions" let-row
        ><hh-action-button
          *ngIf="canWrite"
          (pressed)="revoke(row)"
          kind="danger"
          mode="icon-only"
          icon="link_off"
          [label]="'admin.revoke' | hhTranslate" /></ng-template></hh-data-table
  ></hh-page-layout>`,
})
export class IamSessionsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.sessions.revoke");
  }
  rows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<AdminSessionCenterResponse>(
    {
      destroyRef: this.destroyRef,
      i18n: this.i18n,
      loadErrorMessageKey: "admin.iamLoadFailed",
      loadErrorFallback: "Unable to load sessions.",
    },
  );
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
        this.rows = data.sessions.map((item) => ({
          ...item,
          displaySubject: item.email || item.userId || "—",
        }));
        this.cdr.markForCheck();
      }
    });
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "displaySubject", label: this.i18n.t("admin.subject", "Subject") },
      { key: "id", label: this.i18n.t("admin.sessionId", "Session ID") },
      {
        key: "createdAt",
        label: this.i18n.t("admin.createdAt", "Created"),
        format: "dateTime",
      },
      {
        key: "expiresAt",
        label: this.i18n.t("admin.expiresAt", "Expires"),
        format: "dateTime",
      },
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
  load(): void {
    this.error = "";
    this.state.load(
      this.api.getAdminSessionCenter().pipe(
        catchError(() => {
          this.error = this.i18n.t(
            "admin.iamLoadFailed",
            "Unable to load sessions.",
          );
          return of({ schemaVersion: "", evaluatedAt: "", sessions: [] });
        }),
      ),
    );
  }
  revoke(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const userId = String(row["userId"] ?? "");
    const id = String(row["id"] ?? "");
    if (!userId || !id) return;
    this.api
      .revokeAdminSession(userId, id, "Revoked from IAM sessions")
      .subscribe({
        next: () => this.load(),
        error: () =>
          (this.error = this.i18n.t(
            "admin.iamSaveFailed",
            "Unable to revoke session.",
          )),
      });
  }
}
