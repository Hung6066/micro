import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AdminApiService, OidcClient } from '../../core/services/admin-api.service';
import { ClientEditDialogComponent } from './client-edit-dialog.component';
import { catchError, finalize } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-clients-page',
  standalone: true,
  imports: [
    CommonModule, MatTableModule, MatButtonModule, MatIconModule,
    MatDialogModule, MatSnackBarModule, MatCardModule, MatProgressSpinnerModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">OIDC Clients</h1>
      <button mat-raised-button color="primary" (click)="openCreateDialog()">
        <mat-icon>add</mat-icon> New Client
      </button>
    </div>

    <mat-card>
      <mat-card-content>
        @if (loading) {
          <div class="loading-state"><mat-spinner diameter="32"></mat-spinner></div>
        } @else if (error) {
          <div class="error-state">
            <mat-icon>error</mat-icon>
            <p>{{ error }}</p>
            <button mat-stroked-button (click)="loadClients()">Retry</button>
          </div>
        } @else {
          <table mat-table [dataSource]="clients" class="mat-elevation-z0">
            <ng-container matColumnDef="clientId">
              <th mat-header-cell *matHeaderCellDef>Client ID</th>
              <td mat-cell *matCellDef="let c">{{ c.clientId }}</td>
            </ng-container>
            <ng-container matColumnDef="displayName">
              <th mat-header-cell *matHeaderCellDef>Display Name</th>
              <td mat-cell *matCellDef="let c">{{ c.displayName }}</td>
            </ng-container>
            <ng-container matColumnDef="clientType">
              <th mat-header-cell *matHeaderCellDef>Type</th>
              <td mat-cell *matCellDef="let c">{{ c.clientType }}</td>
            </ng-container>
            <ng-container matColumnDef="redirectUris">
              <th mat-header-cell *matHeaderCellDef>Redirect URIs</th>
              <td mat-cell *matCellDef="let c">{{ (c.redirectUris || []).join(', ') }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef>Actions</th>
              <td mat-cell *matCellDef="let c">
                <button mat-icon-button color="primary" (click)="openEditDialog(c)" matTooltip="Edit">
                  <mat-icon>edit</mat-icon>
                </button>
                <button mat-icon-button color="warn" (click)="deleteClient(c)" matTooltip="Delete">
                  <mat-icon>delete</mat-icon>
                </button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
          </table>
          @if (clients.length === 0) {
            <div class="empty-state">
              <mat-icon>vpn_key_off</mat-icon>
              <p>No OIDC clients found. Create one to get started.</p>
            </div>
          }
        }
      </mat-card-content>
    </mat-card>
  `,
})
export class ClientsPageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  clients: OidcClient[] = [];
  displayedColumns = ['clientId', 'displayName', 'clientType', 'redirectUris', 'actions'];
  loading = false;
  error: string | null = null;

  ngOnInit(): void { this.loadClients(); }

  loadClients(): void {
    this.loading = true;
    this.error = null;
    this.api.getClients().pipe(
      finalize(() => this.loading = false),
      catchError(err => {
        this.error = 'Failed to load clients. Make sure the API is running.';
        return of([]);
      }),
    ).subscribe(clients => this.clients = clients);
  }

  openCreateDialog(): void {
    const ref = this.dialog.open(ClientEditDialogComponent, { width: '600px' });
    ref.afterClosed().subscribe(result => {
      if (result) this.loadClients();
    });
  }

  openEditDialog(client: OidcClient): void {
    const ref = this.dialog.open(ClientEditDialogComponent, {
      width: '600px',
      data: client,
    });
    ref.afterClosed().subscribe(result => {
      if (result) this.loadClients();
    });
  }

  deleteClient(client: OidcClient): void {
    if (!confirm(`Delete client "${client.clientId}"? This cannot be undone.`)) return;
    this.api.deleteClient(client.id!).pipe(
      catchError(err => {
        this.snackBar.open('Failed to delete client', 'Close', { duration: 3000 });
        return of(undefined);
      }),
    ).subscribe(() => {
      this.snackBar.open('Client deleted', 'Close', { duration: 2000 });
      this.loadClients();
    });
  }
}
