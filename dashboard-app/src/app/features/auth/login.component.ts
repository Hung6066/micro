import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
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

          <div class="login-buttons">
            <button mat-raised-button color="primary" class="full-width oidc-btn" (click)="oidcLogin()" [disabled]="checkingAuth">
              @if (checkingAuth) {
                <mat-spinner diameter="20" class="btn-spinner"></mat-spinner>
              }
              @if (!checkingAuth) {
                <mat-icon>login</mat-icon>
              }
              @if (!checkingAuth) {
                Sign in with His.Hope
              }
            </button>
          </div>
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
    .login-buttons {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .full-width {
      width: 100%;
    }
    .oidc-btn {
      height: 44px;
      font-size: 15px;
      font-weight: 500;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
    }
    .btn-spinner {
      display: inline-block;
      margin: 0 auto;
    }
  `],
})
export class LoginComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  checkingAuth = true;

  ngOnInit(): void {
    this.authService.isAuthenticated$.subscribe(isAuth => {
      this.checkingAuth = false;
      if (isAuth) {
        this.router.navigate(['/resources']);
      }
    });
  }

  oidcLogin(): void {
    this.authService.oidcLogin();
  }
}
