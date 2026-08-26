import { DatePipe, DecimalPipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { HisHopeActionButtonComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent, HisHopeTabsComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeApiErrorMessageService as ApiErrorMessageService, HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeProductSpecificationDto } from "@his-hope/frontend-foundation/contracts";
import { ManufacturingApiService } from "../../core/services/manufacturing-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, FormsModule, HisHopeActionButtonComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent, HisHopeTabsComponent, HisHopeTranslatePipe],
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader [title]="'customerPortal.specificationsTitle' | hhTranslate: 'Finished product specifications'" [subtitle]="pageSubtitle" />
      <hh-tabs label="Specification sections"><button role="tab" type="button" [attr.aria-selected]="activeTab === 'create'" [class.active]="activeTab === 'create'" (click)="selectTab('create')">{{ 'customerPortal.createSpecification' | hhTranslate: 'Create specification' }}</button><button role="tab" type="button" [attr.aria-selected]="activeTab === 'list'" [class.active]="activeTab === 'list'" (click)="selectTab('list')">{{ 'customerPortal.qualityGovernance' | hhTranslate: 'Quality and finished goods' }}</button></hh-tabs>
      @if (loading) { <hh-state kind="loading" [message]="'customerPortal.loadingSpecifications' | hhTranslate: 'Loading specifications…'" /> }
      @else {
        <section class="section form-section">
          <div class="section-heading"><div><p class="eyebrow">{{ 'customerPortal.qualityGovernance' | hhTranslate: 'Quality and finished goods' }}</p><h2>{{ 'customerPortal.createSpecification' | hhTranslate: 'Create specification' }}</h2></div><span class="count">{{ specifications.length }}</span></div>
          <form class="spec-form" (ngSubmit)="create()">
            <label>{{ 'customerPortal.productSku' | hhTranslate: 'Product SKU' }}<input name="productSku" [(ngModel)]="draft.productSku" required /></label>
            <label>{{ 'customerPortal.targetMoisture' | hhTranslate: 'Target moisture %' }}<input name="targetMoisturePercent" type="number" min="0" max="100" step="0.01" [(ngModel)]="draft.targetMoisturePercent" required /></label>
            <label>{{ 'customerPortal.shelfLifeDays' | hhTranslate: 'Shelf life (days)' }}<input name="shelfLifeDays" type="number" min="1" [(ngModel)]="draft.shelfLifeDays" required /></label>
            <label>{{ 'customerPortal.packaging' | hhTranslate: 'Packaging' }}<input name="packaging" [(ngModel)]="draft.packaging" required /></label>
            <label class="wide">{{ 'customerPortal.qcSpec' | hhTranslate: 'QC specification' }}<textarea name="qcSpec" rows="3" [(ngModel)]="draft.qcSpec" required></textarea></label>
            <div class="wide actions"><hh-action-button type="submit" kind="primary" icon="fact_check" [label]="'customerPortal.createSpecification' | hhTranslate: 'Create specification'" [disabled]="saving" /></div>
          </form>
          @if (actionError) { <p class="action-error" role="alert">{{ actionError }}</p> }
        </section>
        <section class="section spec-grid">
          @for (spec of specifications; track spec.id) {
            <article class="card">
              <header><div><strong>{{ spec.productSku }}</strong><p class="meta">{{ spec.packaging }} · {{ spec.shelfLifeDays }} {{ 'customerPortal.days' | hhTranslate: 'days' }}</p></div><span class="status" [class.approved]="spec.status === 'Approved'">{{ spec.status }}</span></header>
              <dl><div><dt>{{ 'customerPortal.targetMoisture' | hhTranslate: 'Target moisture %' }}</dt><dd>{{ spec.targetMoisturePercent | number: '1.0-2' }}%</dd></div><div><dt>{{ 'customerPortal.qcSpec' | hhTranslate: 'QC specification' }}</dt><dd>{{ spec.qcSpec }}</dd></div></dl>
              <footer><span class="meta">{{ spec.createdAt | date: 'medium' }}</span><div class="actions">@if (spec.status === 'Draft') { <hh-action-button kind="primary" icon="verified" [label]="'customerPortal.approveSpecification' | hhTranslate: 'Approve'" [disabled]="saving" (pressed)="approve(spec)" /> } @if (spec.status === 'Approved') { <hh-action-button kind="secondary" icon="archive" [label]="'customerPortal.retireSpecification' | hhTranslate: 'Retire'" [disabled]="saving" (pressed)="retire(spec)" /> }</div></footer>
            </article>
          } @empty { <p class="empty">{{ 'customerPortal.noSpecifications' | hhTranslate: 'No specifications for the selected tenant.' }}</p> }
        </section>
      }
      @if (!loading && error) { <hh-state kind="error" [message]="error" (retry)="load()" /> }
    </hh-page-layout>
  `,
  styles: [`
    :host { font-family: var(--font-sans); } .section { margin-bottom: var(--space-lg); } .section-heading, header, footer { display:flex; align-items:center; justify-content:space-between; gap:var(--space-md); } .eyebrow, .meta { color:var(--text-secondary); font-size:var(--font-size-caption); margin:0; } .count { font-size:var(--font-size-title); font-weight:700; }
    .spec-form { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:var(--space-md); padding:var(--space-md); background:var(--surface-muted); border-radius:var(--radius-card); } label { display:grid; gap:var(--space-2xs); color:var(--text-primary); font-size:var(--font-size-caption); } input, textarea { border:1px solid var(--border-subtle); border-radius:var(--radius-control); padding:var(--space-sm); background:var(--surface); color:var(--text-primary); font:inherit; } .wide { grid-column:1/-1; } .actions { display:flex; flex-wrap:wrap; gap:var(--space-sm); justify-content:flex-end; } .spec-grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(320px,1fr)); gap:var(--space-md); } .card { padding:var(--space-md); border:1px solid var(--border-subtle); border-radius:var(--radius-card); background:var(--surface); color:var(--text-primary); } .status { padding:var(--space-2xs) var(--space-sm); border-radius:var(--radius-badge); background:var(--surface-muted); font-size:var(--font-size-caption); } .status.approved { background:var(--color-success-subtle); color:var(--color-success); } dl { display:grid; gap:var(--space-sm); } dt { color:var(--text-secondary); font-size:var(--font-size-caption); } dd { margin:0; } .action-error { color:var(--color-danger); } @media(max-width:900px){.spec-form{grid-template-columns:1fr 1fr}} @media(max-width:600px){.spec-form{grid-template-columns:1fr}.wide{grid-column:auto}}
  `],
})
export class ProductSpecificationsPageComponent implements OnInit, AfterViewInit {
  activeTab = "create";
  selectTab(tab: string): void { this.activeTab = tab; this.applyTabVisibility(); this.cdr.markForCheck(); }
  ngAfterViewInit(): void { const observer = new MutationObserver(() => { if (document.querySelectorAll("section.section").length) { this.applyTabVisibility(); observer.disconnect(); } }); observer.observe(document.body, { childList: true, subtree: true }); this.applyTabVisibility(); }
  private applyTabVisibility(): void { const sections = Array.from(document.querySelectorAll<HTMLElement>("section.section")); sections.forEach((section, index) => section.hidden = index !== (this.activeTab === "create" ? 0 : 1)); }
  private readonly api = inject(ManufacturingApiService); private readonly tenantContext = inject(TenantContextService); private readonly i18n = inject(HisHopeI18nService); private readonly errors = inject(ApiErrorMessageService); private readonly cdr = inject(ChangeDetectorRef); private readonly destroyRef = inject(DestroyRef);
  specifications: HisHopeProductSpecificationDto[] = []; loading = true; saving = false; error = ""; actionError = ""; tenantLabel: string | null = null;
  draft = { productSku: "", targetMoisturePercent: 12, packaging: "", shelfLifeDays: 180, qcSpec: "" };
  get pageSubtitle(): string { this.i18n.locale(); return this.i18n.t("customerPortal.tenantScope", "Tenant: {{tenant}}", { tenant: this.tenantLabel ?? "—" }); }
  ngOnInit(): void { this.tenantContext.activeTenantLabel$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((label) => { this.tenantLabel = label; this.cdr.markForCheck(); }); this.tenantContext.activeTenantKey$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load()); }
  load(): void { this.loading = true; this.error = ""; this.api.getProductSpecifications().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.specifications = items ?? []; this.loading = false; this.cdr.markForCheck(); }, error: (error) => { this.error = this.errors.message(error, "customerPortal.specificationsLoadFailed"); this.loading = false; this.cdr.markForCheck(); } }); }
  create(): void { const tenantKey = this.tenantContext.getActiveTenantKey(); if (!tenantKey || !this.draft.productSku.trim() || !this.draft.packaging.trim() || !this.draft.qcSpec.trim()) { this.actionError = this.i18n.t("customerPortal.specificationFormInvalid", "Tenant, SKU, packaging and QC specification are required."); return; } this.saving = true; this.actionError = ""; this.api.createProductSpecification({ ...this.draft, tenantKey }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.draft = { productSku: "", targetMoisturePercent: 12, packaging: "", shelfLifeDays: 180, qcSpec: "" }; this.load(); this.saving = false; }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.specificationSaveFailed"); this.saving = false; this.cdr.markForCheck(); } }); }
  approve(spec: HisHopeProductSpecificationDto): void { this.transition(spec, true); }
  retire(spec: HisHopeProductSpecificationDto): void { this.transition(spec, false); }
  private transition(spec: HisHopeProductSpecificationDto, approve: boolean): void { this.saving = true; this.actionError = ""; const request = approve ? this.api.approveProductSpecification(spec.id, "operator") : this.api.retireProductSpecification(spec.id, "operator"); request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.load(); this.saving = false; }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.specificationActionFailed"); this.saving = false; this.cdr.markForCheck(); } }); }
}
