import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { LabCriticalAlertRuleFormComponent } from './lab-critical-alert-rule-form.component';
import { LabCriticalAlertService } from '@core/services/lab-critical-alert.service';
import { of } from 'rxjs';

describe('LabCriticalAlertRuleFormComponent', () => {
  let component: LabCriticalAlertRuleFormComponent;
  let fixture: ComponentFixture<LabCriticalAlertRuleFormComponent>;
  let alertService: jasmine.SpyObj<LabCriticalAlertService>;

  beforeEach(async () => {
    alertService = jasmine.createSpyObj<LabCriticalAlertService>('LabCriticalAlertService', ['saveCriticalAlertRule']);
    alertService.saveCriticalAlertRule.and.returnValue(of({
      id: 'rule-1',
      testCode: 'K',
      testName: 'Potassium',
      unit: 'mmol/L',
      lowCriticalValue: 2.5,
      highCriticalValue: 5.5,
      isActive: true,
    }));

    await TestBed.configureTestingModule({
      imports: [LabCriticalAlertRuleFormComponent, ReactiveFormsModule, NoopAnimationsModule],
      providers: [{ provide: LabCriticalAlertService, useValue: alertService }],
    }).compileComponents();

    fixture = TestBed.createComponent(LabCriticalAlertRuleFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should require threshold fields to include at least one limit', () => {
    component.form.patchValue({
      testCode: 'K',
      testName: 'Potassium',
      unit: 'mmol/L',
      lowCriticalValue: null,
      highCriticalValue: null,
      isActive: true,
    });

    expect(component.form.valid).toBeFalse();

    component.form.patchValue({ lowCriticalValue: 2.5 });

    expect(component.form.valid).toBeTrue();
  });

  it('should require test code and test name', () => {
    component.form.patchValue({
      unit: 'mmol/L',
      lowCriticalValue: 2.5,
      isActive: true,
    });

    expect(component.form.controls['testCode'].valid).toBeFalse();
    expect(component.form.controls['testName'].valid).toBeFalse();
  });

  it('should save the rule through the backend when clicking Lưu quy tắc', () => {
    component.form.patchValue({
      testCode: 'K',
      testName: 'Potassium',
      unit: 'mmol/L',
      lowCriticalValue: 2.5,
      highCriticalValue: null,
      isActive: true,
    });
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('button[type="button"]')?.dispatchEvent(new MouseEvent('click'));
    fixture.detectChanges();

    expect(alertService.saveCriticalAlertRule).toHaveBeenCalledWith({
      testCode: 'K',
      testName: 'Potassium',
      unit: 'mmol/L',
      lowCriticalValue: 2.5,
      highCriticalValue: null,
      isActive: true,
    });
  });
});
