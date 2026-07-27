import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HisHopeMobileIconComponent, HisHopeMobileOtpComponent, HisHopeStateComponent, HisHopeToolbarComponent } from '@his-hope/frontend-foundation';
import { catchError, finalize, of } from 'rxjs';
import QRCode from 'qrcode';
import { MobileAdminApiService, MobileMfaEnrollment } from '../core/admin-api.service';

@Component({
  standalone: true,
  imports: [RouterLink, HisHopeMobileIconComponent, HisHopeMobileOtpComponent, HisHopeStateComponent, HisHopeToolbarComponent],
  template: `
    <section class="mfa-page">
      <hh-toolbar label="MFA controls"><a hhToolbarTitle routerLink="/admin/dashboard">MFA security</a></hh-toolbar>
      @if (loading) { <hh-state kind="loading" message="Loading MFA setup..." /> }
      @else if (error) { <hh-state kind="error" [message]="error"><button type="button" class="hh-button hh-button--secondary" (click)="enroll()"><hh-mobile-icon name="refresh" />Retry</button></hh-state> }
      @else if (!enrollment) {
        <section class="mfa-card" aria-labelledby="mfa-title"><p class="eyebrow">IDENTITY SECURITY</p><h1 id="mfa-title">Authenticator app</h1><p>Use an authenticator app to add a second verification step to your hospital account.</p><button type="button" class="hh-button hh-button--primary" (click)="enroll()"><hh-mobile-icon name="mfa" />Start MFA setup</button><a routerLink="/admin/dashboard" class="mfa-cancel">Cancel</a></section>
      }
      @else if (enrollment) {
        <section class="mfa-card" aria-labelledby="mfa-title">
          <p class="eyebrow">IDENTITY SECURITY</p><h1 id="mfa-title">Authenticator app</h1>
          <p>Scan this QR code in your authenticator app, then enter the six-digit code to enable MFA.</p>
          <div class="mfa-qr" aria-labelledby="mfa-qr-title">
            <div class="mfa-qr__heading"><hh-mobile-icon name="qr" /><strong id="mfa-qr-title">Scan setup code</strong></div>
            @if (qrDataUrl) { <img class="mfa-qr__image" [src]="qrDataUrl" alt="QR code for His.Hope MFA setup" /> }
            @else if (qrError) { <p class="mfa-qr__error" role="alert"><hh-mobile-icon name="error" />{{ qrError }}</p> }
            @else { <div class="mfa-qr__loading" role="status"><hh-mobile-icon name="refresh" />Preparing secure setup code...</div> }
          </div>
          <p class="mfa-secret"><strong><hh-mobile-icon name="key" />Manual key</strong><code>{{ enrollment.secretKey }}</code></p>
          <details><summary><hh-mobile-icon name="link" />Authenticator URI</summary><code class="mfa-codes">{{ enrollment.qrCodeUri }}</code></details>
          <hh-mobile-otp label="Authenticator code" (completed)="verify($event)" />
          @if (status) { <p class="mfa-success" role="status"><hh-mobile-icon name="verified" />{{ status }}</p> }
          <details><summary><hh-mobile-icon name="key" />Recovery codes</summary><p>Store these codes securely. Each code can be used once.</p><code class="mfa-codes">{{ enrollment.recoveryCodes.join('\\n') }}</code></details>
        </section>
      }
    </section>
  `,
  styles: [`:host{display:block}.mfa-page{display:grid;gap:16px}.mfa-page hh-toolbar a{color:inherit;text-decoration:none}.mfa-card{display:grid;gap:16px;padding:20px;border:1px solid var(--border-default);border-radius:var(--radius-card);background:var(--surface-white)}.mfa-card h1{margin:0;font-size:24px}.mfa-card p{margin:0;color:var(--text-secondary);line-height:1.5}.eyebrow{color:var(--color-primary)!important;font-size:11px;font-weight:var(--font-weight-semibold);letter-spacing:.08em}.mfa-qr{display:grid;justify-items:center;gap:12px;padding:16px;border:1px solid var(--border-light);border-radius:var(--radius-card);background:var(--surface-subtle)}.mfa-qr__heading{display:flex;align-items:center;gap:8px;color:var(--text-primary)}.mfa-qr__heading .material-icons{font-size:20px;color:var(--color-primary)}.mfa-qr__image{width:min(220px,70vw);height:auto;aspect-ratio:1;border:10px solid #fff;border-radius:12px;box-shadow:0 4px 16px color-mix(in srgb,var(--text-primary) 12%,transparent);image-rendering:pixelated}.mfa-qr__loading,.mfa-qr__error{display:flex;align-items:center;gap:8px;min-height:220px;justify-content:center;text-align:center}.mfa-qr__loading .material-icons{color:var(--color-primary);animation:mfa-spin 1s linear infinite}.mfa-qr__error{color:var(--color-danger)!important}.mfa-cancel{color:var(--text-secondary);text-align:center;text-decoration:none}.mfa-secret{display:grid;gap:6px}.mfa-secret strong,.mfa-success{display:flex;align-items:center;gap:8px}.mfa-secret strong .material-icons,.mfa-success .material-icons{font-size:18px;color:var(--color-primary)}.mfa-secret code,.mfa-codes{display:block;overflow:auto;padding:10px;border-radius:var(--radius-input);background:var(--surface-subtle);color:var(--text-primary);white-space:pre-wrap;overflow-wrap:anywhere}.mfa-success{color:var(--color-success)!important;font-weight:var(--font-weight-semibold)}details{border-top:1px solid var(--border-light);padding-top:12px}summary{display:flex;align-items:center;gap:8px;cursor:pointer;font-weight:var(--font-weight-semibold)}summary .material-icons{font-size:18px;color:var(--color-primary)}@keyframes mfa-spin{to{transform:rotate(360deg)}}`],
})
export class MobileMfaComponent implements OnInit {
  private readonly api = inject(MobileAdminApiService);
  private readonly changeDetector = inject(ChangeDetectorRef);
  enrollment: MobileMfaEnrollment | null = null;
  loading = false;
  error = '';
  status = '';
  qrDataUrl = '';
  qrError = '';

  ngOnInit(): void {}

  enroll(): void {
    this.loading = true;
    this.error = '';
    this.status = '';
    this.qrDataUrl = '';
    this.qrError = '';
    this.api.enrollMfa().pipe(
      finalize(() => { this.loading = false; this.changeDetector.detectChanges(); }),
      catchError(() => { this.error = 'Unable to start MFA setup.'; return of(null); }),
    ).subscribe(value => {
      this.enrollment = value;
      if (value) void this.renderQrCode(value.qrCodeUri);
      this.changeDetector.detectChanges();
    });
  }

  private async renderQrCode(uri: string): Promise<void> {
    try {
      this.qrDataUrl = await QRCode.toDataURL(uri, {
        errorCorrectionLevel: 'M', margin: 2, width: 220,
        color: { dark: '#10251d', light: '#ffffff' },
      });
    } catch {
      this.qrError = 'The QR code could not be generated. Use the manual key below.';
    } finally {
      this.changeDetector.detectChanges();
    }
  }

  verify(code: string): void {
    if (code.length !== 6 || !this.enrollment) return;
    this.loading = true;
    this.error = '';
    this.api.verifyMfa(code).pipe(
      finalize(() => { this.loading = false; this.changeDetector.detectChanges(); }),
      catchError(() => { this.error = 'The authenticator code is invalid or expired.'; return of(null); }),
    ).subscribe(value => { if (value?.status === 'ok') this.status = 'MFA enabled successfully.'; this.changeDetector.detectChanges(); });
  }
}
