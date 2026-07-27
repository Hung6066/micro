import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { HisHopeStateComponent } from '@his-hope/frontend-foundation';
import { MobileAuthService } from './core/auth.service';

@Component({
  standalone: true,
  imports: [HisHopeStateComponent],
  template: '<hh-state kind="loading" message="Completing secure sign-in..." />',
})
export class MobileCallbackComponent implements OnInit {
  private readonly auth = inject(MobileAuthService);
  private readonly router = inject(Router);
  ngOnInit(): void { this.auth.checkAuth().subscribe(isAuthenticated => this.router.navigateByUrl(isAuthenticated ? '/admin/dashboard' : '/auth/login')); }
}
