import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatButtonModule } from "@angular/material/button";
import {
  MobileDeliverySummary,
  MobileDeviceRegistration,
} from "../../core/contracts/admin.contracts";
import { MobileOperationsApiService } from "../../core/services/mobile-operations-api.service";

import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HisHopeResourceState } from "@his-hope/frontend-foundation/query";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { catchError, forkJoin, of, tap } from "rxjs";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-mobile-operations-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    MatButtonModule,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.mobileOperations' | hhTranslate"
        [subtitle]="'admin.mobileOperationsSubtitle' | hhTranslate"
      />
      @if (loading) {
        <hh-state
          kind="loading"
          [message]="'admin.loadingMobileOperations' | hhTranslate"
        />
      } @else if (error) {
        <hh-state kind="error" icon="error" [message]="error | hhTranslate"
          ><hh-action-button
            (pressed)="load()"
            kind="secondary"
            icon="refresh"
            [label]="'common.retry' | hhTranslate"
        /></hh-state>
      } @else {
        <section
          class="summary-grid"
          [attr.aria-label]="
            'admin.mobileDeliverySummary'
              | hhTranslate: 'Mobile delivery summary'
          "
        >
          <article>
            <strong>{{ summary?.queued ?? 0 }}</strong
            ><span>{{ "admin.mobileQueued" | hhTranslate }}</span>
          </article>
          <article>
            <strong>{{ summary?.sent ?? 0 }}</strong
            ><span>{{ "admin.mobileSent" | hhTranslate }}</span>
          </article>
          <article>
            <strong>{{ summary?.failed ?? 0 }}</strong
            ><span>{{ "admin.mobileFailed" | hhTranslate }}</span>
          </article>
          <article>
            <strong>{{ devices.length }}</strong
            ><span>{{ "admin.mobileDevices" | hhTranslate }}</span>
          </article>
        </section>
        <section class="panel">
          <div class="panel__header">
            <h2>{{ "admin.mobileDevices" | hhTranslate }}</h2>
            <hh-action-button
              (pressed)="load()"
              kind="secondary"
              icon="refresh"
              [label]="'common.refresh' | hhTranslate"
            />
          </div>
          @if (!devices.length) {
            <hh-state
              kind="empty"
              [message]="'admin.noMobileDevices' | hhTranslate"
            />
          } @else {
            <hh-data-table
              [label]="'admin.mobileDevices' | hhTranslate"
              [columns]="columns"
              [rows]="rows"
              [loading]="loading"
              [empty]="false"
              ><ng-template hhDataTableCell="actions" let-row
                ><hh-action-button
                  *ngIf="row['active'] && canWrite"
                  kind="danger"
                  mode="icon-only"
                  icon="link_off"
                  [label]="'admin.revoke' | hhTranslate"
                  [disabled]="busy"
                  (pressed)="revokeByRow(row)" /></ng-template
            ></hh-data-table>
          }
        </section>
        @if (summary?.lastFailure; as failure) {
          <p class="failure">
            {{ "admin.lastMobileFailure" | hhTranslate }}:
            {{ failure.platform }} · {{ failure.createdAt | date: "short" }}
          </p>
        }
      }
    </hh-page-layout>
  `,
  styles: [
    `
      .summary-grid {
        display: grid;
        grid-template-columns: repeat(4, minmax(0, 1fr));
        gap: var(--space-lg);
        margin-bottom: var(--space-lg);
      }
      .summary-grid article,
      .panel {
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
      }
      .summary-grid article {
        display: grid;
        gap: var(--space-snug);
        padding: 18px;
      }
      .summary-grid strong {
        font-size: var(--font-size-display);
      }
      .summary-grid span {
        color: var(--text-secondary);
      }
      .panel {
        padding: 18px;
      }
      .panel__header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: var(--space-md);
      }
      h2 {
        margin: 0;
        font-size: var(--font-size-icon-sm);
      }
      .failure {
        color: var(--color-danger);
      }
      @media (max-width: 800px) {
        .summary-grid {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }
      }
    `,
  ],
})
export class MobileOperationsPageComponent implements OnInit {
  private readonly api = inject(MobileOperationsApiService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly toast = inject(HisHopeToastService);
  private readonly destroyRef = inject(DestroyRef);
  readonly resource = new HisHopeResourceState<{
    devices: {
      items: MobileDeviceRegistration[];
      page: number;
      pageSize: number;
      total: number;
    };
    summary: MobileDeliverySummary;
  } | null>(this.destroyRef);
  get canWrite(): boolean {
    return this.permissions.has("admin.users.write");
  }
  devices: MobileDeviceRegistration[] = [];
  summary: MobileDeliverySummary | null = null;
  busy = false;
  get loading(): boolean {
    return this.resource.loading();
  }
  error: string | null = null;
  get rows(): Record<string, unknown>[] {
    return this.devices.map((device) => ({
      ...device,
      displayStatus: device.active
        ? this.i18n.t("admin.active", "Active")
        : this.i18n.t("admin.revoked", "Revoked"),
    }));
  }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "platform", label: this.i18n.t("admin.platform", "Platform") },
      { key: "userId", label: this.i18n.t("admin.userId", "User ID") },
      {
        key: "lastSeenAt",
        label: this.i18n.t("admin.lastSeen", "Last seen"),
        format: "dateTime",
      },
      {
        key: "displayStatus",
        label: this.i18n.t("admin.status", "Status"),
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
    this.error = null;
    this.resource.load(
      forkJoin({
        devices: this.api.getMobileDevices(),
        summary: this.api.getMobileDeliverySummary(),
      }).pipe(
        tap((result) => {
          this.devices = result.devices.items;
          this.summary = result.summary;
          this.cdr.markForCheck();
        }),
        catchError(() => {
          this.error = "admin.mobileOperationsLoadFailed";
          return of(null);
        }),
      ),
    );
  }

  revoke(device: MobileDeviceRegistration): void {
    if (!this.canWrite) return;
    this.busy = true;
    this.cdr.markForCheck();
    this.api.revokeMobileDevice(device.id).subscribe({
      next: () => {
        device.active = false;
        device.revokedAt = new Date().toISOString();
        this.busy = false;
        this.toast.success(
          this.i18n.t("admin.mobileDeviceRevoked", "Mobile device revoked."),
          { duration: 3000 },
        );
        this.cdr.markForCheck();
      },
      error: () => {
        this.busy = false;
        this.error = "admin.mobileDeviceRevokeFailed";
        this.toast.error(
          this.i18n.t(
            "admin.mobileDeviceRevokeFailed",
            "Unable to revoke mobile device.",
          ),
          { duration: 5000 },
        );
        this.cdr.markForCheck();
      },
    });
  }
  revokeByRow(row: Record<string, unknown>): void {
    const device = this.devices.find((item) => item.id === row["id"]);
    if (device) {
      this.revoke(device);
    }
  }
}
