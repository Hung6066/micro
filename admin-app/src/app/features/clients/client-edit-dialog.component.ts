import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AdminApiService, OidcClient } from '../../core/services/admin-api.service';
import { catchError, map } from 'rxjs/operators';
import { of } from 'rxjs';
import { HisHopeCreateDialogShellComponent, HisHopeFormLayoutComponent, HisHopeFormSectionComponent } from '@his-hope/frontend-foundation';

@Component({
  selector: 'app-client-edit-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatChipsModule,
    MatIconModule, MatSnackBarModule,
    HisHopeCreateDialogShellComponent, HisHopeFormLayoutComponent, HisHopeFormSectionComponent,
  ],
  template: `
    <form #clientForm="ngForm" (ngSubmit)="save()" novalidate>
      <hh-create-dialog-shell [title]="(isEdit ? 'Edit' : 'Create') + ' OIDC Client'" subtitle="Register an application and define how it can authenticate with His.Hope.">
        <div hhCreateDialogContent>
          <hh-form-layout>
            <hh-form-section title="Basic information" description="Identify the application and its authentication profile." [span]="2">
              <div class="form-grid">
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Client ID</mat-label>
                  <input matInput name="clientId" [(ngModel)]="form.clientId" required [disabled]="isEdit">
                  <mat-error>Client ID is required.</mat-error>
                </mat-form-field>
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Display Name</mat-label>
                  <input matInput name="displayName" [(ngModel)]="form.displayName" required>
                  <mat-error>Display name is required.</mat-error>
                </mat-form-field>
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Client Type</mat-label>
                  <mat-select name="clientType" [(ngModel)]="form.clientType">
                    <mat-option value="Public">Public</mat-option>
                    <mat-option value="Confidential">Confidential</mat-option>
                  </mat-select>
                </mat-form-field>
              </div>
            </hh-form-section>
            <hh-form-section title="Redirect URIs" description="Allow-list exact callback addresses for this client." [span]="2">
              <mat-form-field appearance="outline" class="full-width">
                <mat-label>Redirect URIs (one per line)</mat-label>
                <textarea matInput name="redirectUris" [(ngModel)]="redirectUrisText" rows="2" placeholder="https://app.example.com/auth/callback"></textarea>
                <mat-hint>Use exact HTTPS callback URLs in production.</mat-hint>
              </mat-form-field>
              <mat-form-field appearance="outline" class="full-width">
                <mat-label>Post-Logout Redirect URIs (one per line)</mat-label>
                <textarea matInput name="postLogoutUris" [(ngModel)]="postLogoutUrisText" rows="2" placeholder="https://app.example.com/auth/login"></textarea>
              </mat-form-field>
            </hh-form-section>
            <hh-form-section title="Access" description="Choose the scopes and grant types this client may request." [span]="2">
              <div class="form-grid">
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Scopes</mat-label>
                  <mat-select name="scopes" [(ngModel)]="form.scopes" multiple>
                    <mat-option value="openid">openid</mat-option><mat-option value="profile">profile</mat-option><mat-option value="email">email</mat-option><mat-option value="roles">roles</mat-option><mat-option value="hishop:permissions">hishop:permissions</mat-option><mat-option value="hishop:admin">hishop:admin</mat-option><mat-option value="offline_access">offline_access</mat-option>
                  </mat-select>
                </mat-form-field>
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Grant Types</mat-label>
                  <mat-select name="grantTypes" [(ngModel)]="form.grantTypes" multiple>
                    <mat-option value="authorization_code">Authorization Code</mat-option><mat-option value="refresh_token">Refresh Token</mat-option><mat-option value="client_credentials">Client Credentials</mat-option>
                  </mat-select>
                </mat-form-field>
              </div>
            </hh-form-section>
            <hh-form-section title="Advanced security" description="Optional public keys for private_key_jwt clients." [span]="2">
              <mat-form-field appearance="outline" class="full-width">
                <mat-label>Public JWKS</mat-label>
                <textarea matInput name="jwks" [(ngModel)]="form.jwks" rows="2" placeholder='{"keys":[...]}'></textarea>
                <mat-hint>Upload public keys only. Never paste private key material.</mat-hint>
              </mat-form-field>
            </hh-form-section>
          </hh-form-layout>
          <div *ngIf="createdSecret" class="secret-panel" role="alert">
            <strong>Copy this client secret now</strong><code>{{ createdSecret }}</code>
            <button mat-stroked-button type="button" (click)="copySecret()">Copy secret</button>
          </div>
        </div>
        <div hhCreateDialogFooter>
          <button mat-button type="button" (click)="cancel()">Cancel</button>
          <button *ngIf="createdSecret" mat-raised-button color="primary" type="button" (click)="finishCreate()">Done</button>
          <button mat-raised-button color="primary" type="submit" [disabled]="clientForm.invalid || saving">{{ saving ? 'Saving...' : 'Save' }}</button>
        </div>
      </hh-create-dialog-shell>
    </form>
  `,
  styles: [`
    :host { display: block; }
    hh-form-layout { display: block; }
    .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--form-field-gap); }
    .full-width { width: 100%; }
    .secret-panel { display: grid; gap: 10px; margin: 8px 0 16px; padding: 14px; border: 1px solid #d6e8dc; border-radius: 10px; background: #f3faf5; }
    .secret-panel code { overflow-wrap: anywhere; padding: 10px; background: #fff; border: 1px solid #d6e8dc; }
    @media (max-width: 720px) { .form-grid { grid-template-columns: minmax(0, 1fr); } }
  `],
})
export class ClientEditDialogComponent {
  private readonly api = inject(AdminApiService);
  private readonly dialogRef = inject(MatDialogRef<ClientEditDialogComponent>);
  private readonly snackBar = inject(MatSnackBar);

