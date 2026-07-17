import { TestBed } from '@angular/core/testing';;
import {
  HttpClientTestingModule,
  HttpTestingController,
} from '@angular/common/http/testing';
import { ClinicalService } from './clinical.service';
import { environment } from '@env/environment';

describe('ClinicalService', () => {
  let service: ClinicalService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });
    service = TestBed.inject(ClinicalService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should list encounters with pagination', () => {
    const mockResult = {
      items: [{ id: 'enc-001' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };

    service.list(1, 20).subscribe((result) => {
      expect(result.items.length).toBe(1);
    });

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/encounters/search`,
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should search encounters', () => {
    service.search('pain').subscribe();

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/encounters/search` && r.params.get('q') === 'pain',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false });
  });

  it('should start an encounter', () => {
    const request = { patientId: 'pat-001', encounterType: 'consultation' };
    service.start(request as any).subscribe((enc) => {
      expect(enc.id).toBe('enc-002');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/encounters/`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'enc-002', ...request });
  });

  it('should record vitals', () => {
    const request = { temperature: 37.0, heartRate: 78 };
    service.recordVitals('enc-001', request as any).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/encounters/enc-001/vitals`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('should add diagnosis', () => {
    const request = { diagnosisCode: 'J45', description: 'Asthma' };
    service.addDiagnosis('enc-001', request as any).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/encounters/enc-001/diagnosis`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(null);
  });

  it('should complete encounter', () => {
    service.complete('enc-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/encounters/enc-001/complete`);
    expect(req.request.method).toBe('PUT');
    req.flush(null);
  });

  it('should get encounter by id', () => {
    service.getById('enc-001').subscribe((enc) => {
      expect(enc.id).toBe('enc-001');
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/encounters/enc-001`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'enc-001' });
  });

  it('should start encounter handles error', () => {
    service.start({ patientId: 'pat-001', encounterType: 'consultation' } as any).subscribe({
      error: (error) => expect(error).toBeTruthy(),
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/encounters/`);
    req.flush('Error', { status: 400, statusText: 'Bad Request' });
  });

  it('should search with pagination params', () => {
    service.search('test', 2, 50).subscribe();
    const req = httpMock.expectOne(
      (r) => r.params.get('q') === 'test' && r.params.get('page') === '2' && r.params.get('pageSize') === '50',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 2, pageSize: 50, hasNextPage: false, hasPreviousPage: false });
  });
});
