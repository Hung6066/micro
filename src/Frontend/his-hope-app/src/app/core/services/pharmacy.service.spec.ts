import { TestBed } from '@angular/core/testing';;
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PharmacyService } from './pharmacy.service';
import { environment } from '@env/environment';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('PharmacyService', () => {
  let service: PharmacyService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
    imports: [],
    providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
});
    service = TestBed.inject(PharmacyService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should search medications', () => {
    const mockResult = {
      items: [{ id: 'med-001', name: 'Amoxicillin' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };

    service.searchMedications({ searchTerm: 'amox' }).subscribe((result) => {
      expect(result.items.length).toBe(1);
    });

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/medications/search` && r.params.get('q') === 'amox',
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should get medication by id', () => {
    service.getMedication('med-001').subscribe((med) => {
      expect(med.id).toBe('med-001');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/medications/med-001`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'med-001' });
  });

  it('should create medication', () => {
    const data = { name: 'New Med', genericName: 'Generic', strength: '500mg' };
    service.createMedication(data as any).subscribe((med) => {
      expect(med.name).toBe('New Med');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/medications/`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'med-002', ...data });
  });

  it('should update medication', () => {
    const data = { name: 'Updated' };
    service.updateMedication('med-001', data as any).subscribe((med) => {
      expect(med.name).toBe('Updated');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/medications/med-001`);
    expect(req.request.method).toBe('PUT');
    req.flush({ id: 'med-001', ...data });
  });

  it('should deactivate medication', () => {
    service.deactivateMedication('med-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/medications/med-001/deactivate`);
    expect(req.request.method).toBe('PATCH');
    req.flush(null);
  });

  it('should fill prescription', () => {
    service.fillPrescription('rx-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/prescriptions/rx-001/fill`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('should cancel prescription', () => {
    service.cancelPrescription('rx-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/prescriptions/rx-001/cancel`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('should create prescription', () => {
    const data = { patientId: 'pat-001', medicationId: 'med-001', dosageInstructions: 'Take 1 daily' };
    service.createPrescription(data as any).subscribe((rx) => {
      expect(rx.id).toBe('rx-002');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/prescriptions/`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'rx-002', ...data });
  });

  it('should search prescriptions', () => {
    const mockResult = {
      items: [{ id: 'rx-001', medicationName: 'Amoxicillin' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };

    service.searchPrescriptions({ searchTerm: 'amox' }).subscribe((result) => {
      expect(result.items.length).toBe(1);
    });

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/prescriptions/search` && r.params.get('q') === 'amox',
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should get patient prescriptions', () => {
    const mockResult = {
      items: [{ id: 'rx-001', medicationName: 'Amoxicillin' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };
    service.getPatientPrescriptions('pat-001').subscribe((result) => {
      expect(result.items.length).toBe(1);
    });
    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/prescriptions/search` && r.params.get('patientId') === 'pat-001',
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should fill prescription handles error', () => {
    service.fillPrescription('rx-001').subscribe({
      error: (error) => expect(error).toBeTruthy(),
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/prescriptions/rx-001/fill`);
    req.flush('Error', { status: 500, statusText: 'Error' });
  });

  it('should search medications with page params', () => {
    service.searchMedications({ searchTerm: 'test', page: 2, pageSize: 50 }).subscribe();
    const req = httpMock.expectOne(
      (r) => r.params.get('q') === 'test' && r.params.get('page') === '2' && r.params.get('pageSize') === '50',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 2, pageSize: 50, hasNextPage: false, hasPreviousPage: false });
  });
});

