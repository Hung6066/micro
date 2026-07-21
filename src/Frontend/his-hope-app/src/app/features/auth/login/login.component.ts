import { Component, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '@core/services/auth.service';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
    selector: 'app-login',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterModule,
        MatCardModule, MatFormFieldModule, MatInputModule,
        MatButtonModule, MatIconModule, MatProgressSpinnerModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <div class="login-container">
      <mat-card class="login-card">
        <mat-card-header>
          <span class="card-logo" aria-hidden="true"></span>
          <mat-card-title>His.Hope</mat-card-title>
          <mat-card-subtitle>Đăng nhập hệ thống</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <form [formGroup]="loginForm" (ngSubmit)="onSubmit()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Tên đăng nhập</mat-label>
              <input matInput formControlName="username" placeholder="Nhập tài khoản">
              <mat-icon matPrefix aria-hidden="true">person</mat-icon>
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Mật khẩu</mat-label>
              <input matInput type="password" formControlName="password" placeholder="Nhập mật khẩu">
              <mat-icon matPrefix aria-hidden="true">lock</mat-icon>
            </mat-form-field>

            <button mat-raised-button color="primary" class="full-width" type="submit"
                    [disabled]="loginForm.invalid || loading" aria-live="polite">
              @if (loading) {
              <mat-spinner diameter="20" class="btn-spinner" aria-label="Đang đăng nhập"></mat-spinner>
              }
              @if (!loading) {
              <span>Đăng nhập</span>
              }
              @if (loading) {
              <span class="sr-only">Đang xử lý đăng nhập...</span>
              }
            </button>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
    styles: [`
    .login-container { min-height: 100dvh; display: grid; place-items: center; padding: 24px; background: var(--bg-warm, #F7F6F3); }
    .login-card { width: min(100%, 420px); }
    .card-logo {
      position: relative;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 48px;
      height: 48px;
      margin: 0 auto 16px;
      border-radius: 10px;
      background: var(--color-primary, #2F6B4A);
      box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.18);
    }
    .card-logo::before,
    .card-logo::after {
      content: '';
      position: absolute;
      border-radius: 2px;
      background: #FFFFFF;
    }
    .card-logo::before {
      width: 24px;
      height: 6px;
    }
    .card-logo::after {
      width: 6px;
      height: 24px;
    }
    mat-card-header { flex-direction: column; align-items: center; text-align: center; margin-bottom: 24px; }
    mat-card-title { font-size: 28px; line-height: 1.1; letter-spacing: -0.02em; }
    mat-card-subtitle { color: var(--text-secondary, #787774); }
    mat-card-content { padding-top: 0; }
    .full-width { width: 100%; margin-bottom: 16px; }
    form { display: flex; flex-direction: column; }
    .btn-spinner { display: inline-block; margin-right: 8px; }
  `],
})
export class LoginComponent implements OnDestroy {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private route = inject(ActivatedRoute);
  private snackBar = inject(MatSnackBar);
  private cdr = inject(ChangeDetectorRef);
  private destroy$ = new Subject<void>();

  loginForm = this.fb.group({
    username: ['', Validators.required],
    password: ['', Validators.required],
  });
  loading = false;

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSubmit(): void {
    if (this.loginForm.invalid) return;
    this.loading = true;
    this.cdr.markForCheck();

    const { username, password } = this.loginForm.value;
    if (!username || !password) return;

    const request: import('@core/models/auth.model').LoginRequest = {
      username,
      password,
      deviceInfo: navigator.platform,
      userAgent: navigator.userAgent,
    };

    this.authService.login(request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
          const targetUrl = returnUrl || '/dashboard';
          window.location.assign(targetUrl);
        },
        error: (err) => {
          this.loading = false;
          this.snackBar.open(err.error?.error || 'Login failed', 'Close', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
