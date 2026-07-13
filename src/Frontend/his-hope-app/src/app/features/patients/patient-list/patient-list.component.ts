import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { PatientService } from '@core/services/patient.service';
import { Patient } from '@core/models/patient.model';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';

@Component({
  selector: 'app-patient-list',
  template: `
    <div class="patient-list">
      <div class="header">
        <h1>Patients</h1>
        <button mat-raised-button color="primary" routerLink="/patients/new">
          <mat-icon>add</mat-icon> New Patient
        </button>
      </div>

      <mat-form-field appearance="outline" class="search-field">
        <mat-label>Search patients</mat-label>
        <input matInput [formControl]="searchControl" placeholder="Name, phone, or ID...">
        <mat-icon matPrefix>search</mat-icon>
      </mat-form-field>

      <mat-table [dataSource]="patients" class="mat-elevation-z2">
        <ng-container matColumnDef="fullName">
          <mat-header-cell *matHeaderCellDef>Name</mat-header-cell>
          <mat-cell *matCellDef="let p">{{ p.fullName }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="genderName">
          <mat-header-cell *matHeaderCellDef>Gender</mat-header-cell>
          <mat-cell *matCellDef="let p">{{ p.genderName }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="dateOfBirth">
          <mat-header-cell *matHeaderCellDef>DOB</mat-header-cell>
          <mat-cell *matCellDef="let p">{{ p.dateOfBirth | date:'mediumDate' }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="age">
          <mat-header-cell *matHeaderCellDef>Age</mat-header-cell>
          <mat-cell *matCellDef="let p">{{ p.age }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="phone">
          <mat-header-cell *matHeaderCellDef>Phone</mat-header-cell>
          <mat-cell *matCellDef="let p">{{ p.phone }}</mat-cell>
        </ng-container>

        <ng-container matColumnDef="actions">
          <mat-header-cell *matHeaderCellDef>Actions</mat-header-cell>
          <mat-cell *matCellDef="let p">
            <button mat-icon-button color="primary" (click)="viewPatient(p.id)">
              <mat-icon>visibility</mat-icon>
            </button>
            <button mat-icon-button color="accent" [routerLink]="['/patients', p.id, 'edit']">
              <mat-icon>edit</mat-icon>
            </button>
          </mat-cell>
        </ng-container>

        <mat-header-row *matHeaderRowDef="displayedColumns"></mat-header-row>
        <mat-row *matRowDef="let row; columns: displayedColumns;" (click)="viewPatient(row.id)"></mat-row>
      </mat-table>

      <mat-paginator [length]="totalCount" [pageSize]="20" [pageSizeOptions]="[10, 20, 50]"
                     (page)="onPageChange($event)">
      </mat-paginator>
    </div>
  `,
  styles: [`
    .patient-list { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .search-field { width: 100%; max-width: 500px; margin-bottom: 20px; }
    mat-table { width: 100%; cursor: pointer; }
    mat-row:hover { background: #f5f5f5; }
  `],
})
export class PatientListComponent implements OnInit {
  patients: Patient[] = [];
  totalCount = 0;
  searchControl = new Subject<string>();
  displayedColumns = ['fullName', 'genderName', 'dateOfBirth', 'age', 'phone', 'actions'];
  private searchTerm = '';

  constructor(
    private patientService: PatientService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.searchControl.pipe(debounceTime(300), distinctUntilChanged()).subscribe((term) => {
      this.searchTerm = term;
      this.loadPatients();
    });
    this.loadPatients();
  }

  loadPatients(page = 1): void {
    this.patientService.search(this.searchTerm, page).subscribe({
      next: (result) => {
        this.patients = result.items;
        this.totalCount = result.totalCount;
      },
    });
  }

  viewPatient(id: string): void {
    this.router.navigate(['/patients', id]);
  }

  onPageChange(event: any): void {
    this.loadPatients(event.pageIndex + 1);
  }
}
