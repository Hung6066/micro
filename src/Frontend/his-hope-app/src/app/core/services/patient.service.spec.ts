import { TestBed } from '@angular/core/testing';;
import {
  HttpClientTestingModule,
  HttpTestingController,
} from '@angular/common/http/testing';
import { PatientService } from './patient.service';
import { environment } from '@env/environment';

describe('PatientService', () => {
  let service: PatientService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });
    service = TestBed.inject(PatientService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should search patients with paged results', () => {
    const mockResult = {
      items: [{ id: 'pat-001', fullName: 'Test Patient' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };

    service.search('test', 1, 20).subscribe((result) => {
      expect(result.items.length).toBe(1);
      expect(result.totalCount).toBe(1);
    });

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/patients/search` && r.params.get('q') === 'test',
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should get patient by id', () => {
    const mockPatient = { id: 'pat-001', fullName: 'Test Patient' };

    service.getById('pat-001').subscribe((patient) => {
      expect(patient.id).toBe('pat-001');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/pat-001`);
    expect(req.request.method).toBe('GET');
    req.flush(mockPatient);
  });

  it('should create patient', () => {
    const request = { fullName: 'New Patient' };
    const mockPatient = { id: 'pat-002', ...request };

    service.create(request as any).subscribe((patient) => {
      expect(patient.id).toBe('pat-002');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/`);
    expect(req.request.method).toBe('POST');
    req.flush(mockPatient);
  });

  it('should update patient', () => {
    const request = { fullName: 'Updated' };
    const mockPatient = { id: 'pat-001', ...request };

    service.update('pat-001', request as any).subscribe((patient) => {
      expect(patient.fullName).toBe('Updated');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/pat-001`);
    expect(req.request.method).toBe('PUT');
    req.flush(mockPatient);
  });

  it('should deactivate patient', () => {
    service.deactivate('pat-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/pat-001/deactivate`);
    expect(req.request.method).toBe('PATCH');
    req.flush(null);
  });

  it('should get patient encounters', () => {
    const mockResult = {
      items: [{ id: 'enc-001', patientId: 'pat-001' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };

    service.getEncounters('pat-001').subscribe((result) => {
      expect(result.items.length).toBe(1);
    });

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/patients/pat-001/encounters`,
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should handle create with validation error', () => {
    service.create({ fullName: '' } as any).subscribe({
      error: (error) => {
        expect(error.status).toBe(400);
      },
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/`);
    expect(req.request.method).toBe('POST');
    req.flush('Validation error', { status: 400, statusText: 'Bad Request' });
  });

  it('should handle update not found', () => {
    service.update('non-existent', { fullName: 'Test' } as any).subscribe({
      error: (error) => {
        expect(error.status).toBe(404);
      },
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/non-existent`);
    expect(req.request.method).toBe('PUT');
    req.flush('Not found', { status: 404, statusText: 'Not Found' });
  });

  it('should handle deactivate error', () => {
    service.deactivate('pat-001').subscribe({
      error: (error) => {
        expect(error.status).toBe(500);
      },
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/pat-001/deactivate`);
    expect(req.request.method).toBe('PATCH');
    req.flush('Server error', { status: 500, statusText: 'Internal Server Error' });
  });

  it('should get patient appointments', () => {
    const mockResult = {
      items: [{ id: 'apt-001', patientId: 'pat-001', reason: 'Checkup' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };

    service.getAppointments('pat-001').subscribe((result) => {
      expect(result.items.length).toBe(1);
      expect(result.items[0].reason).toBe('Checkup');
    });

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/patients/pat-001/appointments`,
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should get prescriptions', () => {
    const mockResult = { items: [{ id: 'rx-001' }], totalCount: 1, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false };
    service.getPrescriptions('pat-001').subscribe((result) => {
      expect(result.items.length).toBe(1);
    });
    const req = httpMock.expectOne((r) => r.url === `${environment.apiUrl}/patients/pat-001/prescriptions`);
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should get lab orders', () => {
    const mockResult = { items: [{ id: 'lab-001' }], totalCount: 1, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false };
    service.getLabOrders('pat-001').subscribe((result) => {
      expect(result.items.length).toBe(1);
    });
    const req = httpMock.expectOne((r) => r.url === `${environment.apiUrl}/patients/pat-001/lab-orders`);
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should get invoices', () => {
    const mockResult = { items: [{ id: 'inv-001' }], totalCount: 1, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false };
    service.getInvoices('pat-001').subscribe((result) => {
      expect(result.items.length).toBe(1);
    });
    const req = httpMock.expectOne((r) => r.url === `${environment.apiUrl}/patients/pat-001/invoices`);
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should reactivate patient', () => {
    service.reactivate('pat-001').subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/patients/pat-001/reactivate`);
    expect(req.request.method).toBe('PATCH');
    req.flush(null);
  });

  it('should getEncounters with empty result', () => {
    service.getEncounters('pat-001').subscribe((result) => {
      expect(result.items.length).toBe(0);
    });
    const req = httpMock.expectOne((r) => r.url === `${environment.apiUrl}/patients/pat-001/encounters`);
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false });
  });

  it('should search with default params', () => {
    service.search('').subscribe();
    const req = httpMock.expectOne((r) => r.url === `${environment.apiUrl}/patients/search`);
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false });
  });
});

