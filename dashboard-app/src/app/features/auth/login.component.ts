import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="login-container">
      <mat-card class="login-card">
        <mat-card-content>
          <h2>His.Hope Dashboard</h2>
          <p>Đang xác thực...</p>
          <mat-spinner diameter="32"></mat-spinner>
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
    }
    .login-card {
      max-width: 400px;
      width: 100%;
      text-align: center;
    }
    .login-card h2 {
      margin-bottom: 16px;
      color: #1A1A1A;
    }
    .login-card mat-spinner {
      margin: 16px auto;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent implements OnInit {
  constructor(
    private readonly authService: AuthService,
    private readonly route: ActivatedRoute,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/resources';
    this.authService.handleCallback().subscribe({
      next: () => this.router.navigateByUrl(returnUrl),
      error: () => {
        this.authService.login(returnUrl);
      },
    });
  }
}
