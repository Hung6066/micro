import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DashboardService } from './dashboard.service';
import { environment } from '@env/environment';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('DashboardService', () => {
  let service: DashboardService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
    imports: [],
    providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
});
    service = TestBed.inject(DashboardService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should get dashboard stats', () => {
    const mockStats = {
      totalPatients: 150,
      todayAppointments: 12,
      activeEncounters: 8,
      pendingDiagnoses: 3,
      pendingLabs: 5,
      outstandingInvoices: 20,
      lowStockMedications: 2,
      newPatientsToday: 3,
      appointmentsTomorrow: 7,
      recentEncounters: [],
      upcomingAppointments: [],
    };

    service.getStats().subscribe((stats) => {
      expect(stats.totalPatients).toBe(150);
      expect(stats.todayAppointments).toBe(12);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/dashboard/stats`);
    expect(req.request.method).toBe('GET');
    req.flush(mockStats);
  });

  it('should get recent encounters', () => {
    const mockResponse = {
      items: [{ id: 'enc-001' }, { id: 'enc-002' }],
    };

    service.getRecentEncounters(5).subscribe((res) => {
      expect(res.items.length).toBe(2);
    });

    const req = httpMock.expectOne(
      (r) => r.urlWithParams.includes('/dashboard/recent-encounters') && r.urlWithParams.includes('limit=5'),
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('should get upcoming appointments', () => {
    const mockResponse = {
      items: [{ id: 'apt-001' }],
    };

    service.getUpcomingAppointments().subscribe((res) => {
      expect(res.items.length).toBe(1);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/dashboard/upcoming-appointments`);
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('should getRecentEncounters with default limit', () => {
    service.getRecentEncounters().subscribe();
    const req = httpMock.expectOne(
      (r) => r.urlWithParams.includes('/dashboard/recent-encounters') && r.urlWithParams.includes('limit=5'),
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [] });
  });

  it('should getStats with full data', () => {
    const fullStats = {
      totalPatients: 100, todayAppointments: 5, activeEncounters: 3,
      pendingDiagnoses: 1, pendingLabs: 2, outstandingInvoices: 10,
      lowStockMedications: 0, newPatientsToday: 1, appointmentsTomorrow: 3,
      recentEncounters: [{ id: 'enc-001' }], upcomingAppointments: [{ id: 'apt-001' }],
    };
    service.getStats().subscribe((stats) => {
      expect(stats.recentEncounters.length).toBe(1);
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/dashboard/stats`);
    req.flush(fullStats);
  });
});
