import { Component } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HisHopeTooltipDirective } from './his-hope-tooltip.directive';

@Component({
  standalone: true,
  imports: [HisHopeTooltipDirective],
  template: `<button hhTooltip="Delete this row" hhTooltipPosition="below">Delete</button>`,
})
class HostComponent {}

describe('HisHopeTooltipDirective', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  afterEach(() => document.querySelectorAll('.hh-tooltip').forEach((el) => el.remove()));

  it('shows the tooltip panel after the hover delay and sets aria-describedby', fakeAsync(() => {
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    button.dispatchEvent(new Event('mouseenter'));
    tick(300);
    fixture.detectChanges();
    const panel = document.querySelector('.hh-tooltip');
    expect(panel?.textContent).toBe('Delete this row');
    expect(button.getAttribute('aria-describedby')).toBeTruthy();
    button.dispatchEvent(new Event('mouseleave'));
  }));

  it('hides on mouseleave before the delay elapses', fakeAsync(() => {
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    button.dispatchEvent(new Event('mouseenter'));
    tick(100);
    button.dispatchEvent(new Event('mouseleave'));
    tick(300);
    fixture.detectChanges();
    expect(document.querySelector('.hh-tooltip')).toBeFalsy();
  }));

  it('hides on Escape', fakeAsync(() => {
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    button.dispatchEvent(new Event('mouseenter'));
    tick(300);
    fixture.detectChanges();
    button.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(document.querySelector('.hh-tooltip')).toBeFalsy();
  }));
});
