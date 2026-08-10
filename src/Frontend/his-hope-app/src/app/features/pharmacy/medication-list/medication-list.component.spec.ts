import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { MedicationListComponent } from './medication-list.component';
import { PharmacyService } from '@core/services/pharmacy.service';
import { createMockMedication, createMockPagedResult } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HisHopePageQuery } from '@his-hope/frontend-foundation';

describe('MedicationListComponent', () => {
  let component: MedicationListComponent;
  let fixture: ComponentFixture<MedicationListComponent>;
  let pharmacyService: jasmine.SpyObj<PharmacyService>;

  const mockMedications = [createMockMedication(), createMockMedication()];

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('PharmacyService', ['searchMedications']);
    spy.searchMedications.and.returnValue(of(createMockPagedResult(mockMedications, 2)));

    await TestBed.configureTestingModule({
    imports: [
        MedicationListComponent, RouterTestingModule, NoopAnimationsModule],
    providers: [
        { provide: PharmacyService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(MedicationListComponent);
    component = fixture.componentInstance;
    pharmacyService = TestBed.inject(PharmacyService) as jasmine.SpyObj<PharmacyService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load medications on init', () => {
    expect(pharmacyService.searchMedications).toHaveBeenCalled();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Danh mục thuốc');
  });

  it('should show add button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const btn = compiled.querySelector('button[routerLink="/pharmacy/medications/new"]');
    expect(btn).toBeTruthy();
  });

  it('should display medication rows', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('.hh-data-table tbody tr');
    expect(rows.length).toBe(2);
  });

  it('should search medications when the query changes', () => {
    const query: HisHopePageQuery = { page: 1, pageSize: 20, search: 'amoxicillin' };
    component.onQueryChange(query);
    expect(pharmacyService.searchMedications).toHaveBeenCalledWith(jasmine.objectContaining({ searchTerm: 'amoxicillin' }));
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
