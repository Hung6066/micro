import { Component, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
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
          <mat-icon class="card-icon">local_hospital</mat-icon>
          <mat-card-title>His.Hope</mat-card-title>
          <mat-card-subtitle>Hệ thống Quản lý Bệnh viện</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <form [formGroup]="loginForm" (ngSubmit)="onSubmit()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Tên đăng nhập</mat-label>
              <input matInput formControlName="username" placeholder="Nhập tên đăng nhập">
              <mat-icon matPrefix aria-hidden="true">person</mat-icon>
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Mật khẩu</mat-label>
              <input matInput type="password" formControlName="password" placeholder="Nhập mật khẩu">
              <mat-icon matPrefix aria-hidden="true">lock</mat-icon>
            </mat-form-field>

            <button mat-raised-button color="primary" class="full-width" type="submit"
                    [disabled]="loginForm.invalid || loading" aria-live="polite">
              <mat-spinner diameter="20" *ngIf="loading" class="btn-spinner" aria-label="Đang đăng nhập"></mat-spinner>
              <span *ngIf="!loading">Đăng nhập</span>
              <span class="sr-only" *ngIf="loading">Đang xử lý đăng nhập...</span>
            </button>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
    styles: [`
    .login-container { display: flex; justify-content: center; align-items: center; height: 100vh; background: var(--bg-warm, #F7F6F3); }
    .login-card { max-width: 420px; width: 100%; }
    .card-icon { font-size: 48px; width: 48px; height: 48px; color: var(--color-primary, #2F6B4A); margin: 0 auto 16px; }
    mat-card-header { flex-direction: column; align-items: center; text-align: center; margin-bottom: 24px; }
    .full-width { width: 100%; margin-bottom: 16px; }
    form { display: flex; flex-direction: column; }
    .btn-spinner { display: inline-block; margin-right: 8px; }
  `],
})
export class LoginComponent implements OnDestroy {
  private destroy$ = new Subject<void>();

  loginForm = this.fb.group({
    username: ['', Validators.required],
    password: ['', Validators.required],
  });
  loading = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSubmit(): void {
    if (this.loginForm.invalid) return;

    this.loading = true;
    this.authService.login(this.loginForm.value as any)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open('Login successful', 'Close', { duration: 3000 });
          this.router.navigate(['/dashboard']);
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.loading = false;
          this.snackBar.open(err.error?.error || 'Login failed', 'Close', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
