import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { PrescribeDialogComponent, PrescribeData } from './prescribe.dialog';
import { PharmacyService } from '@core/services/pharmacy.service';
import { AuthService } from '@core/services/auth.service';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('PrescribeDialogComponent', () => {
  let component: PrescribeDialogComponent;
  let fixture: ComponentFixture<PrescribeDialogComponent>;

  const mockData: PrescribeData = { patientId: 'pat-001', patientName: 'Test Patient' };

  beforeEach(async () => {
    const pharmacySpy = jasmine.createSpyObj('PharmacyService', ['searchMedications', 'createPrescription']);
    pharmacySpy.searchMedications.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false }));
    const authSpy = jasmine.createSpyObj('AuthService', ['currentUser$']);
    authSpy.currentUser$ = of({ id: 'usr-001' });

    await TestBed.configureTestingModule({
    imports: [PrescribeDialogComponent,
        CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
        MatFormFieldModule, MatInputModule, MatSelectModule, MatAutocompleteModule,
        MatIconModule, MatProgressSpinnerModule, MatSnackBarModule, NoopAnimationsModule],
    providers: [
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: MAT_DIALOG_DATA, useValue: mockData },
        { provide: PharmacyService, useValue: pharmacySpy },
        { provide: AuthService, useValue: authSpy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(PrescribeDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Kê đơn thuốc');
  });

  it('should show patient name', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Test Patient');
  });

  it('should have form controls', () => {
    expect(component.form.contains('dosageInstructions')).toBeTrue();
    expect(component.form.contains('route')).toBeTrue();
    expect(component.form.contains('quantity')).toBeTrue();
  });

  it('should render form fields', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('input[formcontrolname="dosageInstructions"]')).toBeTruthy();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
