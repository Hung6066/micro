import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { PrescriptionDetailComponent } from './prescription-detail.component';
import { PharmacyService } from '@core/services/pharmacy.service';
import { createMockPrescription } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('PrescriptionDetailComponent', () => {
  let component: PrescriptionDetailComponent;
  let fixture: ComponentFixture<PrescriptionDetailComponent>;
  let pharmacyService: jasmine.SpyObj<PharmacyService>;

  const mockPrescription = createMockPrescription();

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('PharmacyService', ['getPrescription', 'fillPrescription', 'cancelPrescription']);
    spy.getPrescription.and.returnValue(of(mockPrescription));

    await TestBed.configureTestingModule({
    
    imports: [
        PrescriptionDetailComponent, RouterTestingModule, NoopAnimationsModule,
        MatCardModule, MatButtonModule, MatIconModule, MatSnackBarModule,
        MatProgressSpinnerModule, CommonModule],
    providers: [
        { provide: PharmacyService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(PrescriptionDetailComponent);
    component = fixture.componentInstance;
    pharmacyService = TestBed.inject(PharmacyService) as jasmine.SpyObj<PharmacyService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load prescription on init', () => {
    expect(pharmacyService.getPrescription).toHaveBeenCalled();
  });

  it('should display prescription info', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Đơn thuốc');
  });

  it('should render detail cards', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const cards = compiled.querySelectorAll('mat-card');
    expect(cards.length).toBeGreaterThanOrEqual(2);
  });

  it('should display medication name', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain(mockPrescription.medicationName);
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
