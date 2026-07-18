import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { PatientListComponent } from './patient-list.component';
import { PatientService } from '@core/services/patient.service';
import { createMockPatientList, createMockPagedResult } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('PatientListComponent', () => {
  let component: PatientListComponent;
  let fixture: ComponentFixture<PatientListComponent>;
  let patientService: jasmine.SpyObj<PatientService>;

  const mockPatients = createMockPatientList(3);

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('PatientService', ['search']);
    spy.search.and.returnValue(of(createMockPagedResult(mockPatients, 3)));

    await TestBed.configureTestingModule({
    declarations: [PatientListComponent],
    imports: [RouterTestingModule,
        NoopAnimationsModule,
        MatTableModule,
        MatPaginatorModule,
        MatFormFieldModule,
        MatInputModule,
        MatIconModule,
        MatButtonModule,
        ReactiveFormsModule,
        CommonModule],
    providers: [
        { provide: PatientService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(PatientListComponent);
    component = fixture.componentInstance;
    patientService = TestBed.inject(PatientService) as jasmine.SpyObj<PatientService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load patients on init', () => {
    expect(patientService.search).toHaveBeenCalled();
    expect(component.patients.length).toBe(3);
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Patients');
  });

  it('should render new patient button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const btn = compiled.querySelector('button[routerLink="/patients/new"]');
    expect(btn).toBeTruthy();
  });

  it('should render patient rows in table', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('mat-row');
    expect(rows.length).toBe(3);
  });

  it('should display search field', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const searchInput = compiled.querySelector('input[placeholder="Name, phone, or ID..."]');
    expect(searchInput).toBeTruthy();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
