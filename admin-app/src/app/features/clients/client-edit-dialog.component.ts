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
import { catchError } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-client-edit-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatChipsModule,
    MatIconModule, MatSnackBarModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ isEdit ? 'Edit' : 'Create' }} OIDC Client</h2>
    <mat-dialog-content>
      <div class="form-grid">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Client ID</mat-label>
          <input matInput [(ngModel)]="form.clientId" required [disabled]="isEdit">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Display Name</mat-label>
          <input matInput [(ngModel)]="form.displayName" required>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Client Type</mat-label>
          <mat-select [(ngModel)]="form.clientType">
            <mat-option value="Public">Public</mat-option>
            <mat-option value="Confidential">Confidential</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Redirect URIs (one per line)</mat-label>
          <textarea matInput [(ngModel)]="redirectUrisText" rows="3" placeholder="http://localhost:4202/auth/callback"></textarea>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Post-Logout Redirect URIs (one per line)</mat-label>
          <textarea matInput [(ngModel)]="postLogoutUrisText" rows="3" placeholder="http://localhost:4202/auth/login"></textarea>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Scopes</mat-label>
          <mat-select [(ngModel)]="form.scopes" multiple>
            <mat-option value="openid">openid</mat-option>
            <mat-option value="profile">profile</mat-option>
            <mat-option value="email">email</mat-option>
            <mat-option value="roles">roles</mat-option>
            <mat-option value="hishop:permissions">hishop:permissions</mat-option>
            <mat-option value="hishop:admin">hishop:admin</mat-option>
            <mat-option value="offline_access">offline_access</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Grant Types</mat-label>
          <mat-select [(ngModel)]="form.grantTypes" multiple>
            <mat-option value="authorization_code">Authorization Code</mat-option>
            <mat-option value="refresh_token">Refresh Token</mat-option>
            <mat-option value="client_credentials">Client Credentials</mat-option>
          </mat-select>
        </mat-form-field>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="saving">
        {{ saving ? 'Saving...' : 'Save' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .form-grid { display: flex; flex-direction: column; gap: 16px; padding-top: 16px; }
    .full-width { width: 100%; }
  `],
})
export class ClientEditDialogComponent {
  private readonly api = inject(AdminApiService);
  private readonly dialogRef = inject(MatDialogRef<ClientEditDialogComponent>);
  private readonly snackBar = inject(MatSnackBar);

  isEdit: boolean;
  saving = false;

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
      ? this.api.updateClient(this.form.id!, this.form)
      : this.api.createClient(this.form);

    request.pipe(
      catchError(err => {
        this.snackBar.open('Failed to save client', 'Close', { duration: 3000 });
        this.saving = false;
        return of(null);
      }),
    ).subscribe(result => {
      if (result) {
        this.snackBar.open('Client saved successfully', 'Close', { duration: 2000 });
        this.dialogRef.close(true);
      }
    });
  }
}
