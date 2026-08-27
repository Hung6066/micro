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
  HisHopeTabsComponent,
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
  HisHopeSupplierQuotationDto,
  HisHopeSupplierCertificateDto,
  HisHopeSupplierMaterialApprovalDto,
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
    HisHopeTabsComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'customerPortal.procurementTitle' | hhTranslate: 'Procurement'"
        [subtitle]="pageSubtitle"
      />
      <hh-tabs
        class="procurement-nav"
        [attr.data-active-tab]="activeTab"
        [attr.aria-label]="'customerPortal.procurementWorkflow' | hhTranslate: 'Procurement workflow'"
      >
        <button role="tab" type="button" [attr.aria-selected]="activeTab === 'requirements'" [class.active]="activeTab === 'requirements'" (click)="selectTab('requirements')">{{ "customerPortal.materialRequirements" | hhTranslate: "Material requirements" }}</button>
        <button role="tab" type="button" [attr.aria-selected]="activeTab === 'facilities'" [class.active]="activeTab === 'facilities'" (click)="selectTab('facilities')">{{ "customerPortal.facilities" | hhTranslate: "Facilities" }}</button>
        <button role="tab" type="button" [attr.aria-selected]="activeTab === 'master-data'" [class.active]="activeTab === 'master-data'" (click)="selectTab('master-data')">{{ "customerPortal.masterData" | hhTranslate: "Material and UOM master data" }}</button>
        <button role="tab" type="button" [attr.aria-selected]="activeTab === 'suppliers'" [class.active]="activeTab === 'suppliers'" (click)="selectTab('suppliers')">{{ "customerPortal.suppliers" | hhTranslate: "Suppliers" }}</button>
        <button role="tab" type="button" [attr.aria-selected]="activeTab === 'rfqs'" [class.active]="activeTab === 'rfqs'" (click)="selectTab('rfqs')">{{ "customerPortal.supplierRfqs" | hhTranslate: "Supplier RFQs" }}</button>
        <button role="tab" type="button" [attr.aria-selected]="activeTab === 'purchase-orders'" [class.active]="activeTab === 'purchase-orders'" (click)="selectTab('purchase-orders')">{{ "customerPortal.purchaseOrders" | hhTranslate: "Purchase orders" }}</button>
        <button role="tab" type="button" [attr.aria-selected]="activeTab === 'inbound-receipts'" [class.active]="activeTab === 'inbound-receipts'" (click)="selectTab('inbound-receipts')">{{ "customerPortal.inboundReceiptHistory" | hhTranslate: "Inbound receipt history" }}</button>
      </hh-tabs>
      @if (loading) {
        <hh-state
          kind="loading"
          [message]="'customerPortal.loadingProcurement' | hhTranslate: 'Loading procurement data…'"
        />
      } @else if (error) {
        <hh-state kind="error" [message]="error" />
      } @else {
        <section class="section" id="requirements">
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

        <section class="section" id="facilities">
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

        <section class="section" id="master-data">
          <div class="section-heading"><h2>{{ "customerPortal.masterData" | hhTranslate: "Material and UOM master data" }}</h2><div class="receipt-actions"><hh-action-button kind="secondary" icon="straighten" [label]="'customerPortal.addUom' | hhTranslate: 'Add UOM'" (pressed)="startUom()" /><hh-action-button kind="secondary" icon="inventory_2" [label]="'customerPortal.addMaterial' | hhTranslate: 'Add material'" (pressed)="startMaterial()" /></div></div>
          @if (uomDraft) { <form class="supplier-form" (ngSubmit)="saveUom()"><label>{{ "customerPortal.uomCode" | hhTranslate: "UOM code" }}<input name="uomCode" [(ngModel)]="uomDraft.code" required /></label><label>{{ "customerPortal.uomName" | hhTranslate: "UOM name" }}<input name="uomName" [(ngModel)]="uomDraft.name" required /></label><label>{{ "customerPortal.uomDimension" | hhTranslate: "Dimension" }}<input name="uomDimension" [(ngModel)]="uomDraft.dimension" required /></label><div class="receipt-actions"><hh-action-button kind="primary" icon="save" type="submit" [label]="'common.save' | hhTranslate: 'Save'" [disabled]="masterDataBusy" /></div></form> }
          @if (materialDraft) { <form class="supplier-form" (ngSubmit)="saveMaterial()"><label>{{ "customerPortal.materialSku" | hhTranslate: "Material SKU" }}<input name="masterMaterialSku" [(ngModel)]="materialDraft.sku" required /></label><label>{{ "customerPortal.materialName" | hhTranslate: "Material name" }}<input name="materialName" [(ngModel)]="materialDraft.name" required /></label><label>{{ "customerPortal.baseUom" | hhTranslate: "Base UOM" }}<select name="baseUom" [(ngModel)]="materialDraft.baseUomCode" required><option value="">{{ "customerPortal.selectUom" | hhTranslate: "Select UOM" }}</option>@for (uom of uoms; track uom.code) { <option [value]="uom.code">{{ uom.code }} · {{ uom.name }}</option> }</select></label><div class="receipt-actions"><hh-action-button kind="primary" icon="save" type="submit" [label]="'common.save' | hhTranslate: 'Save'" [disabled]="masterDataBusy" /></div></form> }
          @if (masterDataError) { <p class="error">{{ masterDataError }}</p> } @if (!materials.length) { <p class="empty">{{ "customerPortal.noMaterials" | hhTranslate: "No materials." }}</p> } @else { <ul class="list">@for (material of materials; track material.id) { <li><strong>{{ material.sku }}</strong> — {{ material.name }} ({{ material.baseUomCode }})</li> }</ul> }
        </section>

        <section class="section" id="suppliers">
          <div class="section-heading"><h2>{{ "customerPortal.suppliers" | hhTranslate: "Suppliers" }}</h2><hh-action-button kind="secondary" icon="add_business" [label]="'customerPortal.addSupplier' | hhTranslate: 'Add supplier'" (pressed)="startSupplier()" /></div>
          @if (supplierDraft) {
            <form class="supplier-form" (ngSubmit)="saveSupplier()">
              <label>{{ "customerPortal.supplierCode" | hhTranslate: "Code" }}<input name="supplierCode" [(ngModel)]="supplierDraft.code" required /></label>
              <label>{{ "customerPortal.supplierName" | hhTranslate: "Name" }}<input name="supplierName" [(ngModel)]="supplierDraft.name" required /></label>
              <label>{{ "customerPortal.supplierLegalName" | hhTranslate: "Legal name" }}<input name="supplierLegalName" [(ngModel)]="supplierDraft.legalName" required /></label>
              <label>{{ "customerPortal.supplierTaxId" | hhTranslate: "Tax ID" }}<input name="supplierTaxId" [(ngModel)]="supplierDraft.taxIdentificationNumber" /></label>
              <label>{{ "customerPortal.supplierContactName" | hhTranslate: "Contact name" }}<input name="supplierContactName" [(ngModel)]="supplierDraft.contactName" /></label>
              <label>{{ "customerPortal.supplierContactEmail" | hhTranslate: "Contact email" }}<input name="supplierContactEmail" type="email" [(ngModel)]="supplierDraft.contactEmail" /></label>
              <label>{{ "customerPortal.supplierCountry" | hhTranslate: "Country (ISO 2)" }}<input name="supplierCountry" maxlength="2" [(ngModel)]="supplierDraft.countryCode" /></label>
              <label>{{ "customerPortal.supplierRisk" | hhTranslate: "Risk level" }}<select name="supplierRisk" [(ngModel)]="supplierDraft.riskLevel"><option value="Low">Low</option><option value="Standard">Standard</option><option value="High">High</option><option value="Critical">Critical</option></select></label>
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
                  <span class="status">{{ supplierApprovalStatusLabel(supplier.approvalStatus) }} · {{ supplier.riskLevel }}</span>
                  <hh-action-button kind="row" mode="icon-only" icon="edit" [label]="'common.edit' | hhTranslate: 'Edit'" (pressed)="editSupplier(supplier)" /><hh-action-button kind="row" icon="verified_user" [label]="'customerPortal.supplierGovernance' | hhTranslate: 'Governance'" (pressed)="selectSupplierGovernance(supplier)" />
                  @if (supplier.approvalStatus === 'Draft') { <hh-action-button kind="secondary" icon="rate_review" [label]="'customerPortal.submitSupplierApproval' | hhTranslate: 'Submit for approval'" (pressed)="updateSupplierApproval(supplier, 'PendingApproval')" /> }
                  @if (supplier.approvalStatus === 'PendingApproval') { <hh-action-button kind="primary" icon="verified" [label]="'customerPortal.approveSupplier' | hhTranslate: 'Approve supplier'" (pressed)="updateSupplierApproval(supplier, 'Approved')" /> }
                  @if (supplier.approvalStatus === 'Approved') { <hh-action-button kind="secondary" icon="block" [label]="'customerPortal.suspendSupplier' | hhTranslate: 'Suspend supplier'" (pressed)="updateSupplierApproval(supplier, 'Suspended')" /> }
                  @if (supplier.approvalStatus === 'Suspended') { <hh-action-button kind="secondary" icon="verified" [label]="'customerPortal.reapproveSupplier' | hhTranslate: 'Re-approve supplier'" (pressed)="updateSupplierApproval(supplier, 'Approved')" /> }
                  @if (!supplier.active) {
                    <span class="inactive">{{
                      "customerPortal.supplierInactive" | hhTranslate: "inactive"
                    }}</span>
                  }
                </li>
              }
            </ul>
            @if (selectedSupplier) { <div class="supplier-governance"><div class="section-heading"><h3>{{ 'customerPortal.supplierGovernance' | hhTranslate: 'Supplier governance' }} · {{ selectedSupplier.code }}</h3></div><form class="supplier-form" (ngSubmit)="saveCertificate()"><label>{{ 'customerPortal.certificateType' | hhTranslate: 'Certificate type' }}<input name="certificateType" [(ngModel)]="certificateDraft.certificateType" required /></label><label>{{ 'customerPortal.certificateNumber' | hhTranslate: 'Certificate number' }}<input name="certificateNumber" [(ngModel)]="certificateDraft.certificateNumber" required /></label><label>{{ 'customerPortal.issuer' | hhTranslate: 'Issuer' }}<input name="certificateIssuer" [(ngModel)]="certificateDraft.issuer" required /></label><label>{{ 'customerPortal.expiresAt' | hhTranslate: 'Expires at' }}<input name="certificateExpires" type="date" [(ngModel)]="certificateDraft.expiresAt" required /></label><div class="receipt-actions"><hh-action-button kind="secondary" icon="verified" type="submit" [label]="'customerPortal.addCertificate' | hhTranslate: 'Add certificate'" [disabled]="governanceBusy" /></div></form>@if (certificates.length) { <ul class="list">@for (certificate of certificates; track certificate.id) { <li><strong>{{ certificate.certificateType }}</strong> — {{ certificate.certificateNumber }} <span class="status">{{ supplierApprovalStatusLabel(certificate.status) }} · {{ certificate.expiresAt | date:'mediumDate' }}</span></li> }</ul> } @else { <p class="empty">{{ 'customerPortal.noCertificates' | hhTranslate: 'No supplier certificates.' }}</p> }<form class="supplier-form" (ngSubmit)="saveMaterialApproval()"><label>{{ 'customerPortal.materialSku' | hhTranslate: 'Material SKU' }}<input name="approvalMaterialSku" [(ngModel)]="materialApprovalDraft.materialSku" required /></label><label>{{ 'customerPortal.approvedUom' | hhTranslate: 'Approved UOM' }}<input name="approvalUom" [(ngModel)]="materialApprovalDraft.approvedUom" required /></label><div class="receipt-actions"><hh-action-button kind="secondary" icon="fact_check" type="submit" [label]="'customerPortal.addMaterialApproval' | hhTranslate: 'Approve material'" [disabled]="governanceBusy" /></div></form>@if (materialApprovals.length) { <ul class="list">@for (approval of materialApprovals; track approval.id) { <li><strong>{{ approval.materialSku }}</strong> · {{ approval.approvedUom }} <span class="status">{{ supplierApprovalStatusLabel(approval.status) }}</span></li> }</ul> } @else { <p class="empty">{{ 'customerPortal.noMaterialApprovals' | hhTranslate: 'No material approvals.' }}</p> }</div> }
          }
        </section>

        <section class="section" id="rfqs">
          <div class="section-heading"><h2>{{ "customerPortal.supplierRfqs" | hhTranslate: "Supplier RFQs" }}</h2><hh-action-button kind="secondary" icon="request_quote" [label]="'customerPortal.addSupplierRfq' | hhTranslate: 'Create RFQ'" (pressed)="startSupplierRfq()" /></div>
          @if (supplierRfqDraft) { <form class="supplier-form" (ngSubmit)="saveSupplierRfq()"><label>{{ "customerPortal.rfqNumber" | hhTranslate: "RFQ number" }}<input name="rfqNumber" [(ngModel)]="supplierRfqDraft.rfqNumber" required /></label><label>{{ "customerPortal.materialSku" | hhTranslate: "Material SKU" }}<input name="rfqMaterialSku" [(ngModel)]="supplierRfqDraft.materialSku" required /></label><label>{{ "customerPortal.forecastQuantity" | hhTranslate: "Quantity" }}<input name="rfqQuantity" type="number" min="0.001" [(ngModel)]="supplierRfqDraft.quantity" required /></label><label>{{ "customerPortal.forecastUom" | hhTranslate: "UOM" }}<input name="rfqUom" [(ngModel)]="supplierRfqDraft.uom" required /></label><div class="receipt-actions"><hh-action-button kind="primary" icon="save" type="submit" [label]="'common.save' | hhTranslate: 'Save'" [disabled]="supplierRfqBusy" /></div></form> }
          @if (supplierRfqError) { <p class="error">{{ supplierRfqError }}</p> }
