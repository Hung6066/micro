import { OverlayModule } from '@angular/cdk/overlay';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { HisHopeSelectComponent, HisHopeSelectOption } from './his-hope-select.component';

@Component({
  standalone: true,
  imports: [OverlayModule, FormsModule, HisHopeSelectComponent],
  template: `
    <hh-select
      [(ngModel)]="value"
      [options]="options"
      label="Role"
      placeholder="Choose a role"
    />
  `,
})
class HostComponent {
  value: string | null = null;
  options: HisHopeSelectOption[] = [
    { value: 'admin', label: 'Administrator' },
    { value: 'nurse', label: 'Nurse' },
    { value: 'locked', label: 'Locked option', disabled: true },
  ];
}

describe('HisHopeSelectComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  afterEach(() => document.querySelectorAll('.hh-select__panel').forEach((el) => el.remove()));

  it('shows the placeholder before a value is chosen', () => {
    expect(fixture.nativeElement.textContent).toContain('Choose a role');
  });

  it('opens the listbox panel on click', () => {
    const host: HTMLElement = fixture.nativeElement.querySelector('hh-select');
    host.click();
    fixture.detectChanges();
    expect(document.querySelector('[role="listbox"]')).toBeTruthy();
  });

  it('keeps the dropdown panel the same width as its trigger', () => {
    const host: HTMLElement = fixture.nativeElement.querySelector('hh-select');
    Object.defineProperty(host, 'getBoundingClientRect', {
      configurable: true,
      value: () => ({ width: 320, height: 40, top: 0, bottom: 40, left: 0, right: 320 }),
    });

    host.click();
    fixture.detectChanges();

    const pane = document.querySelector('.cdk-overlay-pane') as HTMLElement;
    const panel = document.querySelector('.hh-select__panel') as HTMLElement;
    expect(pane.style.width).toBe('320px');
    expect(panel.style.width).toBe('100%');
  });

  it('uses the trigger typography in the detached dropdown overlay', () => {
    const host: HTMLElement = fixture.nativeElement.querySelector('hh-select');
    host.style.fontSize = '12px';
    host.style.fontFamily = 'Test Sans';
    host.style.fontWeight = '600';
    host.style.lineHeight = '18px';

    host.click();
    fixture.detectChanges();

    const pane = document.querySelector('.cdk-overlay-pane') as HTMLElement;
    expect(pane.style.fontSize).toBe('12px');
    expect(pane.style.fontFamily).toBe('Test Sans');
    expect(pane.style.fontWeight).toBe('600');
    expect(pane.style.lineHeight).toBe('18px');
  });

  it('selects an option and updates the ngModel-bound value', () => {
    const host: HTMLElement = fixture.nativeElement.querySelector('hh-select');
    host.click();
    fixture.detectChanges();
    const option = document.querySelector('[role="option"]') as HTMLElement;
    option.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.value).toBe('admin');
    expect(document.querySelector('[role="listbox"]')).toBeFalsy();
  });

  it('does not select a disabled option', () => {
    const host: HTMLElement = fixture.nativeElement.querySelector('hh-select');
    host.click();
    fixture.detectChanges();
    const disabledOption = Array.from(
      document.querySelectorAll<HTMLElement>('[role="option"]'),
    ).find((el) => el.textContent?.includes('Locked'));
    disabledOption?.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.value).toBeNull();
  });
});
