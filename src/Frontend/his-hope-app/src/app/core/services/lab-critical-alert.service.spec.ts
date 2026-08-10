import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { LabCriticalAlertService } from './lab-critical-alert.service';
import { environment } from '@env/environment';

describe('LabCriticalAlertService', () => {
  let service: LabCriticalAlertService;
  let httpMock: HttpTestingController;

  const criticalAlert = {
    id: 'alert-1',
    labOrderId: 'lab-1',
    labTestId: 'test-1',
    labResultId: 'result-1',
    ruleId: 'rule-1',
    triggerType: 'CRITICAL_FLAG',
    status: 'OPEN',
    message: 'Potassium is critically low',
    resultValue: '2.1',
    resultUnit: 'mmol/L',
    thresholdValue: 2.5,
    createdAt: '2026-07-21T00:00:00Z',
    updatedAt: '2026-07-21T00:00:00Z',
    auditEntries: [],
  } as const;

  const rule = {
    id: 'rule-1',
    testCode: 'K',
    testName: 'Potassium',
    unit: 'mmol/L',
    lowCriticalValue: 2.5,
    highCriticalValue: 5.5,
    isActive: true,
    createdAt: '2026-07-21T00:00:00Z',
    updatedAt: null,
    createdByUserId: 'usr-1',
    createdByDisplayName: 'Admin',
  } as const;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        LabCriticalAlertService,
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(LabCriticalAlertService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should list critical alerts from the alert inbox endpoint', () => {
    service.listCriticalAlerts().subscribe((alerts) => {
      expect(alerts).toEqual([criticalAlert]);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/critical-alerts`);
    expect(req.request.method).toBe('GET');
    req.flush([criticalAlert]);
  });

  it('should acknowledge a critical alert', () => {
    service.acknowledgeCriticalAlert('alert-1').subscribe((alert) => {
      expect(alert.status).toBe('ACKNOWLEDGED');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/critical-alerts/alert-1/acknowledge`);
    expect(req.request.method).toBe('POST');
    req.flush({ ...criticalAlert, status: 'ACKNOWLEDGED' });
  });

  it('should save a new critical alert rule with POST', () => {
    service.saveCriticalAlertRule({
      testCode: 'K',
      testName: 'Potassium',
      unit: 'mmol/L',
      lowCriticalValue: 2.5,
      highCriticalValue: 5.5,
      isActive: true,
    }).subscribe((savedRule) => {
      expect(savedRule.id).toBe('rule-1');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/critical-alert-rules`);
    expect(req.request.method).toBe('POST');
    req.flush(rule);
  });

  it('should update an existing critical alert rule with PUT', () => {
    service.saveCriticalAlertRule({
      id: 'rule-1',
      testCode: 'K',
      testName: 'Potassium',
      unit: 'mmol/L',
      lowCriticalValue: 2.5,
      highCriticalValue: 5.5,
      isActive: true,
    }).subscribe((savedRule) => {
      expect(savedRule.id).toBe('rule-1');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/critical-alert-rules/rule-1`);
    expect(req.request.method).toBe('PUT');
    req.flush(rule);
  });
});
