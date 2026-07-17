// @ts-nocheck
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { MedicationFormComponent } from './medication-form.component';
import { PharmacyService } from '@core/services/pharmacy.service';

describe('MedicationFormComponent', () => {
  let component: MedicationFormComponent;
  let fixture: ComponentFixture<MedicationFormComponent>;

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('PharmacyService', ['getMedication', 'createMedication', 'updateMedication']);

    await TestBed.configureTestingModule({
      declarations: [MedicationFormComponent],
      imports: [
        RouterTestingModule, NoopAnimationsModule, HttpClientTestingModule,
        ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule,
        MatCheckboxModule, MatButtonModule, MatIconModule, MatSnackBarModule, MatProgressSpinnerModule, CommonModule,
      ],
      providers: [
        { provide: PharmacyService, useValue: spy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(MedicationFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Thêm thuốc mới');
  });

  it('should have form controls', () => {
    expect(component.medicationForm.contains('name')).toBeTrue();
    expect(component.medicationForm.contains('genericName')).toBeTrue();
    expect(component.medicationForm.contains('dosageForm')).toBeTrue();
    expect(component.medicationForm.contains('strength')).toBeTrue();
    expect(component.medicationForm.contains('route')).toBeTrue();
  });

  it('should be invalid when empty', () => {
    expect(component.medicationForm.valid).toBeFalse();
  });

  it('should show cancel button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('button[routerLink="/pharmacy/medications"]')).toBeTruthy();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
