import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule,
  ],
  template: `
    <div class="login-container">
      <mat-card class="login-card">
        <mat-card-content>
          <div class="login-header">
            <div class="logo">His.Hope</div>
            <h2>Dashboard</h2>
            <p class="subtitle">Sign in to your account</p>
          </div>

          <form [formGroup]="loginForm" (ngSubmit)="onSubmit()" class="login-form">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Username or Email</mat-label>
              <input matInput formControlName="username" placeholder="Enter your username" autocomplete="username" />
              <mat-icon matPrefix>person</mat-icon>
              @if (loginForm.get('username')?.hasError('required') && loginForm.get('username')?.touched) {
                <mat-error>Username is required</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Password</mat-label>
              <input matInput formControlName="password" type="password" placeholder="Enter your password" autocomplete="current-password" />
              <mat-icon matPrefix>lock</mat-icon>
              @if (loginForm.get('password')?.hasError('required') && loginForm.get('password')?.touched) {
                <mat-error>Password is required</mat-error>
              }
            </mat-form-field>

            @if (errorMessage) {
              <div class="error-message">{{ errorMessage }}</div>
            }

            <button mat-raised-button color="primary" type="submit" class="full-width" [disabled]="loginForm.invalid || loading">
              @if (loading) {
                <mat-spinner diameter="20" class="btn-spinner"></mat-spinner>
              } @else {
                Sign In
              }
            </button>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .login-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
      background: #F7F6F3;
      padding: 24px;
    }
    .login-card {
      max-width: 400px;
      width: 100%;
    }
    .login-header {
      text-align: center;
      margin-bottom: 32px;
    }
    .logo {
      font-size: 14px;
      font-weight: 600;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: #7C7A75;
      margin-bottom: 8px;
    }
    .login-header h2 {
      font-size: 24px;
      font-weight: 600;
      color: #1A1A1A;
      margin: 0 0 4px;
    }
    .subtitle {
      font-size: 14px;
      color: #A1A09B;
      margin: 0;
    }
    .login-form {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .full-width {
      width: 100%;
    }
    .error-message {
      background: #FEF2F2;
      color: #DC2626;
      padding: 10px 14px;
      border-radius: 6px;
      font-size: 13px;
      line-height: 1.4;
    }
    .btn-spinner {
      display: inline-block;
      margin: 0 auto;
    }
    button[type="submit"] {
      height: 44px;
      font-size: 15px;
      font-weight: 500;
      margin-top: 4px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.Default,
})
export class LoginComponent implements OnInit {
  loginForm!: FormGroup;
  loading = false;
  errorMessage = '';

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.loginForm = this.fb.group({
      username: ['', Validators.required],
      password: ['', Validators.required],
    });
  }

  onSubmit(): void {
    if (this.loginForm.invalid || this.loading) return;

    const { username, password } = this.loginForm.value;
    if (!username || !password) return;

    this.loading = true;
    this.errorMessage = '';
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/resources';

    this.authService.loginWithCredentials(username, password).subscribe({
      next: () => this.router.navigateByUrl(returnUrl),
      error: (err) => {
        this.loading = false;
        if (err.status === 401) {
          this.errorMessage = 'Invalid username or password.';
        } else if (err.status === 0) {
          this.errorMessage = 'Cannot connect to authentication service. Is the Identity service running?';
        } else {
          this.errorMessage = err.error?.detail ?? 'An error occurred during sign in.';
        }
      },
    });
  }
}
