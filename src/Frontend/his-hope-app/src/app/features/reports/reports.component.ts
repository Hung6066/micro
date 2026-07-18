import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

@Component({
    selector: 'app-reports',
    standalone: true,
    imports: [CommonModule, MatCardModule, MatIconModule],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <div class="reports-container">
      <div class="reports-card">
        <h2 class="reports-title">Báo cáo & Thống kê</h2>
        <p class="reports-subtitle">Tính năng đang được phát triển</p>
        <p class="reports-message">Trang báo cáo và thống kê sẽ sớm được ra mắt.</p>
      </div>
    </div>
  `,
    styles: [`
    .reports-container {
      padding: 24px;
      background: var(--bg-warm, #F7F6F3);
      min-height: calc(100vh - 64px);
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .reports-card {
      background: #FFFFFF;
      border: 1px solid #EAEAEA;
      border-radius: 8px;
      padding: 32px;
      max-width: 480px;
      width: 100%;
      text-align: center;
    }
    .reports-title {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      font-size: 20px;
      font-weight: 600;
      color: #1A1A1A;
      margin: 0 0 8px;
    }
    .reports-subtitle {
      font-size: 14px;
      color: #787774;
      margin: 0 0 16px;
    }
    .reports-message {
      font-size: 15px;
      color: #787774;
      line-height: 1.6;
      margin: 0;
    }
  `],
})
export class ReportsComponent {}
