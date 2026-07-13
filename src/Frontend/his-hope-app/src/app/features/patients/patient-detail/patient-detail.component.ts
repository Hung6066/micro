import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PatientService } from '@core/services/patient.service';
import { Patient } from '@core/models/patient.model';

@Component({
  selector: 'app-patient-detail',
  template: `
    <div class="patient-detail" *ngIf="patient">
      <div class="header">
        <div>
          <h1>{{ patient.fullName }}</h1>
          <p class="subtitle">Patient ID: {{ patient.id | slice:0:8 }}... | {{ patient.genderName }} | Age: {{ patient.age }}</p>
        </div>
        <div class="header-actions">
          <button mat-raised-button color="accent" [routerLink]="['/patients', patient.id, 'edit']">
            <mat-icon>edit</mat-icon> Edit
          </button>
          <button mat-stroked-button color="primary" [routerLink]="['/appointments']">
            <mat-icon>calendar_today</mat-icon> Schedule Appointment
          </button>
        </div>
      </div>

      <div class="detail-grid">
        <mat-card>
          <mat-card-header><mat-card-title>Personal Info</mat-card-title></mat-card-header>
          <mat-card-content>
            <p><strong>DOB:</strong> {{ patient.dateOfBirth | date:'mediumDate' }}</p>
            <p><strong>Gender:</strong> {{ patient.genderName }}</p>
            <p><strong>Blood Type:</strong> {{ patient.bloodTypeName || '-' }}</p>
            <p><strong>National ID:</strong> {{ patient.nationalId || '-' }}</p>
            <p><strong>Occupation:</strong> {{ patient.occupation || '-' }}</p>
          </mat-card-content>
        </mat-card>

        <mat-card>
          <mat-card-header><mat-card-title>Contact Info</mat-card-title></mat-card-header>
          <mat-card-content>
            <p><strong>Phone:</strong> {{ patient.phone }}</p>
            <p><strong>Email:</strong> {{ patient.email || '-' }}</p>
            <p><strong>Address:</strong> {{ patient.street }}, {{ patient.district }}, {{ patient.city }}</p>
            <p><strong>Insurance:</strong> {{ patient.insuranceId || '-' }}</p>
            <p><strong>Emergency:</strong> {{ patient.emergencyContactName || '-' }} - {{ patient.emergencyContactPhone || '-' }}</p>
          </mat-card-content>
        </mat-card>
      </div>

      <mat-card class="conditions-card">
        <mat-card-header>
          <mat-card-title>Medical Conditions ({{ patient.conditions.length }})</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <mat-list *ngIf="patient.conditions.length > 0; else noConditions">
            <mat-list-item *ngFor="let c of patient.conditions">
              <mat-icon matListItemIcon>info</mat-icon>
              <span matListItemTitle>{{ c.conditionName }} <small *ngIf="c.icd10Code">({{ c.icd10Code }})</small></span>
              <span matListItemLine>{{ c.isChronic ? 'Chronic' : 'Acute' }} | {{ c.isActive ? 'Active' : 'Resolved' }}</span>
            </mat-list-item>
          </mat-list>
          <ng-template #noConditions><p class="empty">No conditions recorded</p></ng-template>
        </mat-card-content>
      </mat-card>

      <mat-card>
        <mat-card-header>
          <mat-card-title>Allergies ({{ patient.allergies.length }})</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <mat-list *ngIf="patient.allergies.length > 0; else noAllergies">
            <mat-list-item *ngFor="let a of patient.allergies">
              <mat-icon matListItemIcon>warning</mat-icon>
              <span matListItemTitle>{{ a.allergen }}</span>
              <span matListItemLine>{{ a.reaction || 'Unknown reaction' }} | {{ a.severity || 'N/A' }}</span>
            </mat-list-item>
          </mat-list>
          <ng-template #noAllergies><p class="empty">No allergies recorded</p></ng-template>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .patient-detail { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .header-actions { display: flex; gap: 12px; }
    .subtitle { color: #666; }
    .detail-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 20px; }
    .conditions-card { margin-bottom: 20px; }
    .empty { color: #999; font-style: italic; padding: 12px; }
    mat-card-content p { margin: 8px 0; }
  `],
})
export class PatientDetailComponent implements OnInit {
  patient?: Patient;

  constructor(
    private route: ActivatedRoute,
    private patientService: PatientService,
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.params['id'];
    this.patientService.getById(id).subscribe((p) => (this.patient = p));
  }
}
