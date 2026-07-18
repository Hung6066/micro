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
import { MedicationDetailComponent } from './medication-detail.component';
import { PharmacyService } from '@core/services/pharmacy.service';
import { createMockMedication } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('MedicationDetailComponent', () => {
  let component: MedicationDetailComponent;
  let fixture: ComponentFixture<MedicationDetailComponent>;
  let pharmacyService: jasmine.SpyObj<PharmacyService>;

  const mockMedication = createMockMedication();

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('PharmacyService', ['getMedication', 'deactivateMedication']);
    spy.getMedication.and.returnValue(of(mockMedication));

    await TestBed.configureTestingModule({
    
    imports: [
        MedicationDetailComponent, RouterTestingModule, NoopAnimationsModule,
        MatCardModule, MatButtonModule, MatIconModule, MatSnackBarModule,
        MatProgressSpinnerModule, CommonModule],
    providers: [
        { provide: PharmacyService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(MedicationDetailComponent);
    component = fixture.componentInstance;
    pharmacyService = TestBed.inject(PharmacyService) as jasmine.SpyObj<PharmacyService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load medication on init', () => {
    expect(pharmacyService.getMedication).toHaveBeenCalled();
  });

  it('should display medication name', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain(mockMedication.name);
  });

  it('should render detail cards', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const cards = compiled.querySelectorAll('mat-card');
    expect(cards.length).toBeGreaterThanOrEqual(2);
  });

  it('should show edit button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const buttons = compiled.querySelectorAll('button');
    const editBtn = Array.from(buttons).find(b => b.textContent?.includes('Chỉnh sửa'));
    expect(editBtn).toBeTruthy();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
