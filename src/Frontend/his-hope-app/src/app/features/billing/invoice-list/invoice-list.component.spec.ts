import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { InvoiceListComponent } from './invoice-list.component';
import { BillingService } from '@core/services/billing.service';
import { createMockInvoice, createMockPagedResult } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HisHopePageQuery } from '@his-hope/frontend-foundation';

describe('InvoiceListComponent', () => {
  let component: InvoiceListComponent;
  let fixture: ComponentFixture<InvoiceListComponent>;
  let billingService: jasmine.SpyObj<BillingService>;

  const mockInvoices = [createMockInvoice(), createMockInvoice()];

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('BillingService', ['searchInvoices']);
    spy.searchInvoices.and.returnValue(of(createMockPagedResult(mockInvoices, 2)));

    await TestBed.configureTestingModule({
    imports: [
        InvoiceListComponent, RouterTestingModule, NoopAnimationsModule],
    providers: [
        { provide: BillingService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(InvoiceListComponent);
    component = fixture.componentInstance;
    billingService = TestBed.inject(BillingService) as jasmine.SpyObj<BillingService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load invoices on init', () => {
    expect(billingService.searchInvoices).toHaveBeenCalled();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Hóa đơn thanh toán');
  });

  it('should show create button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const btn = compiled.querySelector('button[routerLink="/billing/new"]');
    expect(btn).toBeTruthy();
  });

  it('should display invoice rows', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('.hh-data-table tbody tr');
    expect(rows.length).toBe(2);
  });

  it('should search invoices when the query changes', () => {
    const query: HisHopePageQuery = { page: 1, pageSize: 20, search: 'INV-' };
    component.onQueryChange(query);
    expect(billingService.searchInvoices).toHaveBeenCalledWith(jasmine.objectContaining({ searchTerm: 'INV-' }));
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