  isEdit: boolean;
  saving = false;
  createdSecret: string | null = null;

  form: Partial<OidcClient> = {
    clientId: '',
    displayName: '',
    clientType: 'Public',
    scopes: ['openid', 'profile', 'email', 'roles'],
    grantTypes: ['authorization_code', 'refresh_token'],
  };

  redirectUrisText = '';
  postLogoutUrisText = '';

  constructor(@Inject(MAT_DIALOG_DATA) data: OidcClient | null) {
    this.isEdit = !!data;
    if (data) {
      this.form = { ...data };
      this.redirectUrisText = (data.redirectUris || []).join('\n');
      this.postLogoutUrisText = (data.postLogoutRedirectUris || []).join('\n');
    }
  }

  save(): void {
    this.saving = true;
    this.form.redirectUris = this.redirectUrisText.split('\n').filter(u => u.trim());
    this.form.postLogoutRedirectUris = this.postLogoutUrisText.split('\n').filter(u => u.trim());

    const request = this.isEdit
      ? this.api.updateClient(this.form.id!, this.form).pipe(map(result => result as OidcClient | { clientSecret?: string; clientId?: string }))
      : this.api.createClient(this.form).pipe(map(result => result as OidcClient | { clientSecret?: string; clientId?: string }));

    request.pipe(
      catchError(err => {
        this.snackBar.open('Failed to save client', 'Close', { duration: 3000 });
        this.saving = false;
        return of(null);
      }),
    ).subscribe(result => {
      if (result) {
        if (!this.isEdit && result && 'clientSecret' in result && result.clientSecret) {
          this.createdSecret = result.clientSecret;
          this.snackBar.open('Client created. Copy the secret now; it will not be shown again.', 'Close', { duration: 5000 });
          return;
        }
        this.snackBar.open('Client saved successfully', 'Close', { duration: 2000 });
        this.dialogRef.close(true);
      }
    });
  }

  copySecret(): void {
    if (this.createdSecret) navigator.clipboard?.writeText(this.createdSecret);
  }

  finishCreate(): void {
    this.dialogRef.close(true);
  }

  cancel(): void { this.dialogRef.close(); }
}
