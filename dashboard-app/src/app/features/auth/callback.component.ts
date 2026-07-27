import { Component, OnInit, inject } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/services/auth.service';

@Component({
  standalone: true,
  imports: [MatProgressSpinnerModule],
  template: `<div style="min-height:100dvh;display:grid;place-items:center;align-content:center;gap:16px">
    <mat-spinner diameter="40"></mat-spinner>
    <p>Completing sign in...</p>
  </div>`,
})
export class CallbackComponent implements OnInit {
  private authService = inject(AuthService);

  ngOnInit(): void {
    this.authService.handleCallback().subscribe();
  }
}
