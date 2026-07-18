import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { of } from 'rxjs';
import { InvoiceFormComponent } from './invoice-form.component';
import { BillingService } from '@core/services/billing.service';
import { PatientService } from '@core/services/patient.service';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('InvoiceFormComponent', () => {
  let component: InvoiceFormComponent;
  let fixture: ComponentFixture<InvoiceFormComponent>;

  beforeEach(async () => {
    const billingSpy = jasmine.createSpyObj('BillingService', ['createInvoice']);
    const patientSpy = jasmine.createSpyObj('PatientService', ['search']);
    patientSpy.search.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false }));

    await TestBed.configureTestingModule({
    declarations: [InvoiceFormComponent],
    imports: [RouterTestingModule, NoopAnimationsModule,
        ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule,
        MatAutocompleteModule, MatCardModule, MatDatepickerModule, MatNativeDateModule,
        MatButtonModule, MatIconModule, MatSnackBarModule, MatProgressSpinnerModule],
    providers: [
        { provide: BillingService, useValue: billingSpy },
        { provide: PatientService, useValue: patientSpy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(InvoiceFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Tạo hóa đơn mới');
  });

  it('should have form controls', () => {
    expect(component.invoiceForm.contains('patientId')).toBeTrue();
    expect(component.invoiceForm.contains('invoiceDate')).toBeTrue();
    expect(component.invoiceForm.contains('notes')).toBeTrue();
  });

  it('should have line items form array', () => {
    expect(component.lineItems).toBeDefined();
  });

  it('should show cancel button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('button[routerLink="/billing"]')).toBeTruthy();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
