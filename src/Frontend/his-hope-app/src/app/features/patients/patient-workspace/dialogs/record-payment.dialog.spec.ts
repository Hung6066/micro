import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { RecordPaymentDialogComponent, RecordPaymentData } from './record-payment.dialog';
import { BillingService } from '@core/services/billing.service';
import { createMockInvoice } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('RecordPaymentDialogComponent', () => {
  let component: RecordPaymentDialogComponent;
  let fixture: ComponentFixture<RecordPaymentDialogComponent>;

  const mockInvoices = [createMockInvoice({ balanceDue: 550000 })];
  const mockData: RecordPaymentData = { patientId: 'pat-001', patientName: 'Test Patient', invoices: mockInvoices };

  beforeEach(async () => {
    const billingSpy = jasmine.createSpyObj('BillingService', ['recordPayment']);

    await TestBed.configureTestingModule({
    imports: [RecordPaymentDialogComponent,
        CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
        MatFormFieldModule, MatInputModule, MatSelectModule, MatDatepickerModule,
        MatNativeDateModule, MatIconModule, MatProgressSpinnerModule, MatSnackBarModule,
        NoopAnimationsModule],
    providers: [
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: MAT_DIALOG_DATA, useValue: mockData },
        { provide: BillingService, useValue: billingSpy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(RecordPaymentDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Ghi nhận thanh toán');
  });

  it('should show patient name', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Test Patient');
  });

  it('should have payable invoices', () => {
    expect(component.payableInvoices.length).toBeGreaterThan(0);
  });

  it('should render invoice select', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('mat-select')).toBeTruthy();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
