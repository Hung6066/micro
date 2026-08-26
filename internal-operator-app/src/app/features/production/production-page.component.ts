import { DatePipe, DecimalPipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import {
  HisHopeActionButtonComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeProductionBatchDto,
  HisHopeOperationExecutionDto,
  HisHopeProductionOrderDto,
  HisHopeLotDto,
  HisHopeRecipeDto,
  HisHopeManufacturingMachineDto,
} from "@his-hope/frontend-foundation/contracts";
import { ManufacturingApiService } from "../../core/services/manufacturing-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { HisHopeApiErrorMessageService as ApiErrorMessageService } from "@his-hope/frontend-foundation/i18n";
import { portalEnumLabel } from "../../core/utils/portal-label.util";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    HisHopeActionButtonComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'customerPortal.productionTitle' | hhTranslate: 'Production'"
        [subtitle]="pageSubtitle"
      />
      @if (loading) {
        <hh-state
          kind="loading"
          [message]="'customerPortal.loadingProduction' | hhTranslate: 'Loading production data…'"
        />
      } @else if (error) {
        <hh-state kind="error" [message]="error" />
      } @else {
        <section class="section create-order-panel">
          <h2>{{ "customerPortal.createProductionOrder" | hhTranslate: "Create production order" }}</h2>
          <form class="order-entry" (ngSubmit)="createProductionOrder()">
            <label>{{ "customerPortal.orderNumber" | hhTranslate: "Order number" }}<input name="orderNumber" [(ngModel)]="productionOrderDraft.orderNumber" required /></label>
            <label>{{ "customerPortal.productSku" | hhTranslate: "Product SKU" }}<input name="productSku" [(ngModel)]="productionOrderDraft.productSku" required /></label>
            <label>{{ "customerPortal.recipe" | hhTranslate: "Recipe" }}
              <select name="recipeId" [(ngModel)]="productionOrderDraft.recipeId" required>
                <option value="">{{ "customerPortal.selectRecipe" | hhTranslate: "Select recipe" }}</option>
                @for (recipe of recipes; track recipe.id) { <option [value]="recipe.id">{{ recipe.productSku }} · v{{ recipe.version }} · {{ recipe.processStep }}</option> }
              </select>
            </label>
            <label>{{ "customerPortal.targetQuantity" | hhTranslate: "Target quantity" }}<input name="targetQuantity" type="number" min="0.001" step="0.001" [(ngModel)]="productionOrderDraft.targetQuantity" required /></label>
            <label>{{ "customerPortal.outputUom" | hhTranslate: "Output UOM" }}<input name="outputUom" [(ngModel)]="productionOrderDraft.outputUom" required /></label>
            <div class="review-actions"><hh-action-button kind="primary" icon="add_task" type="submit" [label]="'customerPortal.createProductionOrder' | hhTranslate: 'Create production order'" [disabled]="productionOrderBusy" /></div>
          </form>
          @if (productionOrderError) { <p class="review-error" role="alert">{{ productionOrderError }}</p> }
        </section>
        <section class="section create-order-panel">
          <h2>{{ "customerPortal.createProductionBatch" | hhTranslate: "Create production batch" }}</h2>
          <form class="order-entry" (ngSubmit)="createProductionBatch()">
            <label>{{ "customerPortal.productionOrder" | hhTranslate: "Production order" }}
              <select name="productionOrderId" [(ngModel)]="productionBatchDraft.productionOrderId" required>
                <option value="">{{ "customerPortal.selectProductionOrder" | hhTranslate: "Select released order" }}</option>
                @for (order of orders; track order.id) { @if (order.status === "Released") { <option [value]="order.id">{{ order.orderNumber }} · {{ order.productSku }}</option> } }
              </select>
            </label>
            <label>{{ "customerPortal.batchNumber" | hhTranslate: "Batch number" }}<input name="batchNumber" [(ngModel)]="productionBatchDraft.batchNumber" required /></label>
            <label>{{ "customerPortal.plannedQuantity" | hhTranslate: "Planned quantity" }}<input name="plannedQuantity" type="number" min="0.001" step="0.001" [(ngModel)]="productionBatchDraft.plannedQuantity" required /></label>
            <label>{{ "customerPortal.machine" | hhTranslate: "Machine (optional)" }}
              <select name="machineId" [(ngModel)]="productionBatchDraft.machineId">
                <option value="">{{ "customerPortal.noMachine" | hhTranslate: "No machine" }}</option>
                @for (machine of machines; track machine.id) { <option [value]="machine.id">{{ machine.code }} · {{ machine.name }}</option> }
              </select>
            </label>
            <label>{{ "customerPortal.inputLot" | hhTranslate: "Input lot" }}
              <select name="inputLotId" [(ngModel)]="productionBatchDraft.inputLotId" required>
                <option value="">{{ "customerPortal.selectInputLot" | hhTranslate: "Select released lot" }}</option>
                @for (lot of lots; track lot.id) { <option [value]="lot.id">{{ lot.sku }} · {{ lot.quantity | number: "1.0-2" }} {{ lot.uom }}</option> }
              </select>
            </label>
            <label>{{ "customerPortal.inputQuantity" | hhTranslate: "Input quantity" }}<input name="inputQuantity" type="number" min="0.001" step="0.001" [(ngModel)]="productionBatchDraft.inputQuantity" required /></label>
            <div class="review-actions"><hh-action-button kind="primary" icon="precision_manufacturing" type="submit" [label]="'customerPortal.createProductionBatch' | hhTranslate: 'Create production batch'" [disabled]="productionBatchBusy" /></div>
          </form>
          @if (productionBatchError) { <p class="review-error" role="alert">{{ productionBatchError }}</p> }
        </section>
        <section class="section">
          <h2>{{ "customerPortal.productionOrders" | hhTranslate: "Production orders" }}</h2>
          @if (!orders.length) {
            <p class="empty">{{ "customerPortal.noProductionOrders" | hhTranslate: "No production orders." }}</p>
          } @else {
            <div class="cards">
              @for (order of orders; track order.id) {
                <article class="card">
                  <header>
                    <strong>{{ order.orderNumber }}</strong>
                    <span class="status">{{ productionOrderStatusLabel(order.status) }}</span>
                  </header>
                  <p>
                    {{ order.productSku }} ·
                    {{ order.targetQuantity | number: "1.0-2" }}
                    {{ order.outputUom }}
                  </p>
                  <p class="meta">
                    {{
                      "customerPortal.productionCreatedAt"
                        | hhTranslate
                          : "Created {{date}}"
                          : { date: (order.createdAt | date: "medium") ?? "" }
                    }}
                  </p>
                  @if (order.status === "Planned") {
                    <hh-action-button
                      kind="secondary"
                      icon="publish"
                      [label]="'customerPortal.releaseOrder' | hhTranslate: 'Release'"
                      (pressed)="releaseOrder(order)"
                    />
                    <hh-action-button kind="secondary" icon="cancel" [label]="'customerPortal.cancelOrder' | hhTranslate: 'Cancel order'" (pressed)="cancelOrder(order)" />
                  }
                </article>
              }
            </div>
          }
        </section>

        <section class="section">
          <h2>{{ "customerPortal.productionBatches" | hhTranslate: "Production batches" }}</h2>
          @if (!batches.length) {
            <p class="empty">{{ "customerPortal.noProductionBatches" | hhTranslate: "No production batches." }}</p>
          } @else {
            <div class="cards">
              @for (batch of batches; track batch.id) {
                <article class="card">
                  <header>
                    <strong>{{ batch.batchNumber }}</strong>
                    <span class="status">{{ productionBatchStatusLabel(batch.status) }}</span>
                  </header>
                  <p>
                    {{
                      "customerPortal.productionPlannedActual"
                        | hhTranslate
                          : "Planned {{planned}} · Actual {{actual}}"
                          : {
                              planned: (batch.plannedQuantity | number: "1.0-2") ?? "",
                              actual: (batch.actualOutputQuantity | number: "1.0-2") ?? "",
                            }
                    }}
                  </p>
                  <p class="meta">
                    {{
                      "customerPortal.productionCreatedAt"
                        | hhTranslate
                          : "Created {{date}}"
                          : { date: (batch.createdAt | date: "medium") ?? "" }
                    }}
                  </p>
                  @if (batch.status === "Created") {
                    <hh-action-button
                      kind="secondary"
                      icon="play_arrow"
                      [label]="'customerPortal.startBatch' | hhTranslate: 'Start batch'"
                      (pressed)="startBatch(batch)"
                    />
                    <hh-action-button kind="secondary" icon="cancel" [label]="'customerPortal.cancelBatch' | hhTranslate: 'Cancel batch'" [disabled]="batchActionId === batch.id" (pressed)="cancelBatch(batch)" />
                  }
                  @if (batch.status === "Started") {
                    <hh-action-button kind="secondary" icon="pause" [label]="'customerPortal.pauseBatch' | hhTranslate: 'Pause batch'" [disabled]="batchActionId === batch.id" (pressed)="transitionBatch(batch, 'pause')" />
                    <hh-action-button kind="primary" icon="check_circle" [label]="'customerPortal.completeBatch' | hhTranslate: 'Complete batch'" [disabled]="batchActionId === batch.id" (pressed)="transitionBatch(batch, 'complete')" />
                  }
                  @if (batch.status === "Paused") {
                    <hh-action-button kind="secondary" icon="play_arrow" [label]="'customerPortal.resumeBatch' | hhTranslate: 'Resume batch'" [disabled]="batchActionId === batch.id" (pressed)="transitionBatch(batch, 'resume')" />
                    <hh-action-button kind="primary" icon="check_circle" [label]="'customerPortal.completeBatch' | hhTranslate: 'Complete batch'" [disabled]="batchActionId === batch.id" (pressed)="transitionBatch(batch, 'complete')" />
                  }
                  @if (batch.status === "Started" && operationBatchId !== batch.id) {
                    <hh-action-button kind="secondary" icon="add_chart" [label]="'customerPortal.recordOperation' | hhTranslate: 'Record operation'" (pressed)="startOperationEntry(batch)" />
                  }
                  @if (batchActionError && batchActionId === batch.id) { <p class="review-error" role="alert">{{ batchActionError }}</p> }
                  @if (operationBatchId === batch.id) {
                    <form class="operation-entry" (ngSubmit)="recordOperation(batch)">
                      <label>{{ "customerPortal.operationSequence" | hhTranslate: "Sequence" }}<input name="sequence" type="number" min="0" [(ngModel)]="operationDraft.sequence" required /></label>
                      <label>{{ "customerPortal.processStep" | hhTranslate: "Process step" }}<input name="processStep" [(ngModel)]="operationDraft.processStep" required /></label>
                      <label>{{ "customerPortal.operationOperator" | hhTranslate: "Operator" }}<input name="operator" [(ngModel)]="operationDraft.operator" required /></label>
                      <label>{{ "customerPortal.operationInput" | hhTranslate: "Input quantity" }}<input name="inputQuantity" type="number" min="0.001" step="0.001" [(ngModel)]="operationDraft.inputQuantity" required /></label>
                      <label>{{ "customerPortal.operationOutput" | hhTranslate: "Output quantity" }}<input name="outputQuantity" type="number" min="0" step="0.001" [(ngModel)]="operationDraft.outputQuantity" required /></label>
                      <label>{{ "customerPortal.qcStatus" | hhTranslate: "QC status" }}<input name="qcStatus" [(ngModel)]="operationDraft.qcStatus" required /></label>
                      <div class="review-actions"><hh-action-button kind="primary" icon="save" type="submit" [label]="'common.save' | hhTranslate: 'Save'" [disabled]="operationBusy" /><hh-action-button kind="secondary" icon="close" type="button" [label]="'common.cancel' | hhTranslate: 'Cancel'" [disabled]="operationBusy" (pressed)="cancelOperationEntry()" /></div>
                    </form>
                    @if (operationError) { <p class="review-error" role="alert">{{ operationError }}</p> }
                  }
                  @if ((batch.operations?.length ?? 0) > 0) {
                    <div class="operations" [attr.aria-label]="'customerPortal.operationMeasurements' | hhTranslate: 'Operation measurements'">
                      @for (operation of batch.operations ?? []; track operation.id) {
                        <div class="operation-row">
                          <div class="operation-summary">
                            <strong>{{ operation.sequence }} · {{ operation.processStep }}</strong>
                            <span>
                              {{
                                "customerPortal.operationMassBalance"
                                  | hhTranslate
                                    : "Input {{input}} · Output {{output}} · Loss {{loss}}"
                                    : {
                                        input: (operation.inputQuantity | number: "1.0-2") ?? "",
                                        output: (operation.outputQuantity | number: "1.0-2") ?? "",
                                        loss: (operation.lossQuantity | number: "1.0-2") ?? "",
                                      }
                              }}
                            </span>
                          </div>
                          @if (operation.lossQuantity > 0 && !isLossReviewed(operation.id)) {
                            <div class="loss-review">
                              <label>
                                {{ "customerPortal.lossReviewer" | hhTranslate: "Reviewer" }}
                                <input
                                  [value]="reviewerDrafts[operation.id] || ''"
                                  (input)="setReviewer(operation.id, $any($event.target).value)"
                                />
                              </label>
                              <label>
                                {{ "customerPortal.lossReviewNotes" | hhTranslate: "Review notes" }}
                                <textarea
                                  [value]="notesDrafts[operation.id] || ''"
                                  (input)="setReviewNotes(operation.id, $any($event.target).value)"
                                  rows="2"
                                ></textarea>
                              </label>
                              <div class="review-actions">
                                <hh-action-button
                                  kind="primary"
                                  icon="check"
                                  [label]="'customerPortal.approveLoss' | hhTranslate: 'Approve loss'"
                                  [disabled]="reviewingOperationId === operation.id"
                                  (pressed)="reviewLoss(batch, operation, 'Approved')"
                                />
                                <hh-action-button
                                  kind="secondary"
                                  icon="close"
                                  [label]="'customerPortal.rejectLoss' | hhTranslate: 'Reject loss'"
                                  [disabled]="reviewingOperationId === operation.id"
                                  (pressed)="reviewLoss(batch, operation, 'Rejected')"
                                />
                              </div>
                            </div>
                          } @else if (isLossReviewed(operation.id)) {
                            <span class="reviewed">
                              {{ "customerPortal.lossReviewed" | hhTranslate: "Loss review recorded" }}
                            </span>
                          }
                        </div>
                      }
                    </div>
                  }
                  @if (reviewError) {
                    <p class="review-error" role="alert">{{ reviewError }}</p>
                  }
                </article>
              }
            </div>
          }
        </section>
      }
    </hh-page-layout>
  `,
  styles: [
    `
      :host {
        font-family: var(--font-sans);
      }
      .section {
        margin-bottom: var(--space-xl);
      }
      h2 {
        font-size: var(--font-size-section);
        font-weight: var(--font-weight-semibold);
        margin: 0 0 var(--space-md);
      }
      .cards {
        display: grid;
        gap: var(--space-md);
        grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      }
      .card {
        border: 1px solid var(--border-subtle);
        border-radius: var(--radius-card);
        padding: var(--space-md);
      }
      header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: var(--space-sm);
      }
      .status {
        font-size: var(--font-size-caption);
        color: var(--text-secondary);
        text-transform: uppercase;
      }
      .meta {
        font-size: var(--font-size-caption);
        color: var(--text-secondary);
        margin: var(--space-xs) 0 var(--space-sm);
      }
      .empty {
        color: var(--text-secondary);
        font-size: var(--font-size-body);
      }
      .operations {
        display: grid;
        gap: var(--space-sm);
        margin-top: var(--space-md);
        padding-top: var(--space-md);
        border-top: 1px solid var(--border-subtle);
      }
      .operation-row {
        display: grid;
        gap: var(--space-sm);
        padding: var(--space-sm);
        border: 1px solid var(--border-subtle);
        border-radius: var(--radius-sm);
      }
      .operation-summary {
        display: flex;
        justify-content: space-between;
        gap: var(--space-sm);
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      .operation-summary strong {
        color: var(--text-primary);
      }
      .loss-review {
        display: grid;
        gap: var(--space-sm);
      }
      .loss-review label {
        display: grid;
        gap: var(--space-2xs);
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      .loss-review input,
      .loss-review textarea {
        width: 100%;
        box-sizing: border-box;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-sm);
        padding: var(--space-xs);
        color: var(--text-primary);
        background: var(--surface-white);
        font: inherit;
      }
      .review-actions {
        display: flex;
        flex-wrap: wrap;
        gap: var(--space-sm);
      }
      .reviewed {
        color: var(--color-success);
        font-size: var(--font-size-caption);
      }
      .review-error {
        color: var(--color-danger);
        font-size: var(--font-size-caption);
        margin: var(--space-xs) 0 0;
      }
      .operation-entry { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: var(--space-sm); margin-top: var(--space-sm); padding: var(--space-sm); border: 1px solid var(--border-subtle); border-radius: var(--radius-sm); }
      .operation-entry label { display: grid; gap: var(--space-2xs); color: var(--text-secondary); font-size: var(--font-size-caption); }
      .operation-entry input { width: 100%; box-sizing: border-box; border: 1px solid var(--border-default); border-radius: var(--radius-sm); padding: var(--space-xs); color: var(--text-primary); background: var(--surface-white); font: inherit; }
      .order-entry { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: var(--space-sm); align-items: end; }
      .order-entry label { display: grid; gap: var(--space-2xs); color: var(--text-secondary); font-size: var(--font-size-caption); }
      .order-entry input { width: 100%; box-sizing: border-box; border: 1px solid var(--border-default); border-radius: var(--radius-sm); padding: var(--space-xs); color: var(--text-primary); background: var(--surface-white); font: inherit; }
      .order-entry select { width: 100%; box-sizing: border-box; border: 1px solid var(--border-default); border-radius: var(--radius-sm); padding: var(--space-xs); color: var(--text-primary); background: var(--surface-white); font: inherit; }
    `,
  ],
})
export class ProductionPageComponent implements OnInit {
  private readonly manufacturingApi = inject(ManufacturingApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly errors = inject(ApiErrorMessageService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  loading = true;
  error = "";
  orders: HisHopeProductionOrderDto[] = [];
  batches: HisHopeProductionBatchDto[] = [];
  tenantLabel: string | null = null;
  reviewerDrafts: Record<string, string> = {};
  notesDrafts: Record<string, string> = {};
  reviewedLossOperations = new Set<string>();
  reviewingOperationId: string | null = null;
  reviewError = "";
  operationBatchId: string | null = null;
  operationBusy = false;
  operationError = "";
  operationDraft = { sequence: 1, processStep: "", operator: "", inputQuantity: 0, outputQuantity: 0, qcStatus: "Pending" };
  productionOrderBusy = false;
  productionOrderError = "";
  productionOrderDraft = { orderNumber: "", productSku: "", recipeId: "", targetQuantity: 0, outputUom: "kg" };
  productionBatchBusy = false;
  productionBatchError = "";
  productionBatchDraft = { productionOrderId: "", batchNumber: "", plannedQuantity: 0, machineId: "", inputLotId: "", inputQuantity: 0 };
  lots: HisHopeLotDto[] = [];
  recipes: HisHopeRecipeDto[] = [];
  machines: HisHopeManufacturingMachineDto[] = [];
  batchActionId: string | null = null;
  batchActionError = "";

  get pageSubtitle(): string {
    this.i18n.locale();
    return this.i18n.t("customerPortal.tenantScope", "Tenant: {{tenant}}", {
      tenant:
        this.tenantLabel ??
        this.i18n.t("customerPortal.tenantUnknown", "—"),
    });
  }

  productionOrderStatusLabel(status: string): string {
    return portalEnumLabel(this.i18n, "productionOrderStatus", status);
  }

  productionBatchStatusLabel(status: string): string {
    return portalEnumLabel(this.i18n, "productionBatchStatus", status);
  }

  createProductionOrder(): void {
    const draft = this.productionOrderDraft;
    const recipeIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (!draft.orderNumber.trim() || !draft.productSku.trim() || !recipeIdPattern.test(draft.recipeId.trim()) || draft.targetQuantity <= 0 || !draft.outputUom.trim()) {
      this.productionOrderError = this.i18n.t("customerPortal.productionOrderFormInvalid", "Order number, SKU, valid recipe ID and positive quantity are required.");
      return;
    }
    this.productionOrderBusy = true;
    this.productionOrderError = "";
    this.manufacturingApi.createProductionOrder({ ...draft, orderNumber: draft.orderNumber.trim(), productSku: draft.productSku.trim(), recipeId: draft.recipeId.trim(), outputUom: draft.outputUom.trim() }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.productionOrderBusy = false; this.productionOrderDraft = { ...draft, orderNumber: "", productSku: "", targetQuantity: 0 }; this.load(); },
      error: (error) => { this.productionOrderError = this.errors.message(error, "customerPortal.productionOrderSaveFailed"); this.productionOrderBusy = false; this.cdr.markForCheck(); },
    });
  }

  createProductionBatch(): void {
    const draft = this.productionBatchDraft;
    const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (!uuid.test(draft.productionOrderId) || !draft.batchNumber.trim() || draft.plannedQuantity <= 0 || !uuid.test(draft.inputLotId) || draft.inputQuantity <= 0 || (draft.machineId.trim() && !uuid.test(draft.machineId.trim()))) {
      this.productionBatchError = this.i18n.t("customerPortal.productionBatchFormInvalid", "A released order, batch number and positive quantity are required.");
      return;
    }
    this.productionBatchBusy = true;
    this.productionBatchError = "";
    this.manufacturingApi.reserveLot(draft.inputLotId, { referenceType: "ProductionOrder", referenceId: draft.productionOrderId, quantity: draft.inputQuantity }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (reservation) => this.manufacturingApi.createProductionBatch({ productionOrderId: draft.productionOrderId, batchNumber: draft.batchNumber.trim(), plannedQuantity: draft.plannedQuantity, machineId: draft.machineId.trim() || undefined, inputs: [{ lotId: draft.inputLotId, reservationId: reservation.id, quantity: draft.inputQuantity }] }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => { this.productionBatchBusy = false; this.productionBatchDraft = { ...draft, batchNumber: "", plannedQuantity: 0, inputQuantity: 0 }; this.load(); },
        error: (error) => { this.productionBatchError = this.errors.message(error, "customerPortal.productionBatchSaveFailed"); this.productionBatchBusy = false; this.cdr.markForCheck(); },
      }),
      error: (error) => { this.productionBatchError = this.errors.message(error, "customerPortal.reservationSaveFailed"); this.productionBatchBusy = false; this.cdr.markForCheck(); },
    });
  }

  ngOnInit(): void {
    this.tenantContext.activeTenantLabel$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((label) => {
        this.tenantLabel = label;
        this.cdr.markForCheck();
      });

    this.tenantContext.activeTenantKey$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load());
  }

  load(): void {
    this.loading = true;
    this.error = "";
    this.manufacturingApi.getLots({ disposition: "Released", limit: 100 }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (lots) => { this.lots = lots ?? []; this.cdr.markForCheck(); }, error: (error) => { this.productionBatchError = this.errors.message(error, "customerPortal.lotsLoadFailed"); this.cdr.markForCheck(); } });
    this.manufacturingApi.getRecipes(undefined, true).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (recipes) => { this.recipes = recipes ?? []; this.cdr.markForCheck(); }, error: (error) => { this.productionOrderError = this.errors.message(error, "customerPortal.recipesLoadFailed"); this.cdr.markForCheck(); } });
    this.manufacturingApi.getMachines("Available").pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (machines) => { this.machines = machines ?? []; this.cdr.markForCheck(); }, error: (error) => { this.productionBatchError = this.errors.message(error, "customerPortal.machinesLoadFailed"); this.cdr.markForCheck(); } });
    this.manufacturingApi
      .getProductionOrders()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (orders) => {
          this.orders = orders ?? [];
          this.manufacturingApi
            .getProductionBatches()
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: (batches) => {
                this.batches = batches ?? [];
                this.loading = false;
                this.cdr.markForCheck();
              },
              error: (error) => this.fail(error),
            });
        },
        error: (error) => this.fail(error),
      });
  }

  releaseOrder(order: HisHopeProductionOrderDto): void {
    this.manufacturingApi
      .releaseProductionOrder(order.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.orders = this.orders.map((candidate) =>
            candidate.id === updated.id ? updated : candidate,
          );
          this.cdr.markForCheck();
        },
        error: (error) => {
          this.productionOrderError = this.errors.message(error, "customerPortal.releaseOrderFailed");
          this.cdr.markForCheck();
        },
      });
  }

  cancelOrder(order: HisHopeProductionOrderDto): void {
    this.productionOrderBusy = true;
    this.manufacturingApi.cancelProductionOrder(order.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (updated) => { this.orders = this.orders.map((item) => item.id === updated.id ? updated : item); this.productionOrderBusy = false; this.cdr.markForCheck(); },
      error: (error) => { this.productionOrderError = this.errors.message(error, "customerPortal.cancelOrderFailed"); this.productionOrderBusy = false; this.cdr.markForCheck(); },
    });
  }

  startBatch(batch: HisHopeProductionBatchDto): void {
    this.batchActionId = batch.id;
    this.batchActionError = "";
    this.manufacturingApi
      .startProductionBatch(batch.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.batches = this.batches.map((candidate) =>
            candidate.id === updated.id ? updated : candidate,
          );
          this.batchActionId = null;
          this.cdr.markForCheck();
        },
        error: (error) => { this.batchActionError = this.errors.message(error, "customerPortal.batchTransitionFailed"); this.batchActionId = null; this.cdr.markForCheck(); },
      });
  }

  transitionBatch(batch: HisHopeProductionBatchDto, action: "pause" | "resume" | "complete"): void {
    this.batchActionId = batch.id;
    this.batchActionError = "";
    const request = action === "pause" ? this.manufacturingApi.pauseProductionBatch(batch.id) : action === "resume" ? this.manufacturingApi.resumeProductionBatch(batch.id) : this.manufacturingApi.completeProductionBatch(batch.id);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (updated) => { this.batches = this.batches.map((candidate) => candidate.id === updated.id ? updated : candidate); this.batchActionId = null; this.cdr.markForCheck(); },
      error: (error) => { this.batchActionError = this.errors.message(error, "customerPortal.batchTransitionFailed"); this.batchActionId = null; this.cdr.markForCheck(); },
    });
  }

  cancelBatch(batch: HisHopeProductionBatchDto): void {
    this.batchActionId = batch.id;
    this.manufacturingApi.cancelProductionBatch(batch.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (updated) => { this.batches = this.batches.map((item) => item.id === updated.id ? updated : item); this.batchActionId = null; this.cdr.markForCheck(); },
      error: (error) => { this.batchActionError = this.errors.message(error, "customerPortal.cancelBatchFailed"); this.batchActionId = null; this.cdr.markForCheck(); },
    });
  }

  startOperationEntry(batch: HisHopeProductionBatchDto): void {
    this.operationBatchId = batch.id;
    this.operationError = "";
    this.operationDraft = { sequence: (batch.operations?.length ?? 0) + 1, processStep: "", operator: "", inputQuantity: batch.operations?.at(-1)?.outputQuantity ?? batch.plannedQuantity, outputQuantity: 0, qcStatus: "Pending" };
    this.cdr.markForCheck();
  }

  cancelOperationEntry(): void {
    if (this.operationBusy) return;
    this.operationBatchId = null;
    this.operationError = "";
  }

  recordOperation(batch: HisHopeProductionBatchDto): void {
    if (!this.operationDraft.processStep.trim() || !this.operationDraft.operator.trim() || this.operationDraft.inputQuantity <= 0 || this.operationDraft.outputQuantity < 0) {
      this.operationError = this.i18n.t("customerPortal.operationFormInvalid", "Process step, operator and valid quantities are required.");
      return;
    }
    this.operationBusy = true;
    this.operationError = "";
    this.manufacturingApi.recordProductionOperation(batch.id, { ...this.operationDraft, processStep: this.operationDraft.processStep.trim(), operator: this.operationDraft.operator.trim(), startedAt: new Date().toISOString() }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (operation) => { this.batches = this.batches.map((candidate) => candidate.id === batch.id ? { ...candidate, operations: [...(candidate.operations ?? []), operation] } : candidate); this.operationBatchId = null; this.operationBusy = false; this.cdr.markForCheck(); },
      error: (error) => { this.operationError = this.errors.message(error, "customerPortal.operationSaveFailed"); this.operationBusy = false; this.cdr.markForCheck(); },
    });
  }

  setReviewer(operationId: string, reviewer: string): void {
    this.reviewerDrafts[operationId] = reviewer;
  }

  setReviewNotes(operationId: string, notes: string): void {
    this.notesDrafts[operationId] = notes;
  }

  isLossReviewed(operationId: string): boolean {
    return this.reviewedLossOperations.has(operationId);
  }

  reviewLoss(
    batch: HisHopeProductionBatchDto,
    operation: HisHopeOperationExecutionDto,
    decision: "Approved" | "Rejected",
  ): void {
    const reviewer = (this.reviewerDrafts[operation.id] ?? "").trim();
    if (!reviewer) {
      this.reviewError = this.i18n.t(
        "customerPortal.lossReviewerRequired",
        "Reviewer is required.",
      );
      this.cdr.markForCheck();
      return;
    }
    this.reviewingOperationId = operation.id;
    this.reviewError = "";
    this.manufacturingApi
      .reviewLoss(batch.id, operation.id, {
        decision,
        reviewer,
        notes: (this.notesDrafts[operation.id] ?? "").trim() || undefined,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.reviewedLossOperations.add(operation.id);
          this.reviewingOperationId = null;
          this.cdr.markForCheck();
        },
        error: (error) => {
          this.reviewingOperationId = null;
          this.reviewError = this.errors.message(error, "customerPortal.lossReviewFailed");
          this.cdr.markForCheck();
        },
      });
  }

  private fail(error?: unknown): void {
    this.error = this.errors.message(error, "customerPortal.productionLoadFailed");
    this.loading = false;
    this.cdr.markForCheck();
  }
}
