import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HisHopeAlertComponent } from './his-hope-alert.component';

describe('HisHopeAlertComponent', () => {
  let fixture: ComponentFixture<HisHopeAlertComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HisHopeAlertComponent] }).compileComponents();
    fixture = TestBed.createComponent(HisHopeAlertComponent);
    fixture.componentRef.setInput('dismissible', true);
    fixture.detectChanges();
  });

  it('uses an alert role for error feedback', () => {
    fixture.componentRef.setInput('tone', 'error');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('emits when dismissed', () => {
    let dismissed = false;
    fixture.componentInstance.dismissed.subscribe(() => dismissed = true);
    fixture.nativeElement.querySelector('.hh-alert__close').click();
    expect(dismissed).toBeTrue();
  });
});
