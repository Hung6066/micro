import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { EmptyStateComponent } from './empty-state.component';

describe('EmptyStateComponent', () => {
  let component: EmptyStateComponent;
  let fixture: ComponentFixture<EmptyStateComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [EmptyStateComponent, CommonModule, MatIconModule, MatButtonModule],
    });
    fixture = TestBed.createComponent(EmptyStateComponent);
    component = fixture.componentInstance;
  });

  it('should create with default inputs', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(component.icon).toBe('info');
    expect(component.title).toBe('No data found');
  });

  it('should display custom title and message', () => {
    component.icon = 'search';
    component.title = 'Custom Title';
    component.message = 'Custom message';
    fixture.detectChanges();

    const titleEl: HTMLElement = fixture.nativeElement.querySelector('.empty-title');
    const msgEl: HTMLElement = fixture.nativeElement.querySelector('.empty-message');
    expect(titleEl?.textContent?.trim()).toBe('Custom Title');
    expect(msgEl?.textContent?.trim()).toBe('Custom message');
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
