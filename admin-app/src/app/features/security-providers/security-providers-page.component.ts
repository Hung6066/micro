import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { HisHopePageHeaderComponent, HisHopePageLayoutComponent } from '@his-hope/frontend-foundation';
import QRCode from 'qrcode';
import { firstValueFrom } from 'rxjs';
import { AdminApiService, IdentitySetting } from '../../core/services/admin-api.service';
import { HisHopeBrowserPasskeyClient } from '@his-hope/frontend-foundation';
import { environment } from '../../../environments/environment';

interface ProviderSettings {
  passkeys: { rpId: string; origins: string };
  ldap: { enabled: boolean; server: string; port: number; useSsl: boolean; searchBase: string; searchFilter: string; groupRoleMapping: string };
  saml: { enabled: boolean; issuer: string; singleSignOnDestination: string; idpMetadata: string; emailClaim: string; groupClaim: string; groupRoleMapping: string };
}

@Component({
  selector: 'app-security-providers-page',
  standalone: true,
  imports: [CommonModule, FormsModule, HisHopePageHeaderComponent, HisHopePageLayoutComponent, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule, MatSnackBarModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader title="Security providers" subtitle="Create a passkey and configure LDAP/AD or SAML for the His.Hope identity service.">
        <button mat-raised-button color="primary" (click)="save()" [disabled]="loading || saving"><mat-icon>save</mat-icon>Save settings</button>
      </hh-page-header>
      <div class="notice"><mat-icon>info</mat-icon><span>Bind passwords, signing certificates and private keys stay in Vault/environment secrets. These settings may require an identity-service restart.</span></div>
      <section class="provider-grid">
        <mat-card class="provider-card">
          <mat-card-header><mat-icon mat-card-avatar>fingerprint</mat-icon><mat-card-title>Passkey</mat-card-title><mat-card-subtitle>WebAuthn / FIDO2</mat-card-subtitle></mat-card-header>
          <mat-card-content><p>Create a passkey for the current admin account using the browser's native biometric or security-key prompt.</p><p class="status" [class.ready]="passkeyReady">{{ passkeyReady ? 'Passkey is registered for this account.' : 'No passkey is registered for this account.' }}</p><p class="error" *ngIf="passkeyError">{{ passkeyError }}</p></mat-card-content>
          <mat-card-actions><button mat-flat-button color="primary" (click)="createPasskey()" [disabled]="passkeyBusy"><mat-spinner *ngIf="passkeyBusy" diameter="18"></mat-spinner><mat-icon *ngIf="!passkeyBusy">add</mat-icon>Create passkey</button></mat-card-actions>
        </mat-card>
        <mat-card class="provider-card mfa-card">
          <mat-card-header><mat-icon mat-card-avatar>verified_user</mat-icon><mat-card-title>MFA / TOTP</mat-card-title><mat-card-subtitle>Authenticator app</mat-card-subtitle></mat-card-header>
          <mat-card-content>
            <p>Protect the current admin account with a six-digit authenticator code.</p>
            <p class="status" [class.ready]="mfa.enabled">{{ mfa.enabled ? 'MFA is enabled.' : 'MFA is not enabled.' }}</p>
            <p class="error" *ngIf="mfaError">{{ mfaError }}</p>
            <div class="mfa-setup" *ngIf="mfa.qrCodeUri">
              <strong>Step 1: Add to your authenticator app</strong>
              <div class="qr-frame" *ngIf="mfa.qrCodeDataUrl"><img [src]="mfa.qrCodeDataUrl" alt="MFA setup QR code"></div>
              <label>Secret key</label><code>{{ mfa.secretKey }}</code>
              <label>OTP URI</label><textarea readonly rows="3" [value]="mfa.qrCodeUri"></textarea>
              <strong>Step 2: Verify the six-digit code</strong>
              <mat-form-field appearance="outline"><mat-label>Authenticator code</mat-label><input matInput inputmode="numeric" maxlength="6" autocomplete="one-time-code" [(ngModel)]="mfa.verificationCode"></mat-form-field>
              <button mat-flat-button color="primary" (click)="verifyMfa()" [disabled]="mfaBusy || mfa.verificationCode.length !== 6">Verify and enable MFA</button>
              <div class="recovery" *ngIf="mfa.recoveryCodes.length"><strong>Save recovery codes now</strong><code>{{ mfa.recoveryCodes.join(' · ') }}</code></div>
            </div>
          </mat-card-content>
          <mat-card-actions><button mat-stroked-button (click)="enrollMfa()" [disabled]="mfaBusy || mfa.enabled"><mat-spinner *ngIf="mfaBusy" diameter="18"></mat-spinner><mat-icon *ngIf="!mfaBusy">security</mat-icon>{{ mfa.qrCodeUri ? 'Regenerate setup' : 'Set up MFA' }}</button></mat-card-actions>
        </mat-card>
        <mat-card class="provider-card">
          <mat-card-header><mat-icon mat-card-avatar>account_tree</mat-icon><mat-card-title>LDAP / AD</mat-card-title><mat-card-subtitle>Directory authentication</mat-card-subtitle></mat-card-header>
          <mat-card-content class="form-grid">
            <mat-checkbox [(ngModel)]="settings.ldap.enabled">Enable LDAP/AD login</mat-checkbox>
            <mat-form-field appearance="outline"><mat-label>Server</mat-label><input matInput [(ngModel)]="settings.ldap.server" placeholder="dc01.hospital.local"></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Port</mat-label><input matInput type="number" [(ngModel)]="settings.ldap.port"></mat-form-field>
            <mat-checkbox [(ngModel)]="settings.ldap.useSsl">Use LDAPS/TLS</mat-checkbox>
            <mat-form-field appearance="outline" class="wide"><mat-label>Search base</mat-label><input matInput [(ngModel)]="settings.ldap.searchBase" placeholder="DC=hospital,DC=local"></mat-form-field>
            <mat-form-field appearance="outline" class="wide"><mat-label>Search filter</mat-label><input matInput [(ngModel)]="settings.ldap.searchFilter"></mat-form-field>
            <mat-form-field appearance="outline" class="wide"><mat-label>Group → role mapping (JSON)</mat-label><textarea matInput rows="3" [(ngModel)]="settings.ldap.groupRoleMapping" placeholder='{"CN=Doctors":"Provider"}'></textarea></mat-form-field>
          </mat-card-content>
        </mat-card>
        <mat-card class="provider-card">
          <mat-card-header><mat-icon mat-card-avatar>business</mat-icon><mat-card-title>SAML SSO</mat-card-title><mat-card-subtitle>Enterprise identity provider</mat-card-subtitle></mat-card-header>
          <mat-card-content class="form-grid">
            <mat-checkbox [(ngModel)]="settings.saml.enabled">Enable SAML login</mat-checkbox>
            <mat-form-field appearance="outline" class="wide"><mat-label>Service provider issuer</mat-label><input matInput [(ngModel)]="settings.saml.issuer"></mat-form-field>
            <mat-form-field appearance="outline" class="wide"><mat-label>IdP SSO destination</mat-label><input matInput [(ngModel)]="settings.saml.singleSignOnDestination" placeholder="https://idp.example.com/sso"></mat-form-field>
            <mat-form-field appearance="outline" class="wide"><mat-label>IdP metadata</mat-label><textarea matInput rows="5" [(ngModel)]="settings.saml.idpMetadata" placeholder="Paste metadata URL"></textarea></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Email claim</mat-label><input matInput [(ngModel)]="settings.saml.emailClaim" placeholder="email"></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Group claim</mat-label><input matInput [(ngModel)]="settings.saml.groupClaim" placeholder="groups"></mat-form-field>
            <mat-form-field appearance="outline" class="wide"><mat-label>Group → role mapping (JSON)</mat-label><textarea matInput rows="3" [(ngModel)]="settings.saml.groupRoleMapping" placeholder='{"HisHope-Admins":"Admin"}'></textarea></mat-form-field>
          </mat-card-content>
        </mat-card>
      </section>
      <div class="loading" *ngIf="loading"><mat-spinner diameter="32"></mat-spinner></div>
    </hh-page-layout>
  `,
  styles: [`
    .notice{display:flex;gap:10px;align-items:flex-start;padding:14px 16px;border:1px solid var(--border-default);border-radius:var(--radius-card);background:var(--surface-muted);color:var(--text-secondary);font-size:var(--font-size-caption)}.notice mat-icon{color:var(--color-primary)}.provider-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--space-5)}.provider-card{min-height:330px;border-color:var(--border-default);background:var(--surface-white);color:var(--text-primary)}.provider-card mat-card-avatar{display:grid;place-items:center;background:var(--color-primary-soft);color:var(--color-primary)}.form-grid{display:grid;gap:var(--space-2)}.wide{grid-column:1/-1}.status{padding:10px 12px;border-radius:var(--radius-input);background:var(--surface-muted);color:var(--text-secondary);font-size:var(--font-size-caption)}.status.ready{background:var(--surface-success);color:var(--color-success)}.error{color:var(--color-danger);font-size:var(--font-size-caption)}.mfa-setup{display:grid;gap:var(--space-2);margin-top:var(--space-3)}.mfa-setup label{font-size:var(--font-size-caption);color:var(--text-muted)}.mfa-setup code,.recovery code{display:block;overflow-wrap:anywhere;padding:9px;border-radius:var(--radius-input);background:var(--surface-muted);color:var(--text-primary);font-family:var(--font-mono);font-size:var(--font-size-caption)}.mfa-setup textarea{width:100%;resize:vertical;font:var(--font-size-caption) var(--font-mono);padding:8px;border:1px solid var(--border-default);border-radius:var(--radius-input);background:var(--surface-white);color:var(--text-primary)}.qr-frame{display:grid;place-items:center;width:fit-content;padding:12px;border:1px solid var(--border-default);border-radius:var(--radius-card);background:#fff}.qr-frame img{display:block;width:180px;height:180px}.recovery{display:grid;gap:6px;padding:10px;border:1px solid var(--color-warning);background:var(--surface-warning);border-radius:var(--radius-input)}.loading{display:grid;place-items:center;padding:40px}@media(max-width:980px){.provider-grid{grid-template-columns:1fr 1fr}}@media(max-width:680px){.provider-grid{grid-template-columns:1fr}}
  `],
})
export class SecurityProvidersPageComponent {
  private readonly api = inject(AdminApiService);
  private readonly http = inject(HttpClient);
  private readonly passkeys = new HisHopeBrowserPasskeyClient();
  private readonly snackBar = inject(MatSnackBar);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly settingsByKey = new Map<string, IdentitySetting>();

  loading = true;
  saving = false;
  passkeyBusy = false;
  passkeyReady = false;
  passkeyError = '';
  mfaBusy = false;
  mfaError = '';
  mfa: { enabled: boolean; enrolledAt?: string; recoveryCodesRemaining: number; secretKey: string; qrCodeUri: string; qrCodeDataUrl: string; recoveryCodes: string[]; verificationCode: string } = { enabled: false, recoveryCodesRemaining: 0, secretKey: '', qrCodeUri: '', qrCodeDataUrl: '', recoveryCodes: [], verificationCode: '' };
  settings: ProviderSettings = { passkeys: { rpId: 'localhost', origins: '' }, ldap: { enabled: false, server: '', port: 636, useSsl: true, searchBase: '', searchFilter: '(&(objectClass=user)(objectCategory=person))', groupRoleMapping: '{\n  "CN=Doctors": "Provider"\n}' }, saml: { enabled: false, issuer: '', singleSignOnDestination: '', idpMetadata: '', emailClaim: 'email', groupClaim: 'groups', groupRoleMapping: '{}' } };

  constructor() { this.load(); this.loadPasskeyStatus(); this.loadMfaStatus(); }

  private loadPasskeyStatus(): void {
    firstValueFrom(this.http.get<{ registered: boolean }>(`${environment.authApiUrl}/passkeys/status`)).then(status => {
      this.passkeyReady = status.registered;
      this.cdr.markForCheck();
    }).catch(error => { this.passkeyError = this.readApiError(error, 'Unable to load passkey status.'); this.cdr.markForCheck(); });
  }

  private loadMfaStatus(): void {
    firstValueFrom(this.http.get<{ enabled: boolean; enrolledAt?: string; recoveryCodesRemaining: number }>(`${environment.authApiUrl}/mfa/status`)).then(status => {
      this.mfa = { ...this.mfa, ...status };
      this.cdr.markForCheck();
    }).catch(error => { this.mfaError = this.readApiError(error, 'Unable to load MFA status.'); this.cdr.markForCheck(); });
  }

  async enrollMfa(): Promise<void> {
    this.mfaBusy = true;
    this.mfaError = '';
    try {
      const enrollment = await firstValueFrom(this.http.post<{ secretKey: string; qrCodeUri: string; recoveryCodes: string[] }>(`${environment.authApiUrl}/mfa/enroll`, {}));
      this.mfa = { ...this.mfa, ...enrollment, qrCodeDataUrl: await QRCode.toDataURL(enrollment.qrCodeUri, { width: 180, margin: 1, errorCorrectionLevel: 'M' }), verificationCode: '' };
    } catch (error) { this.mfaError = this.readApiError(error, 'Unable to start MFA enrollment.'); }
    finally { this.mfaBusy = false; this.cdr.markForCheck(); }
  }

  async verifyMfa(): Promise<void> {
    this.mfaBusy = true;
    this.mfaError = '';
    try {
      await firstValueFrom(this.http.post(`${environment.authApiUrl}/mfa/verify`, { code: this.mfa.verificationCode }));
      this.mfa = { ...this.mfa, enabled: true, qrCodeUri: '', qrCodeDataUrl: '', secretKey: '', verificationCode: '' };
      this.snackBar.open('MFA enabled successfully', 'Close', { duration: 3000 });
      this.loadMfaStatus();
    } catch (error) { this.mfaError = this.readApiError(error, 'Unable to verify MFA code.'); }
    finally { this.mfaBusy = false; this.cdr.markForCheck(); }
  }

  private load(): void {
    this.api.getIdentitySettings().subscribe({ next: items => { items.forEach(item => this.settingsByKey.set(item.key, item)); this.readSettings(); this.loading = false; this.cdr.markForCheck(); }, error: () => { this.loading = false; this.snackBar.open('Unable to load provider settings', 'Close', { duration: 5000 }); this.cdr.markForCheck(); } });
  }

  private readSettings(): void {
    const value = (key: string, fallback: unknown) => this.settingsByKey.get(key)?.value ?? fallback;
    const rawMetadata = String(value('Saml2:IdPMetadata', ''));
    // Repair values saved by the earlier UI where the claim labels could be
    // appended to the metadata textarea value.
    const legacyEmailClaim = rawMetadata.match(/Email claim:\s*([^\r\n]+)/i)?.[1]?.trim();
    const legacyGroupClaim = rawMetadata.match(/Group claim:\s*([^\r\n]+)/i)?.[1]?.trim();
    const metadata = rawMetadata.replace(/\r?\n(?:Email claim|Group claim):[^\r\n]*/gi, '').trim();
    this.settings = { passkeys: { rpId: String(value('Passkeys:RpId', 'localhost')), origins: this.toText(value('Passkeys:Origins', '')) }, ldap: { enabled: this.toBool(value('Ldap:Enabled', false)), server: String(value('Ldap:Server', '')), port: Number(value('Ldap:Port', 636)), useSsl: this.toBool(value('Ldap:UseSsl', true)), searchBase: String(value('Ldap:SearchBase', '')), searchFilter: String(value('Ldap:SearchFilter', '(&(objectClass=user)(objectCategory=person))')), groupRoleMapping: String(value('Ldap:GroupRoleMapping', '{\n  "CN=Doctors": "Provider"\n}')) }, saml: { enabled: this.toBool(value('Saml2:Enabled', false)), issuer: String(value('Saml2:Issuer', '')), singleSignOnDestination: String(value('Saml2:SingleSignOnDestination', '')), idpMetadata: metadata, emailClaim: String(value('Saml2:EmailClaim', legacyEmailClaim ?? 'email')), groupClaim: String(value('Saml2:GroupClaim', legacyGroupClaim ?? 'groups')), groupRoleMapping: String(value('Saml2:GroupRoleMapping', '{}')) } };
  }

  save(): void {
    this.saving = true;
    const updates = [
      { key: 'Passkeys:RpId', value: this.settings.passkeys.rpId }, { key: 'Passkeys:Origins', value: this.settings.passkeys.origins },
      { key: 'Ldap:Enabled', value: this.settings.ldap.enabled }, { key: 'Ldap:Server', value: this.settings.ldap.server }, { key: 'Ldap:Port', value: this.settings.ldap.port }, { key: 'Ldap:UseSsl', value: this.settings.ldap.useSsl }, { key: 'Ldap:SearchBase', value: this.settings.ldap.searchBase }, { key: 'Ldap:SearchFilter', value: this.settings.ldap.searchFilter }, { key: 'Ldap:GroupRoleMapping', value: this.settings.ldap.groupRoleMapping },
      { key: 'Saml2:Enabled', value: this.settings.saml.enabled }, { key: 'Saml2:Issuer', value: this.settings.saml.issuer }, { key: 'Saml2:SingleSignOnDestination', value: this.settings.saml.singleSignOnDestination }, { key: 'Saml2:IdPMetadata', value: this.settings.saml.idpMetadata }, { key: 'Saml2:EmailClaim', value: this.settings.saml.emailClaim }, { key: 'Saml2:GroupClaim', value: this.settings.saml.groupClaim }, { key: 'Saml2:GroupRoleMapping', value: this.settings.saml.groupRoleMapping },
    ];
    this.api.saveIdentitySettings(updates).subscribe({ next: () => { this.saving = false; this.snackBar.open('Provider settings saved', 'Close', { duration: 3000 }); this.cdr.markForCheck(); }, error: () => { this.saving = false; this.snackBar.open('Unable to save provider settings', 'Close', { duration: 5000 }); this.cdr.markForCheck(); } });
  }

  async createPasskey(): Promise<void> {
    this.passkeyError = '';
    const support = await this.passkeys.support();
    if (!support.supported) { this.passkeyError = 'This browser does not support passkeys.'; return; }
    this.passkeyBusy = true;
    try {
      const options = await firstValueFrom(this.http.post<any>(`${environment.authApiUrl}/passkeys/register/options`, {}));
      options.challenge = this.decode(options.challenge);
      if (options.user?.id) options.user.id = this.decode(options.user.id);
      if (options.excludeCredentials) options.excludeCredentials.forEach((item: { id: string | Uint8Array }) => item.id = this.decode(item.id as string));
      const credential = await this.passkeys.create(options);
      const response = credential.response as AuthenticatorAttestationResponse;
      await firstValueFrom(this.http.post(`${environment.authApiUrl}/passkeys/register/complete`, { id: credential.id, rawId: this.encode(credential.rawId), type: credential.type, response: { clientDataJSON: this.encode(response.clientDataJSON), attestationObject: this.encode(response.attestationObject) } }));
      this.passkeyReady = true;
      this.snackBar.open('Passkey created successfully', 'Close', { duration: 3000 });
    } catch (error) { this.passkeyError = this.readApiError(error, 'Passkey registration failed.'); } finally { this.passkeyBusy = false; this.cdr.markForCheck(); }
  }

  private encode(value: ArrayBuffer): string { const bytes = new Uint8Array(value); let binary = ''; bytes.forEach(byte => binary += String.fromCharCode(byte)); return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, ''); }
  private readApiError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const problem = error.error as { detail?: string; title?: string } | null;
      return problem?.detail || problem?.title || fallback;
    }
    return error instanceof Error ? error.message : fallback;
  }
  private decode(value: string): Uint8Array { return Uint8Array.from(atob(value.replace(/-/g, '+').replace(/_/g, '/') + '='.repeat((4 - value.length % 4) % 4)), character => character.charCodeAt(0)); }
  private toBool(value: unknown): boolean { return value === true || value === 'true'; }
  private toText(value: unknown): string { return Array.isArray(value) ? value.join(',\n') : String(value ?? ''); }
}
