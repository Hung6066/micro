import { DatePipe, DecimalPipe } from "@angular/common";
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeActionButtonComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeSelectComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeGenealogyDto, HisHopeLotDto } from "@his-hope/frontend-foundation/contracts";
import { ManufacturingApiService } from "../../core/services/manufacturing-api.service";
import { HisHopeApiErrorMessageService as ApiErrorMessageService } from "@his-hope/frontend-foundation/i18n";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { portalEnumLabel } from "../../core/utils/portal-label.util";
import { EntityCrossWorkflowPanelComponent } from "../../core/components/entity-cross-workflow-panel.component";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTranslatePipe,
    EntityCrossWorkflowPanelComponent,
    HisHopeSelectComponent,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'customerPortal.inventoryLotsTitle' | hhTranslate: 'Inventory lots'"
        [subtitle]="pageSubtitle"
      />
      <div class="filters">
        <hh-select [ngModel]="dispositionFilter" (ngModelChange)="onDispositionChange($event)">
          <option value="">{{ "customerPortal.dispositionAll" | hhTranslate: "All dispositions" }}</option>
          <option value="Released">{{ "customerPortal.dispositionReleased" | hhTranslate: "Released" }}</option>
          <option value="Quarantined">{{ "customerPortal.dispositionQuarantined" | hhTranslate: "Quarantined" }}</option>
          <option value="Consumed">{{ "customerPortal.dispositionConsumed" | hhTranslate: "Consumed" }}</option>
        </hh-select>
      </div>
      <hh-data-table
        [label]="'customerPortal.inventoryLotsTitle' | hhTranslate: 'Inventory lots'"
        [columns]="columns"
        [rows]="tableRows"
        [loading]="loading"
        [error]="error"
        mode="client"
        [mobilePresentation]="'list'"
        [empty]="!loading && !error && !lots.length"
        [emptyMessage]="'customerPortal.noLots' | hhTranslate: 'No lots for the selected tenant.'"
        (retry)="loadLots()"
      >
        <ng-template hhDataTableCell="quantity" let-row>
          {{ row.quantity | number: "1.0-2" }} {{ row.uom }}
        </ng-template>
        <ng-template hhDataTableCell="disposition" let-row>
          <span class="badge">{{ dispositionLabel(row.disposition) }}</span>
        </ng-template>
        <ng-template hhDataTableCell="bestBefore" let-row>
          {{ row.bestBefore ?? ("customerPortal.tenantUnknown" | hhTranslate: "—") }}
        </ng-template>
        <ng-template hhDataTableCell="createdAt" let-row>
          {{ row.createdAt | date: "medium" }}
        </ng-template>
      </hh-data-table>
      <section class="lot-actions" aria-label="Lot operations">
        <div class="lot-action-form">
          <label for="selected-lot-id">{{ "customerPortal.selectLot" | hhTranslate: "Select a lot" }}
            <hh-select id="selected-lot-id" [(ngModel)]="selectedLotId" (ngModelChange)="onLotSelected($event)">
              <option value="">{{ "customerPortal.selectLot" | hhTranslate: "Select a lot" }}</option>
              @for (lot of lots; track lot.id) { <option [value]="lot.id">{{ lot.sku }} · {{ lot.quantity | number: '1.0-3' }} {{ lot.uom }} · {{ dispositionLabel(lot.disposition) }}</option> }
            </hh-select>
          </label>
          <label for="next-disposition">{{ "customerPortal.lotDisposition" | hhTranslate: "Disposition" }}
            <hh-select id="next-disposition" [(ngModel)]="nextDisposition">
              @for (value of dispositionOptions; track value) { <option [value]="value">{{ dispositionLabel(value) }}</option> }
            </hh-select>
          </label>
          <div class="actions"><hh-action-button kind="primary" icon="published_with_changes" [label]="'customerPortal.changeDisposition' | hhTranslate: 'Change disposition'" [disabled]="!selectedLotId || actionBusy" (pressed)="changeDisposition()" /><hh-action-button kind="secondary" icon="account_tree" [label]="'customerPortal.viewGenealogy' | hhTranslate: 'View genealogy'" [disabled]="!selectedLotId || actionBusy" (pressed)="loadGenealogy()" /></div>
        </div>
        @if (actionError) { <p class="action-error" role="alert">{{ actionError }}</p> }
        @if (genealogy) { <div class="genealogy"><h3>{{ "customerPortal.genealogy" | hhTranslate: "Lot genealogy" }}</h3><p class="meta">{{ genealogy.lot.sku }} · {{ genealogy.relations.length }} {{ "customerPortal.relations" | hhTranslate: "relations" }}</p><ul>@for (relation of genealogy.relations; track relation.transformationId + relation.lotId + relation.role) { <li><strong>{{ relation.role }}</strong> · {{ relation.sku }} · {{ relation.quantity | number: "1.0-3" }}</li> }</ul></div> }
        @for (lot of selectedLotForWorkflow; track lot.id) {
          <app-entity-cross-workflow-panel
            entityType="lot"
            [entityId]="lot.id"
            [loadTrace]="loadCrossEntityWorkflow"
          />
        }
      </section>
    </hh-page-layout>
  `,
  styles: [
    `
      :host {
        font-family: var(--font-sans);
      }
      .filters {
        margin-bottom: var(--space-md);
        max-width: 240px;
      }
      .badge {
        display: inline-block;
        padding: var(--space-2xs) var(--space-sm);
        border-radius: var(--radius-badge);
        background: var(--surface-muted);
        font-size: var(--font-size-caption);
      }
      .lot-actions { margin-top: var(--space-lg); padding: var(--space-md); border: 1px solid var(--border-subtle); border-radius: var(--radius-card); background: var(--surface); color: var(--text-primary); }
      .lot-action-form { display: flex; flex-wrap: wrap; align-items: end; gap: var(--space-md); }
      label { display: grid; gap: var(--space-2xs); color: var(--text-primary); font-size: var(--font-size-caption); }
      select { min-height: var(--control-height); min-width: min(180px, 100%); max-width: 100%; border: 1px solid var(--border-subtle); border-radius: var(--radius-control); padding: 0 var(--space-sm); background: var(--surface); color: var(--text-primary); font: inherit; }
      .actions { display: flex; flex-wrap: wrap; gap: var(--space-sm); }
      .action-error { color: var(--color-danger); }
      .genealogy { margin-top: var(--space-md); padding-top: var(--space-md); border-top: 1px solid var(--border-subtle); }
      .genealogy h3 { margin: 0; }
      .meta { color: var(--text-secondary); font-size: var(--font-size-caption); }
    `,
  ],
})
export class LotsPageComponent implements OnInit {
  private readonly manufacturingApi = inject(ManufacturingApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly errors = inject(ApiErrorMessageService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  loading = true;
  error = "";
  actionBusy = false;
  actionError = "";
  lots: HisHopeLotDto[] = [];
  selectedLotId = "";
  nextDisposition = "Released";
  genealogy: HisHopeGenealogyDto | null = null;
  tenantLabel: string | null = null;
  dispositionFilter = "";
  readonly dispositionOptions = ["Released", "Quarantined", "Rejected", "Hold", "Consumed"];

  get pageSubtitle(): string {
    this.i18n.locale();
    return this.i18n.t("customerPortal.tenantScope", "Tenant: {{tenant}}", {
      tenant:
        this.tenantLabel ??
        this.i18n.t("customerPortal.tenantUnknown", "—"),
    });
  }

  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      {
        key: "sku",
        label: this.i18n.t("customerPortal.colSku", "SKU"),
        sortable: true,
      },
      {
        key: "quantity",
        label: this.i18n.t("customerPortal.colQuantity", "Quantity"),
        sortable: true,
      },
      {
        key: "disposition",
        label: this.i18n.t("customerPortal.colDisposition", "Disposition"),
      },
      {
        key: "bestBefore",
        label: this.i18n.t("customerPortal.colBestBefore", "Best before"),
        responsivePriority: 2,
      },
      {
        key: "createdAt",
        label: this.i18n.t("customerPortal.colCreated", "Created"),
        sortable: true,
      },
    ];
  }

  get tableRows(): Record<string, unknown>[] {
    return this.lots as unknown as Record<string, unknown>[];
  }

  get selectedLotForWorkflow(): HisHopeLotDto[] {
    const lot = this.lots.find((item) => item.id === this.selectedLotId);
    return lot ? [lot] : [];
  }

  readonly loadCrossEntityWorkflow = (entityType: string, entityId: string) =>
    this.manufacturingApi.getCrossEntityWorkflow(entityType, entityId);

  ngOnInit(): void {
    this.tenantContext.activeTenantLabel$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((label) => {
        this.tenantLabel = label;
        this.cdr.markForCheck();
      });

    this.tenantContext.activeTenantKey$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadLots());
  }

  onDispositionChange(value: string): void {
    this.dispositionFilter = value;
    this.loadLots();
  }

  dispositionLabel(value: string): string {
    return portalEnumLabel(this.i18n, "disposition", value);
  }

  loadLots(): void {
    this.loading = true;
    this.error = "";
    this.manufacturingApi
      .getLots({
        disposition: this.dispositionFilter || undefined,
        limit: 100,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          this.lots = items ?? [];
          if (!this.lots.some((lot) => lot.id === this.selectedLotId)) this.selectedLotId = this.lots[0]?.id ?? "";
          this.genealogy = null;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: (error) => {
          this.error = this.errors.message(error, "customerPortal.lotsLoadFailed");
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  onLotSelected(lotId: string): void {
    this.selectedLotId = lotId;
    this.genealogy = null;
    const lot = this.lots.find((item) => item.id === lotId);
    this.nextDisposition = lot?.disposition ?? "Released";
    this.actionError = "";
  }

  changeDisposition(): void {
    if (!this.selectedLotId) return;
    this.actionBusy = true;
    this.actionError = "";
    this.manufacturingApi.changeLotDisposition(this.selectedLotId, this.nextDisposition).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.actionBusy = false; this.loadLots(); },
      error: (error) => { this.actionError = this.errors.message(error, "customerPortal.dispositionChangeFailed"); this.actionBusy = false; this.cdr.markForCheck(); },
    });
  }

  loadGenealogy(): void {
    if (!this.selectedLotId) return;
    this.actionBusy = true;
    this.actionError = "";
    this.manufacturingApi.getLotGenealogy(this.selectedLotId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => { this.genealogy = result; this.actionBusy = false; this.cdr.markForCheck(); },
      error: (error) => { this.actionError = this.errors.message(error, "customerPortal.genealogyLoadFailed"); this.actionBusy = false; this.cdr.markForCheck(); },
    });
  }
}
