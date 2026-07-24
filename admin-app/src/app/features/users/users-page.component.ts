import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AdminApiService, User } from '../../core/services/admin-api.service';
import { catchError, finalize } from 'rxjs/operators';
import { of } from 'rxjs';

@Component({
  selector: 'app-users-page',
  standalone: true,
  imports: [CommonModule, MatTableModule, MatCardModule, MatProgressSpinnerModule, MatIconModule],
  template: `
    <div class="page-header">
      <h1 class="page-title">Users</h1>
    </div>
    <mat-card>
      <mat-card-content>
        @if (loading) {
          <div class="loading-state"><mat-spinner diameter="32"></mat-spinner></div>
        } @else if (error) {
          <div class="error-state">
            <mat-icon>error</mat-icon>
            <p>{{ error }}</p>
            <button mat-stroked-button (click)="loadUsers()">Retry</button>
          </div>
        } @else {
          <table mat-table [dataSource]="users" class="mat-elevation-z0">
            <ng-container matColumnDef="id">
              <th mat-header-cell *matHeaderCellDef>ID</th>
              <td mat-cell *matCellDef="let u">{{ u.id | slice:0:8 }}...</td>
            </ng-container>
            <ng-container matColumnDef="userName">
              <th mat-header-cell *matHeaderCellDef>Username</th>
              <td mat-cell *matCellDef="let u">{{ u.userName }}</td>
            </ng-container>
            <ng-container matColumnDef="email">
              <th mat-header-cell *matHeaderCellDef>Email</th>
              <td mat-cell *matCellDef="let u">{{ u.email }}</td>
            </ng-container>
            <ng-container matColumnDef="roles">
              <th mat-header-cell *matHeaderCellDef>Roles</th>
              <td mat-cell *matCellDef="let u">{{ (u.roles || []).join(', ') }}</td>
            </ng-container>
            <ng-container matColumnDef="isActive">
              <th mat-header-cell *matHeaderCellDef>Active</th>
              <td mat-cell *matCellDef="let u">{{ u.isActive ? 'Yes' : 'No' }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
          </table>
          @if (users.length === 0) {
            <div class="empty-state">
              <mat-icon>people_outline</mat-icon>
              <p>No users found.</p>
            </div>
          }
        }
      </mat-card-content>
    </mat-card>
  `,
})
export class UsersPageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  users: User[] = [];
  displayedColumns = ['id', 'userName', 'email', 'roles', 'isActive'];
  loading = false;
  error: string | null = null;

  ngOnInit(): void { this.loadUsers(); }

  loadUsers(): void {
    this.loading = true;
    this.error = null;
    this.api.getUsers().pipe(
      finalize(() => this.loading = false),
      catchError(err => {
        this.error = 'Failed to load users.';
        return of([]);
      }),
    ).subscribe(users => this.users = users);
  }
}
