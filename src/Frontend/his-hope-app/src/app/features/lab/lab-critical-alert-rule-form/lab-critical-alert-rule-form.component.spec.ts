import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { LabCriticalAlertRuleFormComponent } from './lab-critical-alert-rule-form.component';

describe('LabCriticalAlertRuleFormComponent', () => {
  let component: LabCriticalAlertRuleFormComponent;
  let fixture: ComponentFixture<LabCriticalAlertRuleFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LabCriticalAlertRuleFormComponent, ReactiveFormsModule, NoopAnimationsModule],
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
});
