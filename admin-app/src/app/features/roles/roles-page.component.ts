import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AdminApiService, Role } from '../../core/services/admin-api.service';
import { catchError, finalize } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-roles-page',
  standalone: true,
  imports: [CommonModule, MatTableModule, MatCardModule, MatProgressSpinnerModule, MatIconModule],
  template: `
    <div class="page-header">
      <h1 class="page-title">Roles</h1>
    </div>
    <mat-card>
      <mat-card-content>
        @if (loading) {
          <div class="loading-state"><mat-spinner diameter="32"></mat-spinner></div>
        } @else if (error) {
          <div class="error-state">
            <mat-icon>error</mat-icon>
            <p>{{ error }}</p>
            <button mat-stroked-button (click)="loadRoles()">Retry</button>
          </div>
        } @else {
          <table mat-table [dataSource]="roles" class="mat-elevation-z0">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Name</th>
              <td mat-cell *matCellDef="let r">{{ r.name }}</td>
            </ng-container>
            <ng-container matColumnDef="description">
              <th mat-header-cell *matHeaderCellDef>Description</th>
              <td mat-cell *matCellDef="let r">{{ r.description }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
          </table>
          @if (roles.length === 0) {
            <div class="empty-state">
              <mat-icon>badge</mat-icon>
              <p>No roles found.</p>
            </div>
          }
        }
      </mat-card-content>
    </mat-card>
  `,
})
export class RolesPageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  roles: Role[] = [];
  displayedColumns = ['name', 'description'];
  loading = false;
  error: string | null = null;

  ngOnInit(): void { this.loadRoles(); }

  loadRoles(): void {
    this.loading = true;
    this.error = null;
    this.api.getRoles().pipe(
      finalize(() => this.loading = false),
      catchError(err => {
        this.error = 'Failed to load roles.';
        return of([]);
      }),
    ).subscribe(roles => this.roles = roles);
  }
}
