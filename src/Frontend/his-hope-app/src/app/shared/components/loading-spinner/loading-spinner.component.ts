import { Component, Input, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-loading-spinner',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="loading-overlay" *ngIf="loading">
      <div class="loading-content">
        <mat-spinner [diameter]="diameter" [strokeWidth]="strokeWidth"></mat-spinner>
        <p *ngIf="message" class="loading-message">{{ message }}</p>
      </div>
    </div>
  `,
  styles: [`
    .loading-overlay {
      display: flex;
      justify-content: center;
      align-items: center;
      padding: 48px 0;
      width: 100%;
    }
    .loading-content {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
    }
    .loading-message {
      color: #666;
      font-size: 14px;
      margin: 0;
    }
  `],
})
export class LoadingSpinnerComponent {
  @Input() loading = true;
  @Input() message = '';
  @Input() diameter = 40;
  @Input() strokeWidth = 4;
}
