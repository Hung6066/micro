import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { EncounterDetailComponent } from './encounter-detail.component';
import { ClinicalService } from '@core/services/clinical.service';
import { LabService } from '@core/services/lab.service';
import { PharmacyService } from '@core/services/pharmacy.service';
import { createMockEncounter, createMockPagedResult } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('EncounterDetailComponent', () => {
  let component: EncounterDetailComponent;
  let fixture: ComponentFixture<EncounterDetailComponent>;
  let clinicalService: jasmine.SpyObj<ClinicalService>;

  const mockEncounter = createMockEncounter();

  beforeEach(async () => {
    const clinicalSpy = jasmine.createSpyObj('ClinicalService', ['getById']);
    clinicalSpy.getById.and.returnValue(of(mockEncounter));

    const labSpy = jasmine.createSpyObj('LabService', ['getPatientLabOrders']);
    labSpy.getPatientLabOrders.and.returnValue(of(createMockPagedResult([], 0)));

    const pharmacySpy = jasmine.createSpyObj('PharmacyService', ['getPatientPrescriptions']);
    pharmacySpy.getPatientPrescriptions.and.returnValue(of(createMockPagedResult([], 0)));

    await TestBed.configureTestingModule({
    declarations: [EncounterDetailComponent],
    imports: [RouterTestingModule, NoopAnimationsModule,
        MatCardModule, MatListModule, MatButtonModule, MatIconModule,
        MatSnackBarModule, CommonModule],
    providers: [
        { provide: ClinicalService, useValue: clinicalSpy },
        { provide: LabService, useValue: labSpy },
        { provide: PharmacyService, useValue: pharmacySpy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(EncounterDetailComponent);
    component = fixture.componentInstance;
    clinicalService = TestBed.inject(ClinicalService) as jasmine.SpyObj<ClinicalService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load encounter on init', () => {
    expect(clinicalService.getById).toHaveBeenCalled();
  });

  it('should display encounter title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Chi tiết lượt khám');
  });

  it('should render SOAP cards', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const cards = compiled.querySelectorAll('mat-card');
    expect(cards.length).toBeGreaterThanOrEqual(3);
  });

  it('should show overview section', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Tổng quan');
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
