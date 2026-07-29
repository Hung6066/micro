import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { HisHopeStateComponent, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';
import { MobileAdminApiService } from './core/admin-api.service';
import { NativeCapabilityService } from './core/native-capability.service';

@Component({
  standalone: true,
  imports: [HisHopeStateComponent, HisHopeTranslatePipe],
  template: `
    @if (status() === 'loading') {
      <hh-state kind="loading" [message]="'mobile.approveSignIn' | hhTranslate" />
    } @else if (status() === 'success') {
      <hh-state kind="empty" [message]="'mobile.signInApproved' | hhTranslate" />
    } @else {
      <hh-state kind="error" [message]="'mobile.nativeApprovalFailed' | hhTranslate" />
    }
  `,
})
export class MobileNativeMfaComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(MobileAdminApiService);
  private readonly native = inject(NativeCapabilityService);
  readonly status = signal<'loading' | 'success' | 'error'>('loading');

  async ngOnInit(): Promise<void> {
    const ticket = this.route.snapshot.queryParamMap.get('ticket');
    if (!ticket || !await this.native.nativePasskeySupported()) {
      this.status.set('error');
      return;
    }

    try {
      const challenge = await firstValueFrom(this.api.nativeMfaOptions(ticket));
      const assertion = await this.native.authenticateNativePasskey(challenge.options);
      const result = await firstValueFrom(this.api.completeNativeMfa(ticket, assertion));
      if (!result.approved) throw new Error('Native MFA was not approved.');
      this.status.set('success');
    } catch {
      this.status.set('error');
    }
  }
}
