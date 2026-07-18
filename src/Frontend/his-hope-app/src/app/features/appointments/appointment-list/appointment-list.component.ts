import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppointmentService } from '@core/services/appointment.service';
import { Appointment } from '@core/models/appointment.model';

@Component({
    selector: 'app-appointment-list',
    standalone: true,
    imports: [
        CommonModule, RouterModule, ReactiveFormsModule,
        MatCardModule, MatFormFieldModule, MatInputModule, MatIconModule,
        MatTableModule, MatPaginatorModule, MatButtonModule, MatTooltipModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <div class="appointments">
      <div class="header">
        <h1>Appointments</h1>
        <button mat-raised-button color="primary" routerLink="/appointments/new">
          <mat-icon>add</mat-icon> Schedule Appointment
        </button>
      </div>

      <mat-card>
        <mat-card-header>
          <mat-form-field appearance="outline" class="search-field">
            <mat-label>Search by Patient ID</mat-label>
            <input matInput [formControl]="searchControl" placeholder="Type to search...">
            <mat-icon matSuffix>search</mat-icon>
          </mat-form-field>
        </mat-card-header>

        <mat-card-content>
          <div class="table-container" *ngIf="appointments.length > 0; else empty">
            <table mat-table [dataSource]="appointments" class="appointment-table">
              <ng-container matColumnDef="scheduledDate">
                <th mat-header-cell *matHeaderCellDef>Date</th>
                <td mat-cell *matCellDef="let a">{{ a.scheduledDate | date:'mediumDate' }}</td>
              </ng-container>

              <ng-container matColumnDef="startTime">
                <th mat-header-cell *matHeaderCellDef>Time</th>
                <td mat-cell *matCellDef="let a">{{ a.startTime }} - {{ a.endTime }}</td>
              </ng-container>

              <ng-container matColumnDef="type">
                <th mat-header-cell *matHeaderCellDef>Type</th>
                <td mat-cell *matCellDef="let a">{{ a.typeName || a.type }}</td>
              </ng-container>

              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>Status</th>
                <td mat-cell *matCellDef="let a">
                  <span class="status-badge" [class]="'status-' + a.status.toLowerCase()">
                    {{ a.statusName || a.status }}
                  </span>
                </td>
              </ng-container>

              <ng-container matColumnDef="reason">
                <th mat-header-cell *matHeaderCellDef>Reason</th>
                <td mat-cell *matCellDef="let a">{{ a.reason || '-' }}</td>
              </ng-container>

              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let a">
                  <button mat-icon-button (click)="viewDetail(a.id)" matTooltip="View details"
                          attr.aria-label="Xem chi tiết cuộc hẹn">
                    <mat-icon>visibility</mat-icon>
                  </button>
                </td>
              </ng-container>

              <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: displayedColumns;" class="clickable-row"
                  (click)="viewDetail(row.id)"></tr>
            </table>

            <mat-paginator [length]="totalCount"
              [pageSize]="pageSize"
              [pageIndex]="page - 1"
              (page)="onPageChange($event)"
              [pageSizeOptions]="[10, 20, 50]"
              showFirstLastButtons>
            </mat-paginator>
          </div>

          <ng-template #empty>
            <p class="placeholder">{{ loading ? 'Loading...' : 'No appointments found.' }}</p>
          </ng-template>
        </mat-card-content>
      </mat-card>
    </div>
  `,
    styles: [`
    .appointments { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .search-field { width: 100%; max-width: 400px; }
    .table-container { overflow-x: auto; }
    .appointment-table { width: 100%; }
    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background: #f5f5f5; }
    .placeholder { color: #999; text-align: center; padding: 48px; }

  `],
})
export class AppointmentListComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  displayedColumns = ['scheduledDate', 'startTime', 'type', 'status', 'reason', 'actions'];
  appointments: Appointment[] = [];
  totalCount = 0;
  page = 1;
  pageSize = 20;
  loading = false;
  searchControl = new FormControl('');

  constructor(
    private appointmentService: AppointmentService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadAppointments();
    this.searchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntil(this.destroy$),
    ).subscribe(query => {
      this.page = 1;
      query ? this.searchAppointments(query) : this.loadAppointments();
      this.cdr.markForCheck();
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAppointments(): void {
    this.loading = true;
    this.appointmentService.list(this.page, this.pageSize)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          this.appointments = res.items;
          this.totalCount = res.totalCount;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  searchAppointments(query: string): void {
    this.loading = true;
    this.appointmentService.search(query, this.page, this.pageSize)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          this.appointments = res.items;
          this.totalCount = res.totalCount;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  onPageChange(event: any): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    const q = this.searchControl.value;
    q ? this.searchAppointments(q) : this.loadAppointments();
  }

  viewDetail(id: string): void {
    this.router.navigate(['/appointments', id]);
  }
}
