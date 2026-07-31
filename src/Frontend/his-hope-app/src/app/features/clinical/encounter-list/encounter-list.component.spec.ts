import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { EncounterListComponent } from './encounter-list.component';
import { ClinicalService } from '@core/services/clinical.service';
import { createMockEncounter, createMockPagedResult } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HisHopePageQuery } from '@his-hope/frontend-foundation';

describe('EncounterListComponent', () => {
  let component: EncounterListComponent;
  let fixture: ComponentFixture<EncounterListComponent>;
  let clinicalService: jasmine.SpyObj<ClinicalService>;

  const mockEncounters = [createMockEncounter(), createMockEncounter()];

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('ClinicalService', ['list', 'search']);
    spy.list.and.returnValue(of(createMockPagedResult(mockEncounters, 2)));
    spy.search.and.returnValue(of(createMockPagedResult([], 0)));

    await TestBed.configureTestingModule({
    imports: [
        EncounterListComponent, RouterTestingModule, NoopAnimationsModule],
    providers: [
        { provide: ClinicalService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(EncounterListComponent);
    component = fixture.componentInstance;
    clinicalService = TestBed.inject(ClinicalService) as jasmine.SpyObj<ClinicalService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load encounters on init', () => {
    expect(clinicalService.list).toHaveBeenCalled();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Hồ sơ lâm sàng');
  });

  it('should display encounter rows', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('.hh-data-table tbody tr');
    expect(rows.length).toBe(2);
  });

  it('should have search field', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('input[placeholder="Tìm theo mã bệnh nhân..."]')).toBeTruthy();
  });

  it('should search encounters when the query changes', () => {
    const query: HisHopePageQuery = { page: 1, pageSize: 20, search: 'pat-001' };
    component.onQueryChange(query);
    expect(clinicalService.search).toHaveBeenCalledWith('pat-001', 1, 20);
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
