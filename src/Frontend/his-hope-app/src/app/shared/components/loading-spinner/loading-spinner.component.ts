import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading) {
    <div class="loading-overlay">
      <div class="loading-content">
        <mat-spinner [diameter]="diameter" [strokeWidth]="strokeWidth"></mat-spinner>
        @if (message) {
        <p class="loading-message">{{ message }}</p>
        }
      </div>
    </div>
    }
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
