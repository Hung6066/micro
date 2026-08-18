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
import {
  AdminSessionCenterResponse,
  IamRevocation,
  User,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { IamRevocationEditDialogComponent } from "./iam-revocation-edit-dialog.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-iam-sessions-page",
  standalone: true,
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
    <div *ngIf="error" class="hh-state hh-state--error" role="alert">
      {{ error }}
    </div>
    <hh-data-table
      [label]="'admin.activeSessions' | hhTranslate"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [empty]="!loading && !error && !rows.length"
      ><ng-template hhDataTableCell="actions" let-row
        ><hh-action-button
          *ngIf="canWrite"
          (pressed)="revoke(row)"
          kind="danger"
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
        this.rows = data.sessions.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "userId", label: this.i18n.t("admin.subject", "Subject") },
      { key: "id", label: this.i18n.t("admin.sessionId", "Session ID") },
      { key: "createdAt", label: this.i18n.t("admin.createdAt", "Created") },
      { key: "expiresAt", label: this.i18n.t("admin.expiresAt", "Expires") },
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

@Component({
  selector: "app-iam-revocations-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    MatDialogModule,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: ` <hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.revocations' | hhTranslate: 'Revocations'"
      [subtitle]="
        'admin.revocationsSubtitle'
          | hhTranslate: 'Record and inspect explicit principal revocations.'
      " /><hh-toolbar
      hhPageToolbar
      [label]="'admin.revocations' | hhTranslate: 'Revocations'"
      ><span hhToolbarTitle
        >{{ rows.length }} {{ "admin.revocations" | hhTranslate }}</span
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
      [label]="'admin.revocations' | hhTranslate"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [empty]="!loading && !error && !rows.length"
    ></hh-data-table
  ></hh-page-layout>`,
})
export class IamRevocationsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly dialog = inject(MatDialog);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.sessions.revoke");
  }
  rows: Record<string, unknown>[] = [];
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
      },
      {
        key: "principalType",
        label: this.i18n.t("admin.principalType", "Type"),
      },
      { key: "reason", label: this.i18n.t("admin.reason", "Reason") },
      { key: "occurredAt", label: this.i18n.t("admin.createdAt", "Occurred") },
    ];
  }
  ngOnInit(): void {
    this.load();
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
    if (!this.canWrite) return;
    this.dialog
      .open(IamRevocationEditDialogComponent, { width: "560px" })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.load();
      });
  }
}

@Component({
  selector: "app-iam-unused-permissions-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.unusedPermissions' | hhTranslate: 'Unused permissions'"
      [subtitle]="
        'admin.unusedPermissionsSubtitle'
          | hhTranslate: 'Analyze permissions with no observed usage.'
      " /><hh-toolbar
      hhPageToolbar
      [label]="'admin.unusedPermissions' | hhTranslate"
      ><hh-action-button
        (pressed)="load()"
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
    /></hh-toolbar>
    <div *ngIf="error" class="hh-state hh-state--error">{{ error }}</div>
    <hh-data-table
      [label]="'admin.unusedPermissions' | hhTranslate"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [empty]="!loading && !rows.length"
    ></hh-data-table
  ></hh-page-layout>`,
})
export class IamUnusedPermissionsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
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
