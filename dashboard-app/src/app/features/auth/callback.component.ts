import { Component, OnInit, inject } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/services/auth.service';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';

@Component({
  standalone: true,
  imports: [MatProgressSpinnerModule, HisHopeTranslatePipe],
  template: `<div
    style="min-height:100dvh;display:grid;place-items:center;align-content:center;gap:16px"
  >
    <mat-spinner diameter="40"></mat-spinner>
    <p>
      {{
        'dashboard.auth.completingSignIn' | hhTranslate: 'Completing sign in...'
      }}
    </p>
  </div>`,
})
export class CallbackComponent implements OnInit {
  private authService = inject(AuthService);

  ngOnInit(): void {
    this.authService.handleCallback().subscribe();
  }
}
