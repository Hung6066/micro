import { CurrencyPipe, DatePipe, DecimalPipe } from "@angular/common";
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
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
  HisHopeActionButtonComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopePurchaseOrderDto,
  HisHopeSupplierDto,
  HisHopeManufacturingMaterialRequirementDto,
  HisHopePurchaseOrderLineDto,
  HisHopeInboundReceiptDto,
  HisHopeFacilityDto,
  HisHopeUomDto,
  HisHopeMaterialDto,
  HisHopeSupplierRfqDto,
} from "@his-hope/frontend-foundation/contracts";
import { ManufacturingApiService } from "../../core/services/manufacturing-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { HisHopeApiErrorMessageService as ApiErrorMessageService } from "@his-hope/frontend-foundation/i18n";
import { portalEnumLabel } from "../../core/utils/portal-label.util";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    FormsModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeActionButtonComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'customerPortal.procurementTitle' | hhTranslate: 'Procurement'"
        [subtitle]="pageSubtitle"
      />
      @if (loading) {
        <hh-state
          kind="loading"
          [message]="'customerPortal.loadingProcurement' | hhTranslate: 'Loading procurement data…'"
        />
      } @else if (error) {
        <hh-state kind="error" [message]="error" />
      } @else {
        <section class="section">
          <h2>{{ "customerPortal.materialRequirements" | hhTranslate: "Material requirements" }}</h2>
          @if (!materialRequirements.length) {
            <p class="empty">{{ "customerPortal.noMaterialShortages" | hhTranslate: "No open material shortages." }}</p>
          } @else {
            <div class="cards">
              @for (requirement of materialRequirements; track requirement.materialSku + requirement.productionOrderId) {
                <article class="card" [class.shortage]="requirement.shortageQuantity > 0">
                  <header>
                    <strong>{{ requirement.materialSku }}</strong>
                    <span class="status">{{ requirement.orderNumber }}</span>
                  </header>
                  <p>
                    {{
                      "customerPortal.materialRequirementLine"
                        | hhTranslate
                          : "Required {{required}} {{uom}} · Available {{available}} · Shortage {{shortage}}"
                          : {
                              required: (requirement.requiredQuantity | number: "1.0-2") ?? "",
                              uom: requirement.uom,
                              available: (requirement.availableQuantity | number: "1.0-2") ?? "",
                              shortage: (requirement.shortageQuantity | number: "1.0-2") ?? "",
                            }
                    }}
                  </p>
                </article>
              }
            </div>
          }
        </section>

        <section class="section">
          <div class="section-heading"><h2>{{ "customerPortal.facilities" | hhTranslate: "Facilities" }}</h2><hh-action-button kind="secondary" icon="add_home_work" [label]="'customerPortal.addFacility' | hhTranslate: 'Add facility'" (pressed)="startFacility()" /></div>
          @if (facilityDraft) {
            <form class="supplier-form" (ngSubmit)="saveFacility()">
              <label>{{ "customerPortal.facilityCode" | hhTranslate: "Code" }}<input name="facilityCode" [(ngModel)]="facilityDraft.code" required /></label>
              <label>{{ "customerPortal.facilityName" | hhTranslate: "Name" }}<input name="facilityName" [(ngModel)]="facilityDraft.name" required /></label>
              <div class="receipt-actions"><hh-action-button kind="primary" icon="save" type="submit" [label]="'common.save' | hhTranslate: 'Save'" [disabled]="facilityBusy" /><hh-action-button kind="secondary" icon="close" type="button" [label]="'common.cancel' | hhTranslate: 'Cancel'" [disabled]="facilityBusy" (pressed)="facilityDraft = null" /></div>
            </form>
            @if (facilityError) { <p class="error">{{ facilityError }}</p> }
          }
          @if (!facilities.length) { <p class="empty">{{ "customerPortal.noFacilities" | hhTranslate: "No facilities." }}</p> } @else { <ul class="list">@for (facility of facilities; track facility.id) { <li><strong>{{ facility.code }}</strong> — {{ facility.name }}</li> }</ul> }
        </section>

        <section class="section">
          <div class="section-heading"><h2>{{ "customerPortal.masterData" | hhTranslate: "Material and UOM master data" }}</h2><div class="receipt-actions"><hh-action-button kind="secondary" icon="straighten" [label]="'customerPortal.addUom' | hhTranslate: 'Add UOM'" (pressed)="startUom()" /><hh-action-button kind="secondary" icon="inventory_2" [label]="'customerPortal.addMaterial' | hhTranslate: 'Add material'" (pressed)="startMaterial()" /></div></div>
          @if (uomDraft) { <form class="supplier-form" (ngSubmit)="saveUom()"><label>{{ "customerPortal.uomCode" | hhTranslate: "UOM code" }}<input name="uomCode" [(ngModel)]="uomDraft.code" required /></label><label>{{ "customerPortal.uomName" | hhTranslate: "UOM name" }}<input name="uomName" [(ngModel)]="uomDraft.name" required /></label><label>{{ "customerPortal.uomDimension" | hhTranslate: "Dimension" }}<input name="uomDimension" [(ngModel)]="uomDraft.dimension" required /></label><div class="receipt-actions"><hh-action-button kind="primary" icon="save" type="submit" [label]="'common.save' | hhTranslate: 'Save'" [disabled]="masterDataBusy" /></div></form> }
          @if (materialDraft) { <form class="supplier-form" (ngSubmit)="saveMaterial()"><label>{{ "customerPortal.materialSku" | hhTranslate: "Material SKU" }}<input name="masterMaterialSku" [(ngModel)]="materialDraft.sku" required /></label><label>{{ "customerPortal.materialName" | hhTranslate: "Material name" }}<input name="materialName" [(ngModel)]="materialDraft.name" required /></label><label>{{ "customerPortal.baseUom" | hhTranslate: "Base UOM" }}<select name="baseUom" [(ngModel)]="materialDraft.baseUomCode" required><option value="">{{ "customerPortal.selectUom" | hhTranslate: "Select UOM" }}</option>@for (uom of uoms; track uom.code) { <option [value]="uom.code">{{ uom.code }} · {{ uom.name }}</option> }</select></label><div class="receipt-actions"><hh-action-button kind="primary" icon="save" type="submit" [label]="'common.save' | hhTranslate: 'Save'" [disabled]="masterDataBusy" /></div></form> }
          @if (masterDataError) { <p class="error">{{ masterDataError }}</p> } @if (!materials.length) { <p class="empty">{{ "customerPortal.noMaterials" | hhTranslate: "No materials." }}</p> } @else { <ul class="list">@for (material of materials; track material.id) { <li><strong>{{ material.sku }}</strong> — {{ material.name }} ({{ material.baseUomCode }})</li> }</ul> }
        </section>

        <section class="section">
          <div class="section-heading"><h2>{{ "customerPortal.suppliers" | hhTranslate: "Suppliers" }}</h2><hh-action-button kind="secondary" icon="add_business" [label]="'customerPortal.addSupplier' | hhTranslate: 'Add supplier'" (pressed)="startSupplier()" /></div>
          @if (supplierDraft) {
            <form class="supplier-form" (ngSubmit)="saveSupplier()">
              <label>{{ "customerPortal.supplierCode" | hhTranslate: "Code" }}<input name="supplierCode" [(ngModel)]="supplierDraft.code" required /></label>
              <label>{{ "customerPortal.supplierName" | hhTranslate: "Name" }}<input name="supplierName" [(ngModel)]="supplierDraft.name" required /></label>
              <label class="checkbox"><input name="supplierActive" type="checkbox" [(ngModel)]="supplierDraft.active" /> {{ "customerPortal.supplierActive" | hhTranslate: "Active" }}</label>
              <div class="receipt-actions"><hh-action-button kind="primary" icon="save" type="submit" [label]="'common.save' | hhTranslate: 'Save'" [disabled]="supplierBusy" /><hh-action-button kind="secondary" icon="close" type="button" [label]="'common.cancel' | hhTranslate: 'Cancel'" [disabled]="supplierBusy" (pressed)="supplierDraft = null" /></div>
            </form>
            @if (supplierError) { <p class="error">{{ supplierError }}</p> }
          }
          @if (!suppliers.length) {
            <p class="empty">{{ "customerPortal.noSuppliers" | hhTranslate: "No suppliers." }}</p>
          } @else {
            <ul class="list">
              @for (supplier of suppliers; track supplier.id) {
                <li>
                  <strong>{{ supplier.code }}</strong> — {{ supplier.name }}
                  <hh-action-button kind="row" mode="icon-only" icon="edit" [label]="'common.edit' | hhTranslate: 'Edit'" (pressed)="editSupplier(supplier)" />
                  @if (!supplier.active) {
                    <span class="inactive">{{
                      "customerPortal.supplierInactive" | hhTranslate: "inactive"
                    }}</span>
                  }
                </li>
              }
            </ul>
          }
        </section>

        <section class="section">
          <div class="section-heading"><h2>{{ "customerPortal.supplierRfqs" | hhTranslate: "Supplier RFQs" }}</h2><hh-action-button kind="secondary" icon="request_quote" [label]="'customerPortal.addSupplierRfq' | hhTranslate: 'Create RFQ'" (pressed)="startSupplierRfq()" /></div>
          @if (supplierRfqDraft) { <form class="supplier-form" (ngSubmit)="saveSupplierRfq()"><label>{{ "customerPortal.rfqNumber" | hhTranslate: "RFQ number" }}<input name="rfqNumber" [(ngModel)]="supplierRfqDraft.rfqNumber" required /></label><label>{{ "customerPortal.materialSku" | hhTranslate: "Material SKU" }}<input name="rfqMaterialSku" [(ngModel)]="supplierRfqDraft.materialSku" required /></label><label>{{ "customerPortal.forecastQuantity" | hhTranslate: "Quantity" }}<input name="rfqQuantity" type="number" min="0.001" [(ngModel)]="supplierRfqDraft.quantity" required /></label><label>{{ "customerPortal.forecastUom" | hhTranslate: "UOM" }}<input name="rfqUom" [(ngModel)]="supplierRfqDraft.uom" required /></label><div class="receipt-actions"><hh-action-button kind="primary" icon="save" type="submit" [label]="'common.save' | hhTranslate: 'Save'" [disabled]="supplierRfqBusy" /></div></form> }
          @if (supplierRfqError) { <p class="error">{{ supplierRfqError }}</p> }
          @if (!supplierRfqs.length) { <p class="empty">{{ "customerPortal.noSupplierRfqs" | hhTranslate: "No supplier RFQs." }}</p> } @else { <ul class="list">@for (rfq of supplierRfqs; track rfq.id) { <li><strong>{{ rfq.rfqNumber }}</strong> — {{ rfq.materialSku }} · {{ rfq.quantity }} {{ rfq.uom }} <span class="status">{{ rfq.status }}</span> <button type="button" class="link-button" (click)="startSupplierQuotation(rfq.id)">+ {{ "customerPortal.addQuotation" | hhTranslate: "quotation" }}</button></li> }</ul> }
          @if (supplierQuotationDraft) { <form class="supplier-form" (ngSubmit)="saveSupplierQuotation()"><label>{{ "customerPortal.supplier" | hhTranslate: "Supplier" }}<select name="quotationSupplier" [(ngModel)]="supplierQuotationDraft.supplierId" required><option value="">{{ "customerPortal.selectSupplier" | hhTranslate: "Select supplier" }}</option>@for (supplier of suppliers; track supplier.id) { <option [value]="supplier.id">{{ supplier.code }} · {{ supplier.name }}</option> }</select></label><label>{{ "customerPortal.unitPrice" | hhTranslate: "Unit price" }}<input name="quotationPrice" type="number" min="0" [(ngModel)]="supplierQuotationDraft.unitPrice" required /></label><label>{{ "customerPortal.leadTimeDays" | hhTranslate: "Lead time (days)" }}<input name="quotationLeadTime" type="number" min="0" [(ngModel)]="supplierQuotationDraft.leadTimeDays" required /></label><div class="receipt-actions"><hh-action-button kind="primary" icon="save" type="submit" [label]="'common.save' | hhTranslate: 'Save'" [disabled]="supplierQuotationBusy" /></div></form> }
        </section>

        <section class="section create-po-panel">
          <h2>{{ "customerPortal.createPurchaseOrder" | hhTranslate: "Create purchase order" }}</h2>
          <form class="receipt-form" (ngSubmit)="createPurchaseOrder()">
            <label>{{ "customerPortal.supplier" | hhTranslate: "Supplier" }}
              <select name="supplierId" [(ngModel)]="purchaseOrderDraft.supplierId" required>
                <option value="">{{ "customerPortal.selectSupplier" | hhTranslate: "Select supplier" }}</option>
                @for (supplier of suppliers; track supplier.id) { <option [value]="supplier.id">{{ supplier.code }} · {{ supplier.name }}</option> }
              </select>
            </label>
            <label>{{ "customerPortal.orderNumber" | hhTranslate: "Order number" }}<input name="orderNumber" [(ngModel)]="purchaseOrderDraft.orderNumber" required /></label>
            <label>{{ "customerPortal.expectedAt" | hhTranslate: "Expected delivery" }}<input name="expectedAt" type="date" [(ngModel)]="purchaseOrderDraft.expectedAt" /></label>
            <label>{{ "customerPortal.currency" | hhTranslate: "Currency" }}<input name="currency" [(ngModel)]="purchaseOrderDraft.currency" required /></label>
            <div class="line-editor wide">
              <div class="line-editor-heading"><strong>{{ "customerPortal.purchaseOrderLines" | hhTranslate: "Purchase order lines" }}</strong><button type="button" class="link-button" (click)="addPurchaseOrderLine()">+ {{ "customerPortal.addLine" | hhTranslate: "Add line" }}</button></div>
              @for (line of purchaseOrderLines; track $index; let index = $index) {
                <div class="line-row">
                  <input [name]="'materialSku' + index" [placeholder]="'customerPortal.materialSku' | hhTranslate: 'Material SKU'" [(ngModel)]="line.materialSku" required />
                  <input [name]="'orderedQuantity' + index" type="number" min="0.001" step="0.001" [placeholder]="'customerPortal.orderedQuantity' | hhTranslate: 'Quantity'" [(ngModel)]="line.orderedQuantity" required />
                  <input [name]="'uom' + index" [placeholder]="'customerPortal.uom' | hhTranslate: 'UOM'" [(ngModel)]="line.uom" required />
                  <input [name]="'unitPrice' + index" type="number" min="0" step="0.01" [placeholder]="'customerPortal.unitPrice' | hhTranslate: 'Unit price'" [(ngModel)]="line.unitPrice" required />
                  @if (purchaseOrderLines.length > 1) { <button type="button" class="link-button danger" (click)="removePurchaseOrderLine(index)">×</button> }
                </div>
              }
            </div>
            <div class="receipt-actions"><hh-action-button kind="primary" icon="add_shopping_cart" type="submit" [label]="'customerPortal.createPurchaseOrder' | hhTranslate: 'Create purchase order'" [disabled]="purchaseOrderBusy" /></div>
          </form>
          @if (purchaseOrderError) { <p class="error">{{ purchaseOrderError }}</p> }
        </section>

        <section class="section">
          <h2>{{ "customerPortal.purchaseOrders" | hhTranslate: "Purchase orders" }}</h2>
          @if (!purchaseOrders.length) {
            <p class="empty">{{ "customerPortal.noPurchaseOrders" | hhTranslate: "No purchase orders." }}</p>
          } @else {
            <div class="cards">
              @for (po of purchaseOrders; track po.id) {
                <article class="card">
                  <header>
                    <strong>{{ po.orderNumber }}</strong>
                    <span class="status">{{ purchaseOrderStatusLabel(po.status) }}</span>
                  </header>
                  <p>
                    {{
                      "customerPortal.procurementSupplierLine"
                        | hhTranslate
                          : "Supplier {{code}} · {{name}} · {{date}}"
                          : {
                              code: po.supplierCode,
                              name: po.supplierName || "",
                              date: (po.orderedAt | date: "mediumDate") ?? "",
                            }
                    }}
                  </p>
                  <ul>
                    @for (line of po.lines; track line.id) {
                      <li>
                        {{ line.materialSku }} ×
                        {{ line.orderedQuantity | number: "1.0-2" }}
                        {{ line.uom }} @
                        {{ line.unitPrice | currency: po.currency }}
                        <span class="received">({{ line.receivedQuantity | number: "1.0-2" }} {{ "customerPortal.received" | hhTranslate: "received" }})</span>
                        <hh-action-button
                          kind="secondary"
                          icon="move_to_inbox"
                          [label]="'customerPortal.receiveInbound' | hhTranslate: 'Receive'"
                          (pressed)="startReceiving(po.id, line)"
                          [disabled]="(po.status !== 'Approved' && po.status !== 'PartiallyReceived') || line.receivedQuantity >= line.orderedQuantity"
                        />
                      </li>
                    }
                  </ul>
                  <div class="po-actions">
                    @if (po.status === 'Draft') { <hh-action-button kind="primary" icon="task_alt" [label]="'customerPortal.approvePurchaseOrder' | hhTranslate: 'Approve'" [disabled]="purchaseOrderBusy" (pressed)="updatePurchaseOrderStatus(po, 'Approved')" /> }
                    @if (po.status === 'Draft' || po.status === 'Approved') { <hh-action-button kind="secondary" icon="cancel" [label]="'customerPortal.cancelPurchaseOrder' | hhTranslate: 'Cancel order'" [disabled]="purchaseOrderBusy" (pressed)="updatePurchaseOrderStatus(po, 'Cancelled')" /> }
                  </div>
                </article>
              }
            </div>
          }
        </section>
        <section class="section">
          <h2>{{ "customerPortal.inboundReceiptHistory" | hhTranslate: "Inbound receipt history" }}</h2>
          @if (!receipts.length) {
            <p class="empty">{{ "customerPortal.noInboundReceipts" | hhTranslate: "No inbound receipts." }}</p>
          } @else {
            <div class="receipt-history">
              @for (receipt of receipts; track receipt.id) {
                <article class="receipt-history-row">
                  <strong>{{ receipt.receiptNumber }}</strong>
                  <span>{{ receipt.supplierLotCode }} · {{ receipt.quantity | number: "1.0-2" }} {{ receipt.uom }}</span>
                  <span>{{ receipt.facilityId }} · {{ receipt.receivedAt | date: "medium" }}</span>
                  <span class="status">{{ receipt.disposition }}</span>
                </article>
              }
            </div>
          }
        </section>
        @if (receiving) {
          <section class="section receipt-panel">
            <h2>{{ "customerPortal.receiveInbound" | hhTranslate: "Receive inbound lot" }}</h2>
            <p class="meta">
              {{ receiving.materialSku }} · {{ receiving.uom }} ·
              {{ "customerPortal.receiptQuantityLimit" | hhTranslate: "Ordered {{quantity}}" : { quantity: (receiving.orderedQuantity | number: "1.0-2") ?? "" } }}
            </p>
            <form class="receipt-form" (ngSubmit)="receiveInbound()">
              <label>{{ "customerPortal.receiptNumber" | hhTranslate: "Receipt number" }}<input name="receiptNumber" [(ngModel)]="receiptDraft.receiptNumber" required /></label>
              <label>{{ "customerPortal.supplierLotCode" | hhTranslate: "Supplier lot" }}<input name="supplierLotCode" [(ngModel)]="receiptDraft.supplierLotCode" required /></label>
              <label>{{ "customerPortal.facility" | hhTranslate: "Facility" }}
                <select name="facilityId" [(ngModel)]="receiptDraft.facilityId" required>
                  <option value="">{{ "customerPortal.selectFacility" | hhTranslate: "Select facility" }}</option>
                  @for (facility of facilities; track facility.id) { <option [value]="facility.id">{{ facility.name }}</option> }
                </select>
              </label>
              <label>{{ "customerPortal.receiptQuantity" | hhTranslate: "Quantity" }}<input name="quantity" type="number" min="0.001" step="0.001" [(ngModel)]="receiptDraft.quantity" required /></label>
              <label>{{ "customerPortal.expiryDate" | hhTranslate: "Expiry date" }}<input name="expiryDate" type="date" [(ngModel)]="receiptDraft.expiryDate" /></label>
              <div class="receipt-actions">
                <hh-action-button kind="primary" icon="move_to_inbox" type="submit" [label]="'customerPortal.postReceipt' | hhTranslate: 'Post receipt'" [disabled]="receivingBusy" />
                <hh-action-button kind="secondary" icon="close" type="button" [label]="'common.cancel' | hhTranslate: 'Cancel'" [disabled]="receivingBusy" (pressed)="cancelReceiving()" />
              </div>
            </form>
            @if (receiptError) { <p class="error">{{ receiptError }}</p> }
          </section>
        }
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
      .list {
        list-style: none;
        padding: 0;
        margin: 0;
        display: grid;
        gap: var(--space-sm);
        font-size: var(--font-size-body);
      }
      .inactive {
        margin-left: var(--space-sm);
        font-size: var(--font-size-caption);
        color: var(--text-secondary);
      }
      .cards {
        display: grid;
        gap: var(--space-md);
      }
      .card {
        border: 1px solid var(--border-subtle);
        border-radius: var(--radius-card);
        padding: var(--space-md);
      }
      .card.shortage {
        border-left: 4px solid var(--color-danger);
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
      ul {
        margin: var(--space-sm) 0 0;
        padding-left: var(--space-lg);
        font-size: var(--font-size-body);
      }
      .empty {
        color: var(--text-secondary);
        font-size: var(--font-size-body);
      }
      .section-heading { display:flex; align-items:center; justify-content:space-between; gap:var(--space-md); }
      .supplier-form { display:grid; grid-template-columns:repeat(auto-fit,minmax(180px,1fr)); gap:var(--space-md); margin-bottom:var(--space-md); padding:var(--space-md); border:1px solid var(--border-subtle); border-radius:var(--radius-card); background:var(--surface-muted); }
      .checkbox { display:flex; align-items:center; gap:var(--space-xs); }
      .receipt-panel { border: 1px solid var(--border-subtle); border-radius: var(--radius-card); padding: var(--space-md); }
      .meta { color: var(--text-secondary); }
      .receipt-form { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: var(--space-md); align-items: end; }
      label { display: grid; gap: var(--space-xs); font-size: var(--font-size-caption); color: var(--text-secondary); }
      input, select { min-height: 2.5rem; padding: 0 var(--space-sm); border: 1px solid var(--border-subtle); border-radius: var(--radius-control); background: var(--surface-raised); color: var(--text-primary); font: inherit; }
      .receipt-actions { display: flex; gap: var(--space-sm); align-items: center; }
      .wide { grid-column: 1 / -1; }
      .line-editor { display: grid; gap: var(--space-sm); }
      .line-editor-heading { display: flex; align-items: center; justify-content: space-between; gap: var(--space-sm); }
      .line-row { display: grid; grid-template-columns: 2fr 1fr 1fr 1fr auto; gap: var(--space-sm); align-items: center; }
      .link-button { border: 0; background: transparent; color: var(--color-primary); cursor: pointer; font: inherit; padding: var(--space-2xs); }
      .link-button.danger { color: var(--color-danger); font-size: 1.25rem; }
      .received { color: var(--text-secondary); font-size: var(--font-size-caption); }
      .po-actions { display: flex; flex-wrap: wrap; gap: var(--space-sm); margin-top: var(--space-sm); }
      .error { color: var(--color-danger); }
      @media (max-width: 700px) { .line-row { grid-template-columns: 1fr 1fr; } .wide { grid-column: auto; } }
    `,
  ],
})
export class ProcurementPageComponent implements OnInit {
  private readonly manufacturingApi = inject(ManufacturingApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly errors = inject(ApiErrorMessageService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  loading = true;
  error = "";
  suppliers: HisHopeSupplierDto[] = [];
  supplierDraft: { id?: string; code: string; name: string; active: boolean } | null = null;
  supplierBusy = false;
  supplierError = "";
  facilities: HisHopeFacilityDto[] = [];
  facilityDraft: { code: string; name: string } | null = null;
  facilityBusy = false;
  facilityError = "";
  uoms: HisHopeUomDto[] = [];
  materials: HisHopeMaterialDto[] = [];
  supplierRfqs: HisHopeSupplierRfqDto[] = [];
  supplierRfqDraft: { rfqNumber: string; materialSku: string; quantity: number; uom: string } | null = null;
  supplierRfqBusy = false;
  supplierRfqError = "";
  supplierQuotationDraft: { rfqId: string; supplierId: string; unitPrice: number; leadTimeDays: number } | null = null;
  supplierQuotationBusy = false;
  uomDraft: { code: string; name: string; dimension: string } | null = null;
  materialDraft: { sku: string; name: string; baseUomCode: string } | null = null;
  masterDataBusy = false;
  masterDataError = "";
  purchaseOrders: HisHopePurchaseOrderDto[] = [];
  receipts: HisHopeInboundReceiptDto[] = [];
  materialRequirements: HisHopeManufacturingMaterialRequirementDto[] = [];
  tenantLabel: string | null = null;
  receiving: { purchaseOrderId: string; purchaseOrderLineId: string; materialSku: string; uom: string; orderedQuantity: number } | null = null;
  receivingBusy = false;
  receiptError = "";
  receiptDraft = { receiptNumber: "", supplierLotCode: "", facilityId: "default", quantity: 0, expiryDate: "" };
  purchaseOrderBusy = false;
  purchaseOrderError = "";
  purchaseOrderDraft = { supplierId: "", orderNumber: "", expectedAt: "", currency: "VND" };
  purchaseOrderLines = [{ materialSku: "", orderedQuantity: 0, uom: "kg", unitPrice: 0 }];

  startUom(): void { this.masterDataError = ""; this.uomDraft = { code: "", name: "", dimension: "mass" }; }
  startMaterial(): void { this.masterDataError = ""; this.materialDraft = { sku: "", name: "", baseUomCode: this.uoms[0]?.code ?? "" }; }
  saveUom(): void { const draft = this.uomDraft; if (!draft?.code.trim() || !draft.name.trim() || !draft.dimension.trim()) { this.masterDataError = this.i18n.t("customerPortal.masterDataFormInvalid", "Required master data fields are missing."); return; } this.masterDataBusy = true; this.manufacturingApi.createUom({ code: draft.code.trim(), name: draft.name.trim(), dimension: draft.dimension.trim() }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.uomDraft = null; this.masterDataBusy = false; this.load(); }, error: (error) => { this.masterDataError = this.errors.message(error, "customerPortal.masterDataSaveFailed"); this.masterDataBusy = false; this.cdr.markForCheck(); } }); }
  saveMaterial(): void { const tenantKey = this.tenantContext.getActiveTenantKey(); const draft = this.materialDraft; if (!tenantKey || !draft?.sku.trim() || !draft.name.trim() || !draft.baseUomCode) { this.masterDataError = this.i18n.t("customerPortal.masterDataFormInvalid", "Required master data fields are missing."); return; } this.masterDataBusy = true; this.manufacturingApi.createMaterial({ tenantKey, sku: draft.sku.trim(), name: draft.name.trim(), baseUomCode: draft.baseUomCode, materialType: "RawMaterial" }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.materialDraft = null; this.masterDataBusy = false; this.load(); }, error: (error) => { this.masterDataError = this.errors.message(error, "customerPortal.masterDataSaveFailed"); this.masterDataBusy = false; this.cdr.markForCheck(); } }); }

  startFacility(): void { this.facilityError = ""; this.facilityDraft = { code: "", name: "" }; }
  saveFacility(): void {
    const tenantKey = this.tenantContext.getActiveTenantKey();
    const draft = this.facilityDraft;
    if (!tenantKey || !draft || !draft.code.trim() || !draft.name.trim()) { this.facilityError = this.i18n.t("customerPortal.facilityFormInvalid", "Facility code and name are required."); return; }
    this.facilityBusy = true; this.facilityError = "";
    this.manufacturingApi.createFacility({ tenantKey, code: draft.code.trim(), name: draft.name.trim(), active: true }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.facilityDraft = null; this.facilityBusy = false; this.load(); }, error: (error) => { this.facilityError = this.errors.message(error, "customerPortal.facilitySaveFailed"); this.facilityBusy = false; this.cdr.markForCheck(); } });
  }

  startSupplier(): void { this.supplierError = ""; this.supplierDraft = { code: "", name: "", active: true }; }
  startSupplierRfq(): void { this.supplierRfqError = ""; this.supplierRfqDraft = { rfqNumber: `RFQ-${Date.now()}`, materialSku: "", quantity: 0, uom: "kg" }; }
  saveSupplierRfq(): void { const tenantKey = this.tenantContext.getActiveTenantKey(); const draft = this.supplierRfqDraft; if (!tenantKey || !draft || !draft.rfqNumber.trim() || !draft.materialSku.trim() || draft.quantity <= 0 || !draft.uom.trim()) { this.supplierRfqError = this.i18n.t("customerPortal.rfqFormInvalid", "RFQ fields are required."); return; } this.supplierRfqBusy = true; this.manufacturingApi.createSupplierRfq({ tenantKey, rfqNumber: draft.rfqNumber.trim(), materialSku: draft.materialSku.trim(), quantity: draft.quantity, uom: draft.uom.trim() }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rfq => { this.supplierRfqs = [rfq, ...this.supplierRfqs]; this.supplierRfqDraft = null; this.supplierRfqBusy = false; this.cdr.markForCheck(); }, error: error => { this.supplierRfqError = this.errors.message(error, "customerPortal.supplierRfqSaveFailed"); this.supplierRfqBusy = false; this.cdr.markForCheck(); } }); }
  startSupplierQuotation(rfqId: string): void { this.supplierQuotationDraft = { rfqId, supplierId: "", unitPrice: 0, leadTimeDays: 0 }; }
  saveSupplierQuotation(): void { const draft = this.supplierQuotationDraft; if (!draft?.supplierId || draft.unitPrice < 0 || draft.leadTimeDays < 0) return; this.supplierQuotationBusy = true; this.manufacturingApi.createSupplierQuotation(draft.rfqId, { supplierRfqId: draft.rfqId, supplierId: draft.supplierId, unitPrice: draft.unitPrice, currency: "VND", leadTimeDays: draft.leadTimeDays }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.supplierQuotationDraft = null; this.supplierQuotationBusy = false; this.cdr.markForCheck(); }, error: error => { this.supplierRfqError = this.errors.message(error, "customerPortal.supplierQuotationSaveFailed"); this.supplierQuotationBusy = false; this.cdr.markForCheck(); } }); }
  editSupplier(supplier: HisHopeSupplierDto): void { this.supplierError = ""; this.supplierDraft = { id: supplier.id, code: supplier.code, name: supplier.name, active: supplier.active }; }
  saveSupplier(): void {
    const tenantKey = this.tenantContext.getActiveTenantKey();
    const draft = this.supplierDraft;
    if (!tenantKey || !draft || !draft.code.trim() || !draft.name.trim()) { this.supplierError = this.i18n.t("customerPortal.supplierFormInvalid", "Supplier code and name are required."); return; }
    this.supplierBusy = true; this.supplierError = "";
    const request = draft.id ? this.manufacturingApi.updateSupplier(draft.id, { code: draft.code.trim(), name: draft.name.trim(), active: draft.active }) : this.manufacturingApi.createSupplier({ tenantKey, code: draft.code.trim(), name: draft.name.trim(), active: draft.active });
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.supplierDraft = null; this.supplierBusy = false; this.load(); }, error: (error) => { this.supplierError = this.errors.message(error, "customerPortal.supplierSaveFailed"); this.supplierBusy = false; this.cdr.markForCheck(); } });
  }

  get pageSubtitle(): string {
    this.i18n.locale();
    return this.i18n.t("customerPortal.tenantScope", "Tenant: {{tenant}}", {
      tenant:
        this.tenantLabel ??
        this.i18n.t("customerPortal.tenantUnknown", "—"),
    });
  }

  purchaseOrderStatusLabel(status: string): string {
    return portalEnumLabel(this.i18n, "purchaseOrderStatus", status);
  }

  addPurchaseOrderLine(): void { this.purchaseOrderLines = [...this.purchaseOrderLines, { materialSku: "", orderedQuantity: 0, uom: "kg", unitPrice: 0 }]; }
  removePurchaseOrderLine(index: number): void { if (this.purchaseOrderLines.length > 1) this.purchaseOrderLines = this.purchaseOrderLines.filter((_, candidate) => candidate !== index); }

  createPurchaseOrder(): void {
    const tenantKey = this.tenantContext.getActiveTenantKey();
    const draft = this.purchaseOrderDraft;
    if (!tenantKey || !draft.supplierId || !draft.orderNumber.trim() || !draft.currency.trim() || !this.purchaseOrderLines.length || this.purchaseOrderLines.some((line) => !line.materialSku.trim() || !line.uom.trim() || line.orderedQuantity <= 0 || line.unitPrice < 0)) {
      this.purchaseOrderError = this.i18n.t("customerPortal.purchaseOrderFormInvalid", "Tenant, supplier, order number, lines and valid quantities are required.");
      return;
    }
    this.purchaseOrderBusy = true;
    this.purchaseOrderError = "";
    this.manufacturingApi.createPurchaseOrder({
      tenantKey,
      supplierId: draft.supplierId,
      orderNumber: draft.orderNumber.trim(),
      currency: draft.currency.trim().toUpperCase(),
      status: "Draft",
      expectedAt: draft.expectedAt ? `${draft.expectedAt}T00:00:00Z` : undefined,
      lines: this.purchaseOrderLines.map((line) => ({ materialSku: line.materialSku.trim(), orderedQuantity: line.orderedQuantity, uom: line.uom.trim(), unitPrice: line.unitPrice })),
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.purchaseOrderBusy = false; this.purchaseOrderDraft = { ...draft, orderNumber: "", expectedAt: "" }; this.purchaseOrderLines = [{ materialSku: "", orderedQuantity: 0, uom: "kg", unitPrice: 0 }]; this.load(); },
      error: (error) => { this.purchaseOrderError = this.errors.message(error, "customerPortal.purchaseOrderSaveFailed"); this.purchaseOrderBusy = false; this.cdr.markForCheck(); },
    });
  }

  updatePurchaseOrderStatus(order: HisHopePurchaseOrderDto, status: string): void {
    this.purchaseOrderBusy = true;
    this.purchaseOrderError = "";
    this.manufacturingApi.updatePurchaseOrderStatus(order.id, status).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (updated) => { this.purchaseOrders = this.purchaseOrders.map((candidate) => candidate.id === updated.id ? updated : candidate); this.purchaseOrderBusy = false; this.cdr.markForCheck(); },
      error: (error) => { this.purchaseOrderError = this.errors.message(error, "customerPortal.purchaseOrderStatusFailed"); this.purchaseOrderBusy = false; this.cdr.markForCheck(); },
    });
  }

  startReceiving(purchaseOrderId: string, line: HisHopePurchaseOrderLineDto): void {
    this.receiving = { purchaseOrderId, purchaseOrderLineId: line.id, materialSku: line.materialSku, uom: line.uom, orderedQuantity: line.orderedQuantity - line.receivedQuantity };
    this.receiptDraft = { receiptNumber: "", supplierLotCode: "", facilityId: "default", quantity: Math.max(0, line.orderedQuantity - line.receivedQuantity), expiryDate: "" };
    this.receiptError = "";
    this.cdr.markForCheck();
  }

  cancelReceiving(): void {
    if (this.receivingBusy) return;
    this.receiving = null;
    this.receiptError = "";
  }

  receiveInbound(): void {
    if (!this.receiving) return;
    if (!this.receiptDraft.receiptNumber.trim() || !this.receiptDraft.supplierLotCode.trim() || !this.receiptDraft.facilityId.trim() || this.receiptDraft.quantity <= 0 || this.receiptDraft.quantity > this.receiving.orderedQuantity) {
      this.receiptError = this.i18n.t("customerPortal.receiptFormInvalid", "Receipt number, supplier lot, facility and a valid quantity are required.");
      return;
    }
    this.receivingBusy = true;
    this.receiptError = "";
    this.manufacturingApi.receiveInboundLot(this.receiving.purchaseOrderId, {
      purchaseOrderId: this.receiving.purchaseOrderId,
      purchaseOrderLineId: this.receiving.purchaseOrderLineId,
      materialSku: this.receiving.materialSku,
      receiptNumber: this.receiptDraft.receiptNumber.trim(),
      supplierLotCode: this.receiptDraft.supplierLotCode.trim(),
      facilityId: this.receiptDraft.facilityId.trim(),
      quantity: this.receiptDraft.quantity,
      expiryDate: this.receiptDraft.expiryDate || undefined,
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.receivingBusy = false; this.receiving = null; this.load(); },
      error: (error) => { this.receiptError = this.errors.message(error, "customerPortal.receiptSaveFailed"); this.receivingBusy = false; this.cdr.markForCheck(); },
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
    this.manufacturingApi.getUoms().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.uoms = items ?? []; this.cdr.markForCheck(); }, error: () => { this.uoms = []; this.cdr.markForCheck(); } });
    this.manufacturingApi.getMaterials().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.materials = items ?? []; this.cdr.markForCheck(); }, error: () => { this.materials = []; this.cdr.markForCheck(); } });
    this.manufacturingApi.getSupplierRfqs().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.supplierRfqs = items ?? []; this.cdr.markForCheck(); }, error: () => { this.supplierRfqs = []; this.cdr.markForCheck(); } });
    this.manufacturingApi.getFacilities().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (facilities) => { this.facilities = facilities ?? []; if (this.facilities.length && (!this.receiptDraft.facilityId || this.receiptDraft.facilityId === "default")) this.receiptDraft.facilityId = this.facilities[0].id; this.cdr.markForCheck(); },
      error: () => { this.facilities = []; this.cdr.markForCheck(); },
    });
    this.manufacturingApi
      .getSuppliers()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (suppliers) => {
          this.suppliers = suppliers ?? [];
          this.manufacturingApi
            .getPurchaseOrders()
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
      next: (orders) => {
        this.purchaseOrders = orders ?? [];
                this.manufacturingApi
                  .getMaterialRequirements()
                  .pipe(takeUntilDestroyed(this.destroyRef))
                  .subscribe({
                    next: (requirements) => {
                      this.materialRequirements = (requirements ?? []).filter((item) => item.shortageQuantity > 0);
                      this.manufacturingApi.getInboundReceipts().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
                        next: (receipts) => { this.receipts = receipts ?? []; this.loading = false; this.cdr.markForCheck(); },
                        error: (error) => this.fail(error),
                      });
                    },
                    error: (error) => this.fail(error),
                  });
              },
              error: (error) => this.fail(error),
            });
        },
        error: (error) => this.fail(error),
      });
  }

  private fail(error?: unknown): void {
    this.error = this.errors.message(error, "customerPortal.procurementLoadFailed");
    this.loading = false;
    this.cdr.markForCheck();
  }
}
