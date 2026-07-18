import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { PrescriptionListComponent } from './prescription-list.component';
import { PharmacyService } from '@core/services/pharmacy.service';
import { createMockPrescription, createMockPagedResult } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('PrescriptionListComponent', () => {
  let component: PrescriptionListComponent;
  let fixture: ComponentFixture<PrescriptionListComponent>;
  let pharmacyService: jasmine.SpyObj<PharmacyService>;

  const mockPrescriptions = [createMockPrescription(), createMockPrescription(), createMockPrescription()];

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('PharmacyService', ['searchPrescriptions']);
    spy.searchPrescriptions.and.returnValue(of(createMockPagedResult(mockPrescriptions, 3)));

    await TestBed.configureTestingModule({
    
    imports: [
        PrescriptionListComponent, RouterTestingModule, NoopAnimationsModule,
        MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
        MatSelectModule, MatIconModule, MatButtonModule, MatProgressBarModule,
        ReactiveFormsModule, CommonModule],
    providers: [
        { provide: PharmacyService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(PrescriptionListComponent);
    component = fixture.componentInstance;
    pharmacyService = TestBed.inject(PharmacyService) as jasmine.SpyObj<PharmacyService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load prescriptions on init', () => {
    expect(pharmacyService.searchPrescriptions).toHaveBeenCalled();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Danh sách đơn thuốc');
  });

  it('should show create button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const btn = compiled.querySelector('button[routerLink="/pharmacy/prescriptions/new"]');
    expect(btn).toBeTruthy();
  });

  it('should display prescription rows', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('mat-row');
    expect(rows.length).toBe(3);
  });

  it('should have status filter', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const selects = compiled.querySelectorAll('mat-select');
    expect(selects.length).toBeGreaterThanOrEqual(1);
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
