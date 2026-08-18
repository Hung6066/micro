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
import { AuthorizationPolicy } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { PolicyEditDialogComponent } from "./policy-edit-dialog.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-policies-page",
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
      [title]="'admin.policies' | hhTranslate: 'Policies'"
      [subtitle]="
        'admin.policiesSubtitle'
          | hhTranslate
            : 'Versioned authorization policies with lint and publish controls.'
      " /><hh-toolbar
      hhPageToolbar
      [label]="'admin.policies' | hhTranslate: 'Policies'"
      ><span hhToolbarTitle
        >{{ policies.length }} {{ "admin.policies" | hhTranslate }}</span
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
      [label]="'admin.policies' | hhTranslate: 'Policies'"
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
          *ngIf="canWrite"
          kind="row"
          mode="icon-only"
          icon="rule"
          [label]="'admin.lint' | hhTranslate: 'Lint'"
          (pressed)="lint(row)" /><hh-action-button
          *ngIf="canWrite && row['lifecycleStatus'] !== 'published'"
          kind="row"
          mode="icon-only"
          icon="publish"
          [label]="'admin.publish' | hhTranslate"
          (pressed)="publish(row)" /><hh-action-button
          *ngIf="canWrite && row['lifecycleStatus'] === 'published'"
          kind="danger"
          mode="icon-only"
          icon="undo"
          [label]="'admin.rollback' | hhTranslate"
          (pressed)="rollback(row)" /></ng-template></hh-data-table
  ></hh-page-layout>`,
})
export class PoliciesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly dialog = inject(MatDialog);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.settings.write");
  }
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<AuthorizationPolicy[]>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load policies.",
  });
  policies: AuthorizationPolicy[] = [];
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
        key: "description",
        label: this.i18n.t("admin.description", "Description"),
      },
      { key: "owner", label: this.i18n.t("admin.owner", "Owner") },
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
      const policies = this.state.resource.data();
      if (policies) {
        this.policies = policies;
        this.rows = policies.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  load(): void {
    this.state.load(this.api.getAuthorizationPolicies());
  }
  openCreate(): void {
    if (this.canWrite)
      this.dialog
        .open(PolicyEditDialogComponent, {
          width: "640px",
          data: { policy: null },
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
      .open(PolicyEditDialogComponent, {
        width: "640px",
        data: { policy: item },
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.load();
      });
  }
  lint(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    this.api.lintAuthorizationPolicy(id).subscribe({
      next: (result) =>
        (this.error = result.valid ? "" : result.errors.join("; ")),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamAnalyzerFailed",
          "Policy analysis failed.",
        )),
    });
  }
  publish(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    this.api.publishAuthorizationPolicy(id).subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to publish policy.",
        )),
    });
  }
  rollback(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    this.api.rollbackAuthorizationPolicy(id).subscribe({
      next: () => this.load(),
      error: () =>
        (this.error = this.i18n.t(
          "admin.iamSaveFailed",
          "Unable to rollback policy.",
        )),
    });
  }
}
