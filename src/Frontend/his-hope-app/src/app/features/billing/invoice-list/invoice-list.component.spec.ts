import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { InvoiceListComponent } from './invoice-list.component';
import { BillingService } from '@core/services/billing.service';
import { createMockInvoice, createMockPagedResult } from '@testing/mock-data';

describe('InvoiceListComponent', () => {
  let component: InvoiceListComponent;
  let fixture: ComponentFixture<InvoiceListComponent>;
  let billingService: jasmine.SpyObj<BillingService>;

  const mockInvoices = [createMockInvoice(), createMockInvoice()];

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('BillingService', ['searchInvoices']);
    spy.searchInvoices.and.returnValue(of(createMockPagedResult(mockInvoices, 2)));

    await TestBed.configureTestingModule({
      declarations: [InvoiceListComponent],
      imports: [
        RouterTestingModule, NoopAnimationsModule, HttpClientTestingModule,
        MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
        MatSelectModule, MatIconModule, MatButtonModule, MatProgressBarModule,
        ReactiveFormsModule, CommonModule,
      ],
      providers: [
        { provide: BillingService, useValue: spy },
      ],
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
    const rows = compiled.querySelectorAll('mat-row');
    expect(rows.length).toBe(2);
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
