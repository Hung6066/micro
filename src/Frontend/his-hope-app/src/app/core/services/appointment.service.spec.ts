import { TestBed } from '@angular/core/testing';;
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AppointmentService } from './appointment.service';
import { environment } from '@env/environment';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('AppointmentService', () => {
  let service: AppointmentService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
    imports: [],
    providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
});
    service = TestBed.inject(AppointmentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should list appointments with pagination', () => {
    const mockResult = {
      items: [{ id: 'apt-001', patientId: 'pat-001' }],
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
      (r) => r.url === `${environment.apiUrl}/appointments/search`,
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should search appointments', () => {
    const mockResult = {
      items: [{ id: 'apt-002' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };

    service.search('test').subscribe((result) => {
      expect(result.totalCount).toBe(1);
    });

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/appointments/search` && r.params.get('q') === 'test',
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should get appointment by id', () => {
    service.getById('apt-001').subscribe((apt) => {
      expect(apt.id).toBe('apt-001');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/apt-001`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'apt-001' });
  });

  it('should schedule appointment', () => {
    const request = { patientId: 'pat-001', reason: 'Checkup' };
    service.schedule(request as any).subscribe((apt) => {
      expect(apt.id).toBe('apt-003');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'apt-003', ...request });
  });

  it('should cancel appointment', () => {
    service.cancel('apt-001', 'Patient no-show').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/apt-001/cancel`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ reason: 'Patient no-show' });
    req.flush(null);
  });

  it('should check in appointment', () => {
    service.checkIn('apt-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/apt-001/checkin`);
    expect(req.request.method).toBe('PUT');
    req.flush(null);
  });

  it('should check out appointment', () => {
    service.checkOut('apt-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/apt-001/checkout`);
    expect(req.request.method).toBe('PUT');
    req.flush(null);
  });

  it('should handle schedule error', () => {
    service.schedule({} as any).subscribe({
      error: (error) => {
        expect(error.status).toBe(400);
      },
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/`);
    req.flush('Bad request', { status: 400, statusText: 'Bad Request' });
  });

  it('should checkIn handles error', () => {
    service.checkIn('apt-001').subscribe({
      error: (error) => expect(error).toBeTruthy(),
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/apt-001/checkin`);
    req.flush('Error', { status: 500, statusText: 'Error' });
  });

  it('should cancel with no reason', () => {
    service.cancel('apt-001').subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/apt-001/cancel`);
    expect(req.request.body).toEqual({ reason: undefined });
    req.flush(null);
  });

  it('should search with pagination', () => {
    service.search('test', 2, 50).subscribe();
    const req = httpMock.expectOne(
      (r) => r.params.get('q') === 'test' && r.params.get('page') === '2' && r.params.get('pageSize') === '50',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 2, pageSize: 50, hasNextPage: false, hasPreviousPage: false });
  });

  it('should list with default params', () => {
    service.list().subscribe();
    const req = httpMock.expectOne(
      (r) => r.params.get('page') === '1',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false });
  });
});

