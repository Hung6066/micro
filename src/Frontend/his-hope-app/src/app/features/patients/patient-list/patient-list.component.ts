import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { PatientService } from '@core/services/patient.service';
import { Patient } from '@core/models/patient.model';
import { Subject, takeUntil } from 'rxjs';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { HisHopeDataTableCellDirective, HisHopeDataTableComponent, HisHopeFilterToolbarComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent } from '@his-hope/frontend-foundation';

@Component({
    selector: 'app-patient-list',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatTableModule, MatIconModule,
        MatPaginatorModule, MatButtonModule,
        HisHopeDataTableCellDirective, HisHopeDataTableComponent, HisHopeFilterToolbarComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader title="Patients" subtitle="Search and manage patient records">
        <button mat-raised-button color="primary" routerLink="/patients/new">
          <mat-icon>add</mat-icon> New Patient
        </button>
      </hh-page-header>

      <hh-filter-toolbar label="Patient filters" searchPlaceholder="Name, phone, or ID..."
                         searchLabel="Search patients" [resultCount]="totalCount"
                         (searchChanged)="onSearch($event)" />

      <hh-data-table label="Patients" [loading]="loading" [error]="error"
                     [empty]="!loading && !error && patients.length === 0"
                     [columns]="columns" [rows]="tableRows" [selection]="true" emptyMessage="No patients found."
                     [rowClickable]="true" (rowClick)="viewPatient(patientFromRow($event).id)"
                     (retry)="loadPatients()">
        <ng-template hhDataTableCell="dateOfBirth" let-row>{{ row['dateOfBirth'] | date:'mediumDate' }}</ng-template>
        <ng-template hhDataTableCell="actions" let-row>
          <button mat-icon-button color="primary" (click)="viewPatient(patientFromRow(row).id)" aria-label="View patient">
            <mat-icon>visibility</mat-icon>
          </button>
          <button mat-icon-button color="accent" [routerLink]="['/patients', patientFromRow(row).id, 'edit']" aria-label="Edit patient">
            <mat-icon>edit</mat-icon>
          </button>
        </ng-template>
      </hh-data-table>

      <mat-paginator [length]="totalCount" [pageSize]="20" [pageSizeOptions]="[10, 20, 50]"
                     (page)="onPageChange($event)">
      </mat-paginator>
    </hh-page-layout>
  `,
    styles: [`
    mat-table { width: 100%; cursor: pointer; }
    mat-row:hover { background: #f5f5f5; }
  `],
})
export class PatientListComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  patients: Patient[] = [];
  totalCount = 0;
  columns = [
    { key: 'fullName', label: 'Name', sortable: true },
    { key: 'genderName', label: 'Gender' },
    { key: 'dateOfBirth', label: 'DOB', sortable: true },
    { key: 'age', label: 'Age' },
    { key: 'phone', label: 'Phone' },
    { key: 'actions', label: 'Actions', hideable: false, align: 'end' as const },
  ];
  private searchTerm = '';
  loading = false;
  error = '';

  get tableRows(): Record<string, unknown>[] {
    return this.patients.map(patient => ({
      id: patient.id,
      fullName: patient.fullName,
      genderName: patient.genderName,
      dateOfBirth: patient.dateOfBirth,
      age: patient.age,
      phone: patient.phone,
      entity: patient,
    }));
  }

  patientFromRow(row: Record<string, unknown>): Patient { return row['entity'] as Patient; }

  constructor(
    private patientService: PatientService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadPatients();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadPatients(page = 1): void {
    this.loading = true;
    this.error = '';
    this.patientService.search(this.searchTerm, page)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.patients = result.items;
          this.totalCount = result.totalCount;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => { this.loading = false; this.error = 'Unable to load patients.'; this.cdr.markForCheck(); },
      });
  }

  viewPatient(id: string): void {
    this.router.navigate(['/patients', id, 'workspace']);
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.loadPatients();
  }

  onPageChange(event: any): void {
    this.loadPatients(event.pageIndex + 1);
  }
}
