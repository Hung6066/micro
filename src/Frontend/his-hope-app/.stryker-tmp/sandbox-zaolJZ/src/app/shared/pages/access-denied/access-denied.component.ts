// @ts-nocheck
import { Component, ChangeDetectionStrategy } from '@angular/core';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-access-denied',
  standalone: true,
  imports: [RouterModule, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="access-denied-container">
      <div class="access-denied-card">
        <div class="icon-wrapper">
          <mat-icon class="lock-icon">lock</mat-icon>
        </div>
        <h1 class="title">Truy cập bị từ chối</h1>
        <p class="message">
          Bạn không có quyền truy cập vào trang này.
          Vui lòng liên hệ quản trị viên nếu bạn cần được cấp quyền.
        </p>
        <div class="actions">
          <a mat-raised-button color="primary" routerLink="/dashboard">
            <mat-icon>dashboard</mat-icon>
            Về trang chính
          </a>
          <a mat-stroked-button routerLink="/auth/login">
            <mat-icon>login</mat-icon>
            Đăng nhập lại
          </a>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
    .access-denied-container {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      background: var(--bg-warm, #F7F6F3);
      padding: 24px;
    }
    .access-denied-card {
      background: #FFFFFF;
      border: 1px solid #EAEAEA;
      border-radius: 8px;
      padding: 48px 40px;
      max-width: 440px;
      width: 100%;
      text-align: center;
    }
    .icon-wrapper {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 72px;
      height: 72px;
      border-radius: 50%;
      background: #FDEBEC;
      margin: 0 auto 24px;
    }
    .lock-icon {
      font-size: 36px;
      width: 36px;
      height: 36px;
      color: #C25450;
    }
    .title {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      font-size: 24px;
      font-weight: 600;
      color: #1A1A1A;
      margin: 0 0 12px;
      line-height: 1.3;
    }
    .message {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      font-size: 15px;
      color: #787774;
      line-height: 1.6;
      margin: 0 0 32px;
    }
    .actions {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .actions a {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      min-height: 44px;
      border-radius: 6px;
    }
    `,
  ],
})
export class AccessDeniedComponent {}
