import { TestBed } from '@angular/core/testing';;
import {
  HttpClientTestingModule,
  HttpTestingController,
} from '@angular/common/http/testing';
import { LabService } from './lab.service';
import { environment } from '@env/environment';

describe('LabService', () => {
  let service: LabService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });
    service = TestBed.inject(LabService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should search lab orders', () => {
    const mockResult = {
      items: [{ id: 'lab-001', testName: 'CBC' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };

    service.searchLabOrders({ searchTerm: 'CBC' }).subscribe((result) => {
      expect(result.items.length).toBe(1);
    });

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/lab-orders/search` && r.params.get('q') === 'CBC',
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should get lab order by id', () => {
    service.getLabOrder('lab-001').subscribe((order) => {
      expect(order.id).toBe('lab-001');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/lab-orders/lab-001`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'lab-001' });
  });

  it('should create lab order', () => {
    const data = { patientId: 'pat-001', testCode: 'CBC' };
    service.createLabOrder(data as any).subscribe((order) => {
      expect(order.id).toBe('lab-002');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/lab-orders/`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'lab-002', ...data });
  });

  it('should submit lab order', () => {
    service.submitLabOrder('lab-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/lab-orders/lab-001/submit`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('should collect specimen', () => {
    service.collectSpecimen('lab-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/lab-orders/lab-001/collect`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('should cancel lab order', () => {
    service.cancelLabOrder('lab-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/lab-orders/lab-001/cancel`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('should record lab result', () => {
    const data = { value: '5.5', unit: 'g/dL' };
    service.recordResult('lab-001', data as any).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/lab-orders/lab-001/result`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(data);
    req.flush(null);
  });

  it('should get patient lab orders', () => {
    const mockResult = {
      items: [{ id: 'lab-001', testName: 'CBC' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };
    service.getPatientLabOrders('pat-001').subscribe((result) => {
      expect(result.items.length).toBe(1);
    });
    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/lab-orders/search` && r.params.get('patientId') === 'pat-001',
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should search with filters', () => {
    service.searchLabOrders({ statusCode: 'PENDING', page: 1, pageSize: 10 }).subscribe();
    const req = httpMock.expectOne(
      (r) => r.params.get('statusCode') === 'PENDING' && r.params.get('page') === '1',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 10, hasNextPage: false, hasPreviousPage: false });
  });

  it('should handle submit error', () => {
    service.submitLabOrder('lab-001').subscribe({
      error: (error) => expect(error).toBeTruthy(),
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/lab-orders/lab-001/submit`);
    req.flush('Error', { status: 500, statusText: 'Error' });
  });

  it('should create lab order handles error', () => {
    service.createLabOrder({} as any).subscribe({
      error: (error) => expect(error).toBeTruthy(),
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/lab-orders/`);
    req.flush('Error', { status: 400, statusText: 'Bad Request' });
  });

  it('should search with patientId filter', () => {
    service.searchLabOrders({ patientId: 'pat-001' }).subscribe();
    const req = httpMock.expectOne((r) => r.params.get('patientId') === 'pat-001');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false });
  });

  it('should submit lab order handles error', () => {
    service.submitLabOrder('lab-001').subscribe({
      error: (e) => expect(e).toBeTruthy(),
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/lab-orders/lab-001/submit`);
    req.flush('Error', { status: 500, statusText: 'Internal Server Error' });
  });
});

