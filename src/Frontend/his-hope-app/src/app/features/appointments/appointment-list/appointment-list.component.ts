import { Component } from '@angular/core';

@Component({
  selector: 'app-appointment-list',
  template: `
    <div class="appointments">
      <div class="header">
        <h1>Appointments</h1>
        <button mat-raised-button color="primary" routerLink="/appointments/new">
          <mat-icon>add</mat-icon> Schedule Appointment
        </button>
      </div>
      <mat-card>
        <mat-card-content>
          <p class="placeholder">Appointment calendar and list will be displayed here.</p>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .appointments { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .placeholder { color: #999; text-align: center; padding: 48px; }
  `],
})
export class AppointmentListComponent {}
