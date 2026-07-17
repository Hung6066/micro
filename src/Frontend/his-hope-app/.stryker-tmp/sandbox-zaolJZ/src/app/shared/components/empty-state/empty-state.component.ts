// @ts-nocheck
import { Component, Input, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty-state">
      <mat-icon class="empty-icon">{{ icon }}</mat-icon>
      <h3 class="empty-title">{{ title }}</h3>
      <p class="empty-message" *ngIf="message">{{ message }}</p>
      <ng-content></ng-content>
    </div>
  `,
  styles: [`
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 48px 24px;
      text-align: center;
    }
    .empty-icon {
      font-size: 64px;
      width: 64px;
      height: 64px;
      color: #bdbdbd;
      margin-bottom: 16px;
    }
    .empty-title {
      font-size: 18px;
      font-weight: 500;
      color: #616161;
      margin: 0 0 8px 0;
    }
    .empty-message {
      font-size: 14px;
      color: #9e9e9e;
      margin: 0 0 16px 0;
      max-width: 400px;
    }
  `],
})
export class EmptyStateComponent {
  @Input() icon = 'info';
  @Input() title = 'No data found';
  @Input() message = '';
}
