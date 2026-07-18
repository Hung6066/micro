import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { PatientDetailComponent } from './patient-detail.component';
import { PatientService } from '@core/services/patient.service';
import { createMockPatient, createMockPagedResult } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('PatientDetailComponent', () => {
  let component: PatientDetailComponent;
  let fixture: ComponentFixture<PatientDetailComponent>;
  let patientService: jasmine.SpyObj<PatientService>;

  const mockPatient = createMockPatient();

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('PatientService', ['getById', 'getEncounters', 'getPrescriptions', 'getLabOrders', 'deactivate', 'reactivate']);
    spy.getById.and.returnValue(of(mockPatient));
    spy.getEncounters.and.returnValue(of(createMockPagedResult([], 0)));
    spy.getPrescriptions.and.returnValue(of(createMockPagedResult([], 0)));
    spy.getLabOrders.and.returnValue(of(createMockPagedResult([], 0)));

    await TestBed.configureTestingModule({
    
    imports: [
        PatientDetailComponent, RouterTestingModule,
        NoopAnimationsModule,
        MatCardModule,
        MatTabsModule,
        MatTableModule,
        MatButtonModule,
        MatIconModule,
        MatListModule,
        MatSnackBarModule,
        MatProgressSpinnerModule,
        MatTooltipModule, CommonModule],
    providers: [
        { provide: PatientService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(PatientDetailComponent);
    component = fixture.componentInstance;
    patientService = TestBed.inject(PatientService) as jasmine.SpyObj<PatientService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load patient on init', () => {
    expect(patientService.getById).toHaveBeenCalled();
  });

  it('should display patient name', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain(mockPatient.fullName);
  });

  it('should render tab group', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const tabs = compiled.querySelectorAll('.mat-mdc-tab');
    expect(tabs.length).toBeGreaterThanOrEqual(4);
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
