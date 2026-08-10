import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { PrescriptionListComponent } from './prescription-list.component';
import { PharmacyService } from '@core/services/pharmacy.service';
import { createMockPrescription, createMockPagedResult } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HisHopePageQuery } from '@his-hope/frontend-foundation';

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
        PrescriptionListComponent, RouterTestingModule, NoopAnimationsModule],
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
    const rows = compiled.querySelectorAll('.hh-data-table tbody tr');
    expect(rows.length).toBe(3);
  });

  it('should have status filter', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const select = compiled.querySelector('.hh-status-filter select');
    expect(select).toBeTruthy();
  });

  it('should search prescriptions when the query changes', () => {
    const query: HisHopePageQuery = { page: 1, pageSize: 20, search: 'amoxicillin' };
    component.onQueryChange(query);
    expect(pharmacyService.searchPrescriptions).toHaveBeenCalledWith(jasmine.objectContaining({ searchTerm: 'amoxicillin' }));
  });

  it('should filter prescriptions by status', () => {
    component.onStatusFilterChange({ target: { value: 'filled' } } as unknown as Event);
    expect(pharmacyService.searchPrescriptions).toHaveBeenCalledWith(jasmine.objectContaining({ statusCode: 'filled' }));
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
