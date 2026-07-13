import { Component } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-login',
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
              <mat-label>Username</mat-label>
              <input matInput formControlName="username" placeholder="Enter username">
              <mat-icon matPrefix>person</mat-icon>
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Password</mat-label>
              <input matInput type="password" formControlName="password" placeholder="Enter password">
              <mat-icon matPrefix>lock</mat-icon>
            </mat-form-field>

            <button mat-raised-button color="primary" class="full-width" type="submit"
                    [disabled]="loginForm.invalid || loading">
              {{ loading ? 'Logging in...' : 'Login' }}
            </button>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .login-container { display: flex; justify-content: center; align-items: center; height: 100vh; background: #f5f5f5; }
    .login-card { max-width: 420px; width: 100%; padding: 24px; }
    .card-icon { font-size: 48px; width: 48px; height: 48px; color: #1a237e; margin: 0 auto 16px; }
    mat-card-header { flex-direction: column; align-items: center; text-align: center; margin-bottom: 24px; }
    .full-width { width: 100%; margin-bottom: 16px; }
    form { display: flex; flex-direction: column; }
  `],
})
export class LoginComponent {
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
  ) {}

  onSubmit(): void {
    if (this.loginForm.invalid) return;

    this.loading = true;
    this.authService.login(this.loginForm.value as any).subscribe({
      next: () => {
        this.snackBar.open('Login successful', 'Close', { duration: 3000 });
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading = false;
        this.snackBar.open(err.error?.error || 'Login failed', 'Close', { duration: 5000 });
      },
    });
  }
}
