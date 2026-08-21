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
import { AuthorizationPolicy } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { PolicyEditDialogComponent } from "./policy-edit-dialog.component";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-policies-page",
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
      title="admin.policies"
      titleFallback="Policies"
      subtitle="admin.policiesSubtitle"
      subtitleFallback="Versioned authorization policies with lint and publish controls."
      [count]="policies.length"
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
          *ngIf="canWrite"
          kind="secondary"
          mode="label"
          icon="rule"
          [label]="'admin.lint' | hhTranslate: 'Lint'"
          (pressed)="lint(row)"
        />
        <hh-action-button
          *ngIf="canWrite && row['lifecycleStatus'] !== 'published'"
          kind="secondary"
          mode="label"
          icon="publish"
          [label]="'admin.publish' | hhTranslate"
          (pressed)="publish(row)"
        />
        <hh-action-button
          *ngIf="canWrite && row['lifecycleStatus'] === 'published'"
          kind="danger"
          mode="label"
          icon="undo"
          [label]="'admin.rollback' | hhTranslate"
          (pressed)="rollback(row)"
        />
      </ng-template>
    </hh-resource-list-page>
  `,
})
export class PoliciesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly dialog = inject(HisHopeDialogService);
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
        width: "360px",
        pinned: "right",
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
