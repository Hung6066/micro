import { TestBed } from '@angular/core/testing';;
import {
  HttpClientTestingModule,
  HttpTestingController,
} from '@angular/common/http/testing';
import { BillingService } from './billing.service';
import { environment } from '@env/environment';

describe('BillingService', () => {
  let service: BillingService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });
    service = TestBed.inject(BillingService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should search invoices', () => {
    const mockResult = {
      items: [{ id: 'inv-001', invoiceNumber: 'INV-001' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };

    service.searchInvoices({ searchTerm: 'INV' }).subscribe((result) => {
      expect(result.items.length).toBe(1);
    });

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/invoices/search` && r.params.get('q') === 'INV',
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should get invoice by id', () => {
    service.getInvoice('inv-001').subscribe((inv) => {
      expect(inv.id).toBe('inv-001');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/invoices/inv-001`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'inv-001' });
  });

  it('should create invoice', () => {
    const data = { patientId: 'pat-001', lineItems: [] };
    service.createInvoice(data as any).subscribe((inv) => {
      expect(inv.id).toBe('inv-002');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/invoices/`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'inv-002', ...data });
  });

  it('should record payment', () => {
    const data = { amount: 50000, paymentMethod: 'cash' };
    service.recordPayment('inv-001', data as any).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/invoices/inv-001/payments`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('should void invoice', () => {
    service.voidInvoice('inv-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/invoices/inv-001/void`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('should get invoice by number', () => {
    service.getInvoiceByNumber('INV-001').subscribe((inv) => {
      expect(inv.id).toBe('inv-001');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/invoices/number/INV-001`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'inv-001', invoiceNumber: 'INV-001' });
  });

  it('should get patient invoices', () => {
    const mockResult = {
      items: [{ id: 'inv-001', invoiceNumber: 'INV-001' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };

    service.getPatientInvoices('pat-001').subscribe((result) => {
      expect(result.items.length).toBe(1);
    });

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/invoices/search` && r.params.get('patientId') === 'pat-001',
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should void invoice handles error', () => {
    service.voidInvoice('inv-001').subscribe({
      error: (error) => expect(error).toBeTruthy(),
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/invoices/inv-001/void`);
    req.flush('Error', { status: 500, statusText: 'Error' });
  });

  it('should search invoices with filters', () => {
    service.searchInvoices({ searchTerm: 'test', statusCode: 'PAID', page: 1, pageSize: 10 }).subscribe();
    const req = httpMock.expectOne(
      (r) => r.params.get('q') === 'test' && r.params.get('statusCode') === 'PAID',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 10, hasNextPage: false, hasPreviousPage: false });
  });
});

