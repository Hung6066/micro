import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { InvoiceDetailComponent } from './invoice-detail.component';
import { BillingService } from '@core/services/billing.service';
import { createMockInvoice } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('InvoiceDetailComponent', () => {
  let component: InvoiceDetailComponent;
  let fixture: ComponentFixture<InvoiceDetailComponent>;
  let billingService: jasmine.SpyObj<BillingService>;

  const mockInvoice = createMockInvoice();

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('BillingService', ['getInvoice', 'recordPayment', 'voidInvoice']);
    spy.getInvoice.and.returnValue(of(mockInvoice));

    await TestBed.configureTestingModule({
    
    imports: [
        InvoiceDetailComponent, RouterTestingModule, NoopAnimationsModule,
        ReactiveFormsModule, MatCardModule, MatTableModule, MatButtonModule,
        MatIconModule, MatSnackBarModule, MatProgressSpinnerModule,
        MatFormFieldModule, MatInputModule, MatSelectModule,
        MatDatepickerModule, MatNativeDateModule, CommonModule],
    providers: [
        { provide: BillingService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(InvoiceDetailComponent);
    component = fixture.componentInstance;
    billingService = TestBed.inject(BillingService) as jasmine.SpyObj<BillingService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load invoice on init', () => {
    expect(billingService.getInvoice).toHaveBeenCalled();
  });

  it('should display invoice number', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain(mockInvoice.invoiceNumber);
  });

  it('should render summary cards', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const cards = compiled.querySelectorAll('mat-card');
    expect(cards.length).toBeGreaterThanOrEqual(2);
  });

  it('should have payment form initialized', () => {
    expect(component.paymentForm.contains('amount')).toBeTrue();
    expect(component.paymentForm.contains('methodCode')).toBeTrue();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
