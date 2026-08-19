import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { HisHopeDateRangeComponent, HisHopeDateRange } from './his-hope-date-range.component';

@Component({
  standalone: true,
  imports: [FormsModule, HisHopeDateRangeComponent],
  template: `<hh-date-range [(ngModel)]="range" label="Visit window" />`,
})
class HostComponent {
  range: HisHopeDateRange = { start: null, end: null };
}

describe('HisHopeDateRangeComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('emits the updated range on start date change', () => {
    const [startInput]: HTMLInputElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('input[type="date"]'),
    );
    startInput.value = '2026-01-10';
    startInput.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(fixture.componentInstance.range.start).toBe('2026-01-10');
  });

  it('shows a validation error when the end date precedes the start date', () => {
    fixture.componentInstance.range = { start: '2026-02-01', end: '2026-01-15' };
    fixture.detectChanges();
    const error = fixture.nativeElement.querySelector('.hh-date-range__error');
    expect(error?.textContent).toContain('End date must be on or after the start date.');
  });

  it('has no error when the range is valid', () => {
    fixture.componentInstance.range = { start: '2026-01-01', end: '2026-01-15' };
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.hh-date-range__error')).toBeFalsy();
  });
});
