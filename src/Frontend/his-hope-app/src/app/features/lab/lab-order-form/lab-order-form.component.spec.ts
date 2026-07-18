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
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { of } from 'rxjs';
import { LabOrderFormComponent } from './lab-order-form.component';
import { LabService } from '@core/services/lab.service';
import { PatientService } from '@core/services/patient.service';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('LabOrderFormComponent', () => {
  let component: LabOrderFormComponent;
  let fixture: ComponentFixture<LabOrderFormComponent>;

  beforeEach(async () => {
    const labSpy = jasmine.createSpyObj('LabService', ['createLabOrder']);
    const patientSpy = jasmine.createSpyObj('PatientService', ['search']);
    patientSpy.search.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false }));

    await TestBed.configureTestingModule({
    
    imports: [
        LabOrderFormComponent, RouterTestingModule, NoopAnimationsModule,
        ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule,
        MatAutocompleteModule, MatCardModule, MatButtonModule, MatIconModule,
        MatSnackBarModule, MatProgressSpinnerModule],
    providers: [
        { provide: LabService, useValue: labSpy },
        { provide: PatientService, useValue: patientSpy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(LabOrderFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Tạo phiếu xét nghiệm mới');
  });

  it('should have form controls', () => {
    expect(component.labOrderForm.contains('patientId')).toBeTrue();
    expect(component.labOrderForm.contains('priorityCode')).toBeTrue();
    expect(component.labOrderForm.contains('notes')).toBeTrue();
  });

  it('should have tests form array', () => {
    expect(component.tests.length).toBe(1);
  });

  it('should show cancel button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('button[routerLink="/lab"]')).toBeTruthy();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
