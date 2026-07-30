import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HisHopeStateComponent, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';
import { NativeCapabilityService } from './core/native-capability.service';

@Component({
  standalone: true,
  imports: [HisHopeStateComponent, HisHopeTranslatePipe],
  template: `
    @if (status() === 'loading') {
      <hh-state kind="loading" [message]="'mobile.approveSignIn' | hhTranslate" />
    } @else if (status() === 'approved') {
      <hh-state kind="empty" [message]="'mobile.signInApproved' | hhTranslate" />
    } @else if (status() === 'rejected') {
      <hh-state kind="error" [message]="'mobile.nativeApprovalRejected' | hhTranslate" />
    } @else if (status() === 'expired') {
      <hh-state kind="error" [message]="'mobile.nativeApprovalExpired' | hhTranslate" />
    } @else if (status() === 'retry') {
      <hh-state kind="error" [message]="'mobile.nativeApprovalRetry' | hhTranslate" />
    } @else {
      <hh-state kind="error" [message]="'mobile.nativeApprovalFailed' | hhTranslate" />
    }
  `,
})
export class MobileNativeMfaComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly native = inject(NativeCapabilityService);
  readonly status = signal<'loading' | 'approved' | 'rejected' | 'expired' | 'retry' | 'error'>('loading');

  async ngOnInit(): Promise<void> {
    const ticket = this.route.snapshot.queryParamMap.get('ticket');
    if (!ticket) {
      this.status.set('retry');
      return;
    }

    try {
      const result = await this.native.approveMfa({ ticket });
      switch (result.status) {
        case 'approved':
          this.status.set('approved');
          break;
        case 'rejected':
          this.status.set('rejected');
          break;
        case 'expired':
          this.status.set('expired');
          break;
        case 'cancelled':
        case 'unsupported':
          this.status.set('retry');
          break;
      }
    } catch {
      this.status.set('error');
    }
  }
}
