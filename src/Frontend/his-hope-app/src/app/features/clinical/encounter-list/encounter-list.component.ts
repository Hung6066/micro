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
import { ClinicalService } from '@core/services/clinical.service';
import { Encounter } from '@core/models/encounter.model';

@Component({
    selector: 'app-encounter-list',
    standalone: true,
    imports: [
        CommonModule, RouterModule, ReactiveFormsModule,
        MatCardModule, MatFormFieldModule, MatInputModule, MatIconModule,
        MatTableModule, MatPaginatorModule, MatButtonModule, MatTooltipModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <div class="encounters">
      <div class="header">
        <h1>Clinical Encounters</h1>
        <button mat-raised-button color="primary">
          <mat-icon>add</mat-icon> New Encounter
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
          @if (encounters.length > 0) {
          <div class="table-container">
            <table mat-table [dataSource]="encounters" class="encounter-table">
              <ng-container matColumnDef="encounterDate">
                <th mat-header-cell *matHeaderCellDef>Date</th>
                <td mat-cell *matCellDef="let e">{{ e.encounterDate | date:'medium' }}</td>
              </ng-container>

              <ng-container matColumnDef="patientId">
                <th mat-header-cell *matHeaderCellDef>Patient</th>
                <td mat-cell *matCellDef="let e">{{ e.patientId }}</td>
              </ng-container>

              <ng-container matColumnDef="encounterType">
                <th mat-header-cell *matHeaderCellDef>Type</th>
                <td mat-cell *matCellDef="let e">{{ e.encounterTypeName || e.encounterType }}</td>
              </ng-container>

              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>Status</th>
                <td mat-cell *matCellDef="let e">
                  <span class="status-badge" [class]="'status-' + e.status.toLowerCase()">
                    {{ e.statusName || e.status }}
                  </span>
                </td>
              </ng-container>

              <ng-container matColumnDef="chiefComplaint">
                <th mat-header-cell *matHeaderCellDef>Chief Complaint</th>
                <td mat-cell *matCellDef="let e">{{ e.chiefComplaint || '-' }}</td>
              </ng-container>

              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let e">
                  <button mat-icon-button (click)="viewDetail(e.id)" matTooltip="View details"
                          attr.aria-label="Xem chi tiết hồ sơ lâm sàng">
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
          } @else {
          <p class="placeholder">{{ loading ? 'Loading...' : 'No encounters found.' }}</p>
          }
        </mat-card-content>
      </mat-card>
    </div>
  `,
    styles: [`
    .encounters { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .search-field { width: 100%; max-width: 400px; }
    .table-container { overflow-x: auto; }
    .encounter-table { width: 100%; }
    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background: #f5f5f5; }
    .placeholder { color: #999; text-align: center; padding: 48px; }

  `],
})
export class EncounterListComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  displayedColumns = ['encounterDate', 'patientId', 'encounterType', 'status', 'chiefComplaint', 'actions'];
  encounters: Encounter[] = [];
  totalCount = 0;
  page = 1;
  pageSize = 20;
  loading = false;
  searchControl = new FormControl('');

  constructor(
    private clinicalService: ClinicalService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadEncounters();
    this.searchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntil(this.destroy$),
    ).subscribe(query => {
      this.page = 1;
      query ? this.searchEncounters(query) : this.loadEncounters();
      this.cdr.markForCheck();
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadEncounters(): void {
    this.loading = true;
    this.clinicalService.list(this.page, this.pageSize)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          this.encounters = res.items;
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

  searchEncounters(query: string): void {
    this.loading = true;
    this.clinicalService.search(query, this.page, this.pageSize)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          this.encounters = res.items;
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
    q ? this.searchEncounters(q) : this.loadEncounters();
  }

  viewDetail(id: string): void {
    this.router.navigate(['/clinical', id]);
  }
}
