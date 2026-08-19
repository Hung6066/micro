import { OverlayModule } from '@angular/cdk/overlay';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  HisHopeMenuComponent,
  HisHopeMenuItemDirective,
  HisHopeMenuTriggerDirective,
} from './his-hope-menu.component';

@Component({
  standalone: true,
  imports: [OverlayModule, HisHopeMenuComponent, HisHopeMenuItemDirective, HisHopeMenuTriggerDirective],
  template: `
    <button [hhMenuTriggerFor]="menu">Actions</button>
    <hh-menu #menu label="Row actions">
      <button hh-menu-item (click)="edited = true">Edit</button>
      <button hh-menu-item (click)="deleted = true">Delete</button>
    </hh-menu>
  `,
})
class HostComponent {
  edited = false;
  deleted = false;
}

describe('HisHopeMenuTriggerDirective', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  afterEach(() => document.querySelectorAll('.hh-menu').forEach((el) => el.remove()));

  it('opens the menu panel on trigger click', () => {
    const trigger: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    trigger.click();
    fixture.detectChanges();
    expect(document.querySelector('.hh-menu')).toBeTruthy();
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
  });

  it('closes on Escape and restores focus to the trigger', () => {
    const trigger: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    trigger.click();
    fixture.detectChanges();
    const panel = document.querySelector('.hh-menu') as HTMLElement;
    panel.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(document.querySelector('.hh-menu')).toBeFalsy();
    expect(document.activeElement).toBe(trigger);
  });

  it('invokes the clicked menu item action', () => {
    const trigger: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    trigger.click();
    fixture.detectChanges();
    const editItem = document.querySelector('.hh-menu button') as HTMLButtonElement;
    editItem.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.edited).toBe(true);
  });
});
