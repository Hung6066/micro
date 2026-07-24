import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  standalone: true,
  imports: [MatProgressSpinnerModule],
  template: `<div style="min-height:100dvh;display:grid;place-items:center;align-content:center;gap:16px">
    <mat-spinner diameter="40"></mat-spinner>
    <p>Completing sign in...</p>
  </div>`,
})
export class CallbackComponent implements OnInit {
  private oidc = inject(OidcSecurityService);
  private router = inject(Router);

  ngOnInit(): void {
    this.oidc.checkAuth().subscribe(({ isAuthenticated }) => {
      const returnUrl = localStorage.getItem('auth_return_url') ?? '/resources';
      localStorage.removeItem('auth_return_url');
      this.router.navigate(isAuthenticated ? [returnUrl] : ['/auth/login']);
    });
  }
}
