import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AdminApiService, Consent } from '../../core/services/admin-api.service';
import { catchError, finalize } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-consents-page',
  standalone: true,
  imports: [CommonModule, MatTableModule, MatCardModule, MatProgressSpinnerModule, MatIconModule],
  template: `
    <div class="page-header">
      <h1 class="page-title">User Consents</h1>
    </div>
    <mat-card>
      <mat-card-content>
        @if (loading) {
          <div class="loading-state"><mat-spinner diameter="32"></mat-spinner></div>
        } @else if (error) {
          <div class="error-state">
            <mat-icon>error</mat-icon>
            <p>{{ error }}</p>
            <button mat-stroked-button (click)="loadConsents()">Retry</button>
          </div>
        } @else {
          <table mat-table [dataSource]="consents" class="mat-elevation-z0">
            <ng-container matColumnDef="subject">
              <th mat-header-cell *matHeaderCellDef>Subject</th>
              <td mat-cell *matCellDef="let c">{{ c.subject }}</td>
            </ng-container>
            <ng-container matColumnDef="clientId">
              <th mat-header-cell *matHeaderCellDef>Client ID</th>
              <td mat-cell *matCellDef="let c">{{ c.clientId }}</td>
            </ng-container>
            <ng-container matColumnDef="scopes">
              <th mat-header-cell *matHeaderCellDef>Scopes</th>
              <td mat-cell *matCellDef="let c">{{ (c.scopes || []).join(', ') }}</td>
            </ng-container>
            <ng-container matColumnDef="created">
              <th mat-header-cell *matHeaderCellDef>Created</th>
              <td mat-cell *matCellDef="let c">{{ c.created | date:'medium' }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
          </table>
          @if (consents.length === 0) {
            <div class="empty-state">
              <mat-icon>checklist</mat-icon>
              <p>No consents recorded.</p>
            </div>
          }
        }
      </mat-card-content>
    </mat-card>
  `,
})
export class ConsentsPageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  consents: Consent[] = [];
  displayedColumns = ['subject', 'clientId', 'scopes', 'created'];
  loading = false;
  error: string | null = null;

  ngOnInit(): void { this.loadConsents(); }

  loadConsents(): void {
    this.loading = true;
    this.error = null;
    this.api.getConsents().pipe(
      finalize(() => this.loading = false),
      catchError(err => {
        this.error = 'Failed to load consents.';
        return of([]);
      }),
    ).subscribe(consents => this.consents = consents);
  }
}
