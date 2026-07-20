import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { LabOrderDetailComponent } from './lab-order-detail.component';
import { LabService } from '@core/services/lab.service';
import { LabCriticalAlertService } from '@core/services/lab-critical-alert.service';
import { createMockLabOrder } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('LabOrderDetailComponent', () => {
  let component: LabOrderDetailComponent;
  let fixture: ComponentFixture<LabOrderDetailComponent>;
  let labService: jasmine.SpyObj<LabService>;
  let alertService: jasmine.SpyObj<LabCriticalAlertService>;
  let alertSpy: jasmine.SpyObj<LabCriticalAlertService>;

  const mockLabOrder = createMockLabOrder();
  const activatedRouteStub = {
    snapshot: {
      params: {
        id: mockLabOrder.id,
      },
    },
  };
  const mockAlert = {
    id: 'alert-1',
    labOrderId: mockLabOrder.id,
    labTestId: mockLabOrder.tests[0].id,
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
    acknowledgedAt: null,
    acknowledgedByUserId: null,
    acknowledgedByDisplayName: null,
    resolvedAt: null,
    resolvedByUserId: null,
    resolvedByDisplayName: null,
    auditEntries: [],
  } as const;

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('LabService', ['getLabOrder', 'submitLabOrder', 'collectSpecimen', 'cancelLabOrder', 'recordResult']);
    spy.getLabOrder.and.returnValue(of(mockLabOrder));
    alertSpy = jasmine.createSpyObj('LabCriticalAlertService', ['listCriticalAlerts', 'acknowledgeCriticalAlert']);
    alertSpy.listCriticalAlerts.and.returnValue(of([mockAlert] as any));
    alertSpy.acknowledgeCriticalAlert.and.returnValue(of({ ...mockAlert, status: 'ACKNOWLEDGED' } as any));

    await TestBed.configureTestingModule({
    
    imports: [
        LabOrderDetailComponent, RouterTestingModule, NoopAnimationsModule,
        ReactiveFormsModule, MatCardModule, MatTableModule, MatButtonModule,
        MatIconModule, MatSnackBarModule, MatProgressSpinnerModule,
        MatFormFieldModule, MatInputModule, MatSelectModule, CommonModule],
    providers: [
        { provide: LabService, useValue: spy },
        { provide: LabCriticalAlertService, useValue: alertSpy },
        { provide: ActivatedRoute, useValue: activatedRouteStub },
        { provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open']) },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(LabOrderDetailComponent);
    component = fixture.componentInstance;
    labService = TestBed.inject(LabService) as jasmine.SpyObj<LabService>;
    alertService = TestBed.inject(LabCriticalAlertService) as jasmine.SpyObj<LabCriticalAlertService>;
    jest.spyOn(component as any, 'notify').mockImplementation(() => undefined);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load lab order on init', () => {
    expect(labService.getLabOrder).toHaveBeenCalled();
  });

  it('should display lab order info', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Phiếu xét nghiệm');
  });

  it('should render tests table', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const tables = compiled.querySelectorAll('mat-table');
    expect(tables.length).toBeGreaterThanOrEqual(1);
  });

  it('should have result form initialized', () => {
    expect(component.resultForm.contains('value')).toBeTrue();
    expect(component.resultForm.contains('unit')).toBeTrue();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should show critical alert metadata when the order has an active alert', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Cảnh báo nghiêm trọng');
    expect(compiled.textContent).toContain('Critical potassium');
  });

  it('should acknowledge a critical alert from the detail panel', () => {
    component.acknowledgeCriticalAlert(mockAlert as any);
    expect(alertService.acknowledgeCriticalAlert).toHaveBeenCalledWith('alert-1');
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
