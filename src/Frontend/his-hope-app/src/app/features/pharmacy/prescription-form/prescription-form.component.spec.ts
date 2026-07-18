import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { of } from 'rxjs';
import { PrescriptionFormComponent } from './prescription-form.component';
import { PharmacyService } from '@core/services/pharmacy.service';
import { PatientService } from '@core/services/patient.service';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('PrescriptionFormComponent', () => {
  let component: PrescriptionFormComponent;
  let fixture: ComponentFixture<PrescriptionFormComponent>;

  beforeEach(async () => {
    const pharmacySpy = jasmine.createSpyObj('PharmacyService', ['searchMedications', 'createPrescription']);
    pharmacySpy.searchMedications.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 200, hasNextPage: false, hasPreviousPage: false }));
    const patientSpy = jasmine.createSpyObj('PatientService', ['search']);
    patientSpy.search.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false }));

    await TestBed.configureTestingModule({
    declarations: [PrescriptionFormComponent],
    imports: [RouterTestingModule, NoopAnimationsModule,
        ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule,
        MatAutocompleteModule, MatButtonModule, MatIconModule, MatSnackBarModule, MatProgressSpinnerModule],
    providers: [
        { provide: PharmacyService, useValue: pharmacySpy },
        { provide: PatientService, useValue: patientSpy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(PrescriptionFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Tạo đơn thuốc mới');
  });

  it('should have form controls', () => {
    expect(component.prescriptionForm.contains('patientId')).toBeTrue();
    expect(component.prescriptionForm.contains('medicationId')).toBeTrue();
    expect(component.prescriptionForm.contains('dosageInstructions')).toBeTrue();
    expect(component.prescriptionForm.contains('quantity')).toBeTrue();
  });

  it('should be invalid when empty', () => {
    expect(component.prescriptionForm.valid).toBeFalse();
  });

  it('should show cancel button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('button[routerLink="/pharmacy/prescriptions"]')).toBeTruthy();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