@if (!supplierRfqs.length) { <p class="empty">{{ "customerPortal.noSupplierRfqs" | hhTranslate: "No supplier RFQs." }}</p> } @else { <ul class="list">@for (rfq of supplierRfqs; track rfq.id) { <li><strong>{{ rfq.rfqNumber }}</strong> — {{ rfq.materialSku }} · {{ rfq.quantity }} {{ rfq.uom }} <span class="status">{{ quotationStatusLabel(rfq.status) }}</span> <hh-action-button kind="secondary" icon="add" [label]="'customerPortal.addQuotation' | hhTranslate: 'quotation'" (pressed)="startSupplierQuotation(rfq.id)" /> <hh-action-button kind="secondary" icon="visibility" [label]="'customerPortal.viewQuotations' | hhTranslate: 'View quotations'" (pressed)="loadQuotations(rfq.id)" /></li> }</ul> }
          @if (supplierQuotationDraft) { <form class="supplier-form" (ngSubmit)="saveSupplierQuotation()"><label>{{ "customerPortal.supplier" | hhTranslate: "Supplier" }}<select name="quotationSupplier" [(ngModel)]="supplierQuotationDraft.supplierId" required><option value="">{{ "customerPortal.selectSupplier" | hhTranslate: "Select supplier" }}</option>@for (supplier of suppliers; track supplier.id) { <option [value]="supplier.id">{{ supplier.code }} · {{ supplier.name }}</option> }</select></label><label>{{ "customerPortal.unitPrice" | hhTranslate: "Unit price" }}<input name="quotationPrice" type="number" min="0" [(ngModel)]="supplierQuotationDraft.unitPrice" required /></label><label>{{ "customerPortal.leadTimeDays" | hhTranslate: "Lead time (days)" }}<input name="quotationLeadTime" type="number" min="0" [(ngModel)]="supplierQuotationDraft.leadTimeDays" required /></label><div class="receipt-actions"><hh-action-button kind="primary" icon="save" type="submit" [label]="'common.save' | hhTranslate: 'Save'" [disabled]="supplierQuotationBusy" /></div></form> }
          @if (quotationRfqId) { <div class="quotation-list"><h3>{{ "customerPortal.quotationHistory" | hhTranslate: "Quotation history" }}</h3>@if (!quotations.length) { <p class="empty">{{ "customerPortal.noQuotations" | hhTranslate: "No quotations." }}</p> } @else { @for (quotation of quotations; track quotation.id) { <div class="quotation-row"><strong>{{ supplierName(quotation.supplierId) }}</strong><span>{{ quotation.unitPrice | currency: quotation.currency }} · {{ quotation.leadTimeDays }}d</span><span class="status">{{ quotationStatusLabel(quotation.status) }}</span>@if (quotation.status === 'Submitted') { <hh-action-button kind="primary" icon="check" [label]="'customerPortal.selectQuotation' | hhTranslate: 'Select'" [disabled]="supplierQuotationBusy" (pressed)="setQuotationStatus(quotation, 'Selected')" /><hh-action-button kind="secondary" icon="close" [label]="'customerPortal.rejectQuotation' | hhTranslate: 'Reject'" [disabled]="supplierQuotationBusy" (pressed)="setQuotationStatus(quotation, 'Rejected')" /> } </div> } }</div> }
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
              <div class="line-editor-heading"><strong>{{ "customerPortal.purchaseOrderLines" | hhTranslate: "Purchase order lines" }}</strong><hh-action-button kind="secondary" icon="add" [label]="'customerPortal.addLine' | hhTranslate: 'Add line'" (pressed)="addPurchaseOrderLine()" /></div>
              @for (line of purchaseOrderLines; track $index; let index = $index) {
                <div class="line-row">
                  <input [name]="'materialSku' + index" [placeholder]="'customerPortal.materialSku' | hhTranslate: 'Material SKU'" [(ngModel)]="line.materialSku" required />
                  <input [name]="'orderedQuantity' + index" type="number" min="0.001" step="0.001" [placeholder]="'customerPortal.orderedQuantity' | hhTranslate: 'Quantity'" [(ngModel)]="line.orderedQuantity" required />
                  <input [name]="'uom' + index" [placeholder]="'customerPortal.uom' | hhTranslate: 'UOM'" [(ngModel)]="line.uom" required />
                  <input [name]="'unitPrice' + index" type="number" min="0" step="0.01" [placeholder]="'customerPortal.unitPrice' | hhTranslate: 'Unit price'" [(ngModel)]="line.unitPrice" required />
                  @if (purchaseOrderLines.length > 1) { <hh-action-button kind="danger" mode="icon-only" icon="close" [label]="'customerPortal.removeLine' | hhTranslate: 'Remove line'" (pressed)="removePurchaseOrderLine(index)" /> }
                </div>
              }
            </div>
            <div class="receipt-actions"><hh-action-button kind="primary" icon="add_shopping_cart" type="submit" [label]="'customerPortal.createPurchaseOrder' | hhTranslate: 'Create purchase order'" [disabled]="purchaseOrderBusy" /></div>
          </form>
          @if (purchaseOrderError) { <p class="error">{{ purchaseOrderError }}</p> }
        </section>

        <section class="section" id="purchase-orders">
          <h2>{{ "customerPortal.purchaseOrders" | hhTranslate: "Purchase orders" }}</h2>
          <div class="po-toolbar" role="search">
            <label>
              {{ "customerPortal.purchaseOrderSearch" | hhTranslate: "Search purchase orders" }}
              <input name="purchaseOrderSearch" type="search" [(ngModel)]="purchaseOrderSearch" [placeholder]="'customerPortal.purchaseOrderSearchPlaceholder' | hhTranslate: 'Order number, supplier code or name'" />
            </label>
            <label>
              {{ "customerPortal.purchaseOrderStatusFilter" | hhTranslate: "Filter by status" }}
              <select name="purchaseOrderStatusFilter" [(ngModel)]="purchaseOrderStatusFilter">
                <option value="">{{ "customerPortal.allStatuses" | hhTranslate: "All statuses" }}</option>
                @for (status of purchaseOrderStatuses; track status) { <option [value]="status">{{ purchaseOrderStatusLabel(status) }}</option> }
              </select>
            </label>
            <span class="toolbar-count">{{ visiblePurchaseOrders.length }} / {{ purchaseOrders.length }}</span>
          </div>
          @if (!purchaseOrders.length) {
            <p class="empty">{{ "customerPortal.noPurchaseOrders" | hhTranslate: "No purchase orders." }}</p>
          } @else if (!visiblePurchaseOrders.length) {
            <p class="empty">{{ "customerPortal.noPurchaseOrdersMatch" | hhTranslate: "No purchase orders match the current filters." }}</p>
          } @else {
            <div class="cards">
              @for (po of visiblePurchaseOrders; track po.id) {
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
        <section class="section" id="inbound-receipts">
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
                  <span class="status">{{ receiptDispositionLabel(receipt.disposition) }}</span>
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
                  @for (facility of facilities; track facility.id) { <option [value]="facility.code">{{ facility.code }} · {{ facility.name }}</option> }
                </select>
              </label>
              <label>{{ "customerPortal.receiptQuantity" | hhTranslate: "Quantity" }}<input name="quantity" type="number" min="0.001" step="0.001" [(ngModel)]="receiptDraft.quantity" required /></label>
              <label>{{ "customerPortal.expiryDate" | hhTranslate: "Expiry date" }}<input name="expiryDate" type="date" [(ngModel)]="receiptDraft.expiryDate" /></label>
              <label>{{ "customerPortal.traceabilityLotCode" | hhTranslate: "Internal traceability lot" }}<input name="traceabilityLotCode" [(ngModel)]="receiptDraft.traceabilityLotCode" /></label>
              <label>{{ "customerPortal.originCountryCode" | hhTranslate: "Origin country (ISO-2)" }}<input name="originCountryCode" maxlength="2" [(ngModel)]="receiptDraft.originCountryCode" /></label>
              <label>{{ "customerPortal.deliveryNoteNumber" | hhTranslate: "Delivery note" }}<input name="deliveryNoteNumber" [(ngModel)]="receiptDraft.deliveryNoteNumber" /></label>
              <label>{{ "customerPortal.coaReference" | hhTranslate: "Certificate of analysis reference" }}<input name="certificateOfAnalysisReference" [(ngModel)]="receiptDraft.certificateOfAnalysisReference" /></label>
              <div class="receipt-actions">
                <hh-action-button kind="primary" icon="move_to_inbox" type="submit" [label]="'customerPortal.postReceipt' | hhTranslate: 'Post receipt'" [disabled]="receivingBusy" />
                <hh-action-button kind="secondary" icon="playlist_add_check" type="button" [label]="'customerPortal.postBatchReceipt' | hhTranslate: 'Post batch receipt'" [disabled]="receivingBusy" (pressed)="receiveInboundBatch()" />
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
      .procurement-nav {
        position: relative;
      }
      .section {
        margin-bottom: var(--space-xl);
      }
      .procurement-nav[data-active-tab] ~ .section { display: none; }
      .procurement-nav[data-active-tab="requirements"] ~ #requirements,
      .procurement-nav[data-active-tab="facilities"] ~ #facilities,
      .procurement-nav[data-active-tab="master-data"] ~ #master-data,
      .procurement-nav[data-active-tab="suppliers"] ~ #suppliers,
      .procurement-nav[data-active-tab="rfqs"] ~ #rfqs,
      .procurement-nav[data-active-tab="purchase-orders"] ~ #purchase-orders,
      .procurement-nav[data-active-tab="inbound-receipts"] ~ #inbound-receipts { display: block; }
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
      input, select { min-height: var(--control-height); padding: 0 var(--space-sm); border: 1px solid var(--border-subtle); border-radius: var(--radius-control); background: var(--surface-raised); color: var(--text-primary); font: inherit; }
      .po-toolbar { display: grid; grid-template-columns: minmax(220px, 2fr) minmax(180px, 1fr) auto; gap: var(--space-md); align-items: end; margin-bottom: var(--space-md); padding: var(--space-md); background: var(--surface-muted); border: 1px solid var(--border-subtle); border-radius: var(--radius-card); }
      .toolbar-count { color: var(--text-secondary); font-size: var(--font-size-caption); white-space: nowrap; padding-bottom: var(--space-sm); }
      .receipt-actions { display: flex; gap: var(--space-sm); align-items: center; }
      .wide { grid-column: 1 / -1; }
      .line-editor { display: grid; gap: var(--space-sm); }
      .line-editor-heading { display: flex; align-items: center; justify-content: space-between; gap: var(--space-sm); }
      .line-row { display: grid; grid-template-columns: 2fr 1fr 1fr 1fr auto; gap: var(--space-sm); align-items: center; }
      .received { color: var(--text-secondary); font-size: var(--font-size-caption); }
      .po-actions { display: flex; flex-wrap: wrap; gap: var(--space-sm); margin-top: var(--space-sm); }
      .error { color: var(--color-danger); }
      .quotation-list { display: grid; gap: var(--space-sm); margin-top: var(--space-md); }
      .quotation-row { display: flex; gap: var(--space-md); align-items: center; flex-wrap: wrap; padding: var(--space-sm); border: 1px solid var(--border-subtle); border-radius: var(--radius-control); }
      @media (max-width: 700px) { .line-row { grid-template-columns: 1fr 1fr; } .wide { grid-column: auto; } .po-toolbar { grid-template-columns: 1fr; } .toolbar-count { padding-bottom: 0; } }
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
  activeTab = "requirements";
  error = "";
  suppliers: HisHopeSupplierDto[] = [];
  supplierDraft: { id?: string; code: string; name: string; legalName: string; taxIdentificationNumber: string; contactName: string; contactEmail: string; contactPhone: string; countryCode: string; address: string; riskLevel: string; active: boolean } | null = null;
  supplierBusy = false;
  supplierError = "";
  selectedSupplier: HisHopeSupplierDto | null = null;
  certificates: HisHopeSupplierCertificateDto[] = [];
  materialApprovals: HisHopeSupplierMaterialApprovalDto[] = [];
  governanceBusy = false;
  certificateDraft = { certificateType: "", certificateNumber: "", issuer: "", expiresAt: "" };
  materialApprovalDraft = { materialSku: "", approvedUom: "kg" };
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
  quotationRfqId: string | null = null;
  quotations: HisHopeSupplierQuotationDto[] = [];
  uomDraft: { code: string; name: string; dimension: string } | null = null;
  materialDraft: { sku: string; name: string; baseUomCode: string } | null = null;
  masterDataBusy = false;
  masterDataError = "";
  purchaseOrders: HisHopePurchaseOrderDto[] = [];
  purchaseOrderSearch = "";
  purchaseOrderStatusFilter = "";
  readonly purchaseOrderStatuses = ["Draft", "Approved", "PartiallyReceived", "Received", "Cancelled"];
  receipts: HisHopeInboundReceiptDto[] = [];
  materialRequirements: HisHopeManufacturingMaterialRequirementDto[] = [];
  tenantLabel: string | null = null;
  receiving: { purchaseOrderId: string; purchaseOrderLineId: string; materialSku: string; uom: string; orderedQuantity: number } | null = null;
  receivingBusy = false;
  receiptError = "";
  receiptDraft = { receiptNumber: "", supplierLotCode: "", facilityId: "default", quantity: 0, expiryDate: "", traceabilityLotCode: "", originCountryCode: "", deliveryNoteNumber: "", certificateOfAnalysisReference: "" };
  purchaseOrderBusy = false;
  purchaseOrderError = "";
  purchaseOrderDraft = { supplierId: "", orderNumber: "", expectedAt: "", currency: "VND" };
  purchaseOrderLines = [{ materialSku: "", orderedQuantity: 0, uom: "kg", unitPrice: 0 }];

  selectTab(tab: string): void {
    this.activeTab = tab;
    this.cdr.markForCheck();
  }

  get visiblePurchaseOrders(): HisHopePurchaseOrderDto[] {
    const search = this.purchaseOrderSearch.trim().toLowerCase();
    return this.purchaseOrders.filter((order) => {
      const matchesStatus = !this.purchaseOrderStatusFilter || order.status === this.purchaseOrderStatusFilter;
      const matchesSearch = !search || [order.orderNumber, order.supplierCode, order.supplierName]
        .some((value) => (value ?? "").toLowerCase().includes(search));
      return matchesStatus && matchesSearch;
    });
  }

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

  startSupplier(): void { this.supplierError = ""; this.supplierDraft = { code: "", name: "", legalName: "", taxIdentificationNumber: "", contactName: "", contactEmail: "", contactPhone: "", countryCode: "VN", address: "", riskLevel: "Standard", active: true }; }
  startSupplierRfq(): void { this.supplierRfqError = ""; this.supplierRfqDraft = { rfqNumber: `RFQ-${Date.now()}`, materialSku: "", quantity: 0, uom: "kg" }; }
  saveSupplierRfq(): void { const tenantKey = this.tenantContext.getActiveTenantKey(); const draft = this.supplierRfqDraft; if (!tenantKey || !draft || !draft.rfqNumber.trim() || !draft.materialSku.trim() || draft.quantity <= 0 || !draft.uom.trim()) { this.supplierRfqError = this.i18n.t("customerPortal.rfqFormInvalid", "RFQ fields are required."); return; } this.supplierRfqBusy = true; this.manufacturingApi.createSupplierRfq({ tenantKey, rfqNumber: draft.rfqNumber.trim(), materialSku: draft.materialSku.trim(), quantity: draft.quantity, uom: draft.uom.trim() }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rfq => { this.supplierRfqs = [rfq, ...this.supplierRfqs]; this.supplierRfqDraft = null; this.supplierRfqBusy = false; this.cdr.markForCheck(); }, error: error => { this.supplierRfqError = this.errors.message(error, "customerPortal.supplierRfqSaveFailed"); this.supplierRfqBusy = false; this.cdr.markForCheck(); } }); }
  startSupplierQuotation(rfqId: string): void { this.supplierQuotationDraft = { rfqId, supplierId: "", unitPrice: 0, leadTimeDays: 0 }; }
  loadQuotations(rfqId: string): void { this.quotationRfqId = rfqId; this.manufacturingApi.getSupplierQuotations(rfqId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.quotations = items ?? []; this.cdr.markForCheck(); }, error: (error) => { this.supplierRfqError = this.errors.message(error, "customerPortal.supplierQuotationLoadFailed"); this.cdr.markForCheck(); } }); }
  setQuotationStatus(quotation: HisHopeSupplierQuotationDto, status: string): void { this.supplierQuotationBusy = true; this.manufacturingApi.updateSupplierQuotationStatus(quotation.id, status).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.supplierQuotationBusy = false; if (this.quotationRfqId) this.loadQuotations(this.quotationRfqId); }, error: (error) => { this.supplierRfqError = this.errors.message(error, "customerPortal.supplierQuotationSaveFailed"); this.supplierQuotationBusy = false; this.cdr.markForCheck(); } }); }
  supplierName(id: string): string { return this.suppliers.find((item) => item.id === id)?.name ?? id; }
  saveSupplierQuotation(): void { const draft = this.supplierQuotationDraft; if (!draft?.supplierId || draft.unitPrice < 0 || draft.leadTimeDays < 0) return; this.supplierQuotationBusy = true; this.manufacturingApi.createSupplierQuotation(draft.rfqId, { supplierRfqId: draft.rfqId, supplierId: draft.supplierId, unitPrice: draft.unitPrice, currency: "VND", leadTimeDays: draft.leadTimeDays }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.supplierQuotationDraft = null; this.supplierQuotationBusy = false; this.cdr.markForCheck(); }, error: error => { this.supplierRfqError = this.errors.message(error, "customerPortal.supplierQuotationSaveFailed"); this.supplierQuotationBusy = false; this.cdr.markForCheck(); } }); }
  editSupplier(supplier: HisHopeSupplierDto): void { this.supplierError = ""; this.supplierDraft = { id: supplier.id, code: supplier.code, name: supplier.name, legalName: supplier.legalName || supplier.name, taxIdentificationNumber: supplier.taxIdentificationNumber ?? "", contactName: supplier.contactName ?? "", contactEmail: supplier.contactEmail ?? "", contactPhone: supplier.contactPhone ?? "", countryCode: supplier.countryCode ?? "", address: supplier.address ?? "", riskLevel: supplier.riskLevel || "Standard", active: supplier.active }; }
  saveSupplier(): void {
    const tenantKey = this.tenantContext.getActiveTenantKey();
    const draft = this.supplierDraft;
    if (!tenantKey || !draft || !draft.code.trim() || !draft.name.trim()) { this.supplierError = this.i18n.t("customerPortal.supplierFormInvalid", "Supplier code and name are required."); return; }
    this.supplierBusy = true; this.supplierError = "";
    const profile = { code: draft.code.trim(), name: draft.name.trim(), active: draft.active, legalName: draft.legalName.trim(), taxIdentificationNumber: draft.taxIdentificationNumber.trim(), contactName: draft.contactName.trim(), contactEmail: draft.contactEmail.trim(), contactPhone: draft.contactPhone.trim(), countryCode: draft.countryCode.trim(), address: draft.address.trim(), riskLevel: draft.riskLevel };
    const request = draft.id ? this.manufacturingApi.updateSupplier(draft.id, profile) : this.manufacturingApi.createSupplier({ tenantKey, ...profile });
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.supplierDraft = null; this.supplierBusy = false; this.load(); }, error: (error) => { this.supplierError = this.errors.message(error, "customerPortal.supplierSaveFailed"); this.supplierBusy = false; this.cdr.markForCheck(); } });
  }
  updateSupplierApproval(supplier: HisHopeSupplierDto, status: string): void { this.supplierBusy = true; this.supplierError = ""; this.manufacturingApi.updateSupplierApproval(supplier.id, status).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.supplierBusy = false; this.load(); }, error: (error) => { this.supplierError = this.errors.message(error, "customerPortal.supplierApprovalSaveFailed"); this.supplierBusy = false; this.cdr.markForCheck(); } }); }
  selectSupplierGovernance(supplier: HisHopeSupplierDto): void { this.selectedSupplier = supplier; this.governanceBusy = false; this.manufacturingApi.getSupplierCertificates(supplier.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.certificates = items ?? []; this.cdr.markForCheck(); } }); this.manufacturingApi.getSupplierMaterialApprovals(supplier.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.materialApprovals = items ?? []; this.cdr.markForCheck(); } }); }
  saveCertificate(): void { const supplier = this.selectedSupplier; const draft = this.certificateDraft; if (!supplier || !draft.certificateType.trim() || !draft.certificateNumber.trim() || !draft.issuer.trim() || !draft.expiresAt) return; this.governanceBusy = true; this.manufacturingApi.createSupplierCertificate(supplier.id, { certificateType: draft.certificateType.trim(), certificateNumber: draft.certificateNumber.trim(), issuer: draft.issuer.trim(), issuedAt: new Date().toISOString(), expiresAt: new Date(draft.expiresAt).toISOString() }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (item) => { this.certificates = [item, ...this.certificates]; this.governanceBusy = false; this.cdr.markForCheck(); }, error: (error) => { this.supplierError = this.errors.message(error, "customerPortal.supplierSaveFailed"); this.governanceBusy = false; this.cdr.markForCheck(); } }); }
  saveMaterialApproval(): void { const supplier = this.selectedSupplier; const draft = this.materialApprovalDraft; if (!supplier || !draft.materialSku.trim() || !draft.approvedUom.trim()) return; this.governanceBusy = true; this.manufacturingApi.createSupplierMaterialApproval(supplier.id, { materialSku: draft.materialSku.trim(), approvedUom: draft.approvedUom.trim(), effectiveFrom: new Date().toISOString() }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (item) => { this.materialApprovals = [item, ...this.materialApprovals]; this.governanceBusy = false; this.cdr.markForCheck(); }, error: (error) => { this.supplierError = this.errors.message(error, "customerPortal.supplierSaveFailed"); this.governanceBusy = false; this.cdr.markForCheck(); } }); }

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
  supplierApprovalStatusLabel(status: string): string { return portalEnumLabel(this.i18n, "supplierApprovalStatus", status); }
  quotationStatusLabel(status: string): string { return portalEnumLabel(this.i18n, "quotationStatus", status); }
  receiptDispositionLabel(disposition: string): string { return portalEnumLabel(this.i18n, "disposition", disposition); }

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
    this.receiptDraft = { receiptNumber: "", supplierLotCode: "", facilityId: "default", quantity: Math.max(0, line.orderedQuantity - line.receivedQuantity), expiryDate: "", traceabilityLotCode: "", originCountryCode: "", deliveryNoteNumber: "", certificateOfAnalysisReference: "" };
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
      traceabilityLotCode: this.receiptDraft.traceabilityLotCode.trim() || undefined,
      originCountryCode: this.receiptDraft.originCountryCode.trim() || undefined,
      deliveryNoteNumber: this.receiptDraft.deliveryNoteNumber.trim() || undefined,
      certificateOfAnalysisReference: this.receiptDraft.certificateOfAnalysisReference.trim() || undefined,
      acceptedQuantity: this.receiptDraft.quantity,
      rejectedQuantity: 0,
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.receivingBusy = false; this.receiving = null; this.load(); },
      error: (error) => { this.receiptError = this.errors.message(error, "customerPortal.receiptSaveFailed"); this.receivingBusy = false; this.cdr.markForCheck(); },
    });
  }

  receiveInboundBatch(): void {
    if (!this.receiving) return;
    if (!this.receiptDraft.receiptNumber.trim() || !this.receiptDraft.supplierLotCode.trim() || !this.receiptDraft.facilityId.trim() || this.receiptDraft.quantity <= 0 || this.receiptDraft.quantity > this.receiving.orderedQuantity) { this.receiptError = this.i18n.t("customerPortal.receiptFormInvalid", "Receipt number, supplier lot, facility and a valid quantity are required."); return; }
    this.receivingBusy = true; this.receiptError = "";
    this.manufacturingApi.receiveInboundBatch(this.receiving.purchaseOrderId, [{ purchaseOrderId: this.receiving.purchaseOrderId, purchaseOrderLineId: this.receiving.purchaseOrderLineId, materialSku: this.receiving.materialSku, receiptNumber: this.receiptDraft.receiptNumber.trim(), supplierLotCode: this.receiptDraft.supplierLotCode.trim(), facilityId: this.receiptDraft.facilityId.trim(), quantity: this.receiptDraft.quantity, expiryDate: this.receiptDraft.expiryDate || undefined, traceabilityLotCode: this.receiptDraft.traceabilityLotCode.trim() || undefined, originCountryCode: this.receiptDraft.originCountryCode.trim() || undefined, deliveryNoteNumber: this.receiptDraft.deliveryNoteNumber.trim() || undefined, certificateOfAnalysisReference: this.receiptDraft.certificateOfAnalysisReference.trim() || undefined, acceptedQuantity: this.receiptDraft.quantity, rejectedQuantity: 0 }]).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.receivingBusy = false; this.receiving = null; this.load(); }, error: (error) => { this.receiptError = this.errors.message(error, "customerPortal.receiptSaveFailed"); this.receivingBusy = false; this.cdr.markForCheck(); } });
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
