import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, BehaviorSubject } from 'rxjs';
import { LabCriticalAlertsComponent } from './lab-critical-alerts.component';
import { LabCriticalAlertService } from '@core/services/lab-critical-alert.service';
import { LabCriticalAlertStreamService } from '@core/services/lab-critical-alert-stream.service';

describe('LabCriticalAlertsComponent', () => {
  let component: LabCriticalAlertsComponent;
  let fixture: ComponentFixture<LabCriticalAlertsComponent>;
  let alertService: jasmine.SpyObj<LabCriticalAlertService>;
  let streamService: Partial<LabCriticalAlertStreamService>;

  const alerts = [
    {
      id: 'alert-1',
      labOrderId: 'lab-1',
      labTestId: 'test-1',
      labResultId: 'result-1',
      ruleId: 'rule-1',
      triggerType: 'CRITICAL_FLAG',
      status: 'OPEN',
      message: 'Critical potassium',
      resultValue: '2.1',
      resultUnit: 'mmol/L',
      thresholdValue: 2.5,
      createdAt: '2026-07-21T00:00:00Z',
      updatedAt: '2026-07-21T00:00:00Z',
      auditEntries: [],
    },
    {
      id: 'alert-2',
      labOrderId: 'lab-2',
      labTestId: 'test-2',
      labResultId: 'result-2',
      ruleId: 'rule-1',
      triggerType: 'CRITICAL_FLAG',
      status: 'ACKNOWLEDGED',
      message: 'Critical glucose',
      resultValue: '19.4',
      resultUnit: 'mmol/L',
      thresholdValue: 18,
      createdAt: '2026-07-21T00:00:00Z',
      updatedAt: '2026-07-21T00:00:00Z',
      auditEntries: [],
    },
  ] as const;

  beforeEach(async () => {
    alertService = jasmine.createSpyObj('LabCriticalAlertService', ['listCriticalAlerts', 'acknowledgeCriticalAlert', 'resolveCriticalAlert']);
    alertService.listCriticalAlerts.and.returnValue(of(alerts as any));
    alertService.acknowledgeCriticalAlert.and.returnValue(of(alerts[0] as any));
    alertService.resolveCriticalAlert.and.returnValue(of(alerts[1] as any));

    streamService = {
      unreadCount$: new BehaviorSubject(2),
      latestAlert$: new BehaviorSubject<any>(alerts[0]),
      connect: jasmine.createSpy('connect').and.returnValue(Promise.resolve()),
      disconnect: jasmine.createSpy('disconnect').and.returnValue(Promise.resolve()),
    } as any;

    await TestBed.configureTestingModule({
      imports: [LabCriticalAlertsComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: LabCriticalAlertService, useValue: alertService },
        { provide: LabCriticalAlertStreamService, useValue: streamService },
        { provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open']) },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LabCriticalAlertsComponent);
    component = fixture.componentInstance;
    jest.spyOn(component as any, 'notify').mockImplementation(() => undefined);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render open, acknowledged, and resolved filters', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Mở');
    expect(compiled.textContent).toContain('Đã ghi nhận');
    expect(compiled.textContent).toContain('Đã xử lý');
  });

  it('should show the unread badge count from the realtime stream', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('2 cảnh báo mới');
  });

  it('should allow acknowledging an open alert', () => {
    component.acknowledge(alerts[0] as any);
    expect(alertService.acknowledgeCriticalAlert).toHaveBeenCalledWith('alert-1');
  });
});
