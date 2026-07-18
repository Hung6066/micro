import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { AppointmentService } from '@core/services/appointment.service';
import { Appointment } from '@core/models/appointment.model';

@Component({
    selector: 'app-appointment-detail',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatCardModule, MatIconModule, MatListModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    @if (appointment) {
    <div class="detail">
      <div class="header">
        <h1>Appointment Details</h1>
        <div class="status-badge" [class]="'status-' + appointment.status.toLowerCase()">
          {{ appointment.statusName || appointment.status }}
        </div>
      </div>

      <div class="card-grid">
        <mat-card>
          <mat-card-header><mat-card-title>Schedule</mat-card-title></mat-card-header>
          <mat-card-content>
            <p><strong>Date:</strong> {{ appointment.scheduledDate | date:'mediumDate' }}</p>
            <p><strong>Time:</strong> {{ appointment.startTime }} - {{ appointment.endTime }}</p>
            <p><strong>Type:</strong> {{ appointment.typeName || appointment.type }}</p>
            <p><strong>Location:</strong> {{ appointment.location || 'N/A' }}</p>
          </mat-card-content>
        </mat-card>

        <mat-card>
          <mat-card-header><mat-card-title>Participants</mat-card-title></mat-card-header>
          <mat-card-content>
            <p><strong>Patient ID:</strong> {{ appointment.patientId }}</p>
            <p><strong>Provider ID:</strong> {{ appointment.providerId }}</p>
          </mat-card-content>
        </mat-card>

        @if (appointment.reason) {
        <mat-card>
          <mat-card-header><mat-card-title>Reason</mat-card-title></mat-card-header>
          <mat-card-content><p>{{ appointment.reason }}</p></mat-card-content>
        </mat-card>
        }

        @if (appointment.notes) {
        <mat-card>
          <mat-card-header><mat-card-title>Notes</mat-card-title></mat-card-header>
          <mat-card-content><p>{{ appointment.notes }}</p></mat-card-content>
        </mat-card>
        }

        @if (appointment.cancellationReason) {
        <mat-card>
          <mat-card-header><mat-card-title>Cancellation Reason</mat-card-title></mat-card-header>
          <mat-card-content><p>{{ appointment.cancellationReason }}</p></mat-card-content>
        </mat-card>
        }
      </div>

      @if (appointment.createdAt) {
      <div class="timeline">
        <h2>Timeline</h2>
        <mat-list>
          @if (appointment.createdAt) {
          <mat-list-item>
            <mat-icon matListItemIcon>add_circle</mat-icon>
            <div matListItemTitle>Created</div>
            <div matListItemLine>{{ appointment.createdAt | date:'medium' }}</div>
          </mat-list-item>
          }
          @if (appointment.checkedInAt) {
          <mat-list-item>
            <mat-icon matListItemIcon>login</mat-icon>
            <div matListItemTitle>Checked In</div>
            <div matListItemLine>{{ appointment.checkedInAt | date:'medium' }}</div>
          </mat-list-item>
          }
          @if (appointment.checkedOutAt) {
          <mat-list-item>
            <mat-icon matListItemIcon>logout</mat-icon>
            <div matListItemTitle>Checked Out</div>
            <div matListItemLine>{{ appointment.checkedOutAt | date:'medium' }}</div>
          </mat-list-item>
          }
          @if (appointment.cancelledAt) {
          <mat-list-item>
            <mat-icon matListItemIcon>cancel</mat-icon>
            <div matListItemTitle>Cancelled</div>
            <div matListItemLine>{{ appointment.cancelledAt | date:'medium' }}</div>
          </mat-list-item>
          }
        </mat-list>
      </div>
      }
    </div>
    }
  `,
    styles: [`
    .detail { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .status-badge { padding: 4px 16px; border-radius: 16px; font-weight: 500; font-size: 14px; }
    .status-scheduled { background: #e3f2fd; color: #1565c0; }
    .status-checked_in { background: #fff3e0; color: #e65100; }
    .status-completed { background: #e8f5e9; color: #2e7d32; }
    .status-cancelled { background: #fce4ec; color: #c62828; }
    .card-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 20px; margin-bottom: 24px; }
    .timeline h2 { margin-bottom: 16px; }
  `],
})
export class AppointmentDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  appointment?: Appointment;

  constructor(
    private route: ActivatedRoute,
    private appointmentService: AppointmentService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.appointmentService.getById(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe(a => {
        this.appointment = a;
        this.cdr.markForCheck();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
