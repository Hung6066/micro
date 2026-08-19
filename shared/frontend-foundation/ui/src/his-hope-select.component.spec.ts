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
