import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { Observable } from 'rxjs';
import { TracesService } from '../../core/services/traces.service';
import { TraceSummary } from '../../core/models/trace.model';

@Component({
  selector: 'app-traces-page',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTableModule,
    MatIconModule,
    MatButtonModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Truy vết hệ thống</h1>
      <button mat-stroked-button (click)="refresh()">
        <mat-icon>refresh</mat-icon>
        Làm mới
      </button>
    </div>

    <mat-card>
      <mat-card-content>
        <table mat-table [dataSource]="(traces$ | async) ?? []" class="mat-elevation-z0">
          <ng-container matColumnDef="traceId">
            <th mat-header-cell *matHeaderCellDef>Trace ID</th>
            <td mat-cell *matCellDef="let t" class="mono">{{ t.traceId | slice:0:16 }}...</td>
          </ng-container>

          <ng-container matColumnDef="rootService">
            <th mat-header-cell *matHeaderCellDef>Dịch vụ</th>
            <td mat-cell *matCellDef="let t">{{ t.rootService }}</td>
          </ng-container>

          <ng-container matColumnDef="rootName">
            <th mat-header-cell *matHeaderCellDef>Thao tác</th>
            <td mat-cell *matCellDef="let t">{{ t.rootName }}</td>
          </ng-container>

          <ng-container matColumnDef="duration">
            <th mat-header-cell *matHeaderCellDef>Thời gian</th>
            <td mat-cell *matCellDef="let t">{{ t.durationMs }}ms</td>
          </ng-container>

          <ng-container matColumnDef="spans">
            <th mat-header-cell *matHeaderCellDef>Số spans</th>
            <td mat-cell *matCellDef="let t">{{ t.spanCount }}</td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Trạng thái</th>
            <td mat-cell *matCellDef="let t">
              <span class="status-badge" [class]="'trace-status-' + (t.hasErrors ? 'error' : 'ok')">
                {{ t.hasErrors ? 'Lỗi' : 'OK' }}
              </span>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns"></tr>

          <tr class="mat-row" *matNoDataRow>
            <td class="mat-cell empty-state" [attr.colspan]="displayedColumns.length">
              <mat-icon>timeline</mat-icon>
              <p>Không có truy vết nào</p>
            </td>
          </tr>
        </table>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    table { width: 100%; }
    .mono { font-family: 'Cascadia Mono', 'Consolas', monospace; font-size: 12px; }
    .trace-status-ok { background: #EDF3EC; color: #2F6B4A; }
    .trace-status-error { background: #FDEBEC; color: #C25450; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TracesPageComponent implements OnInit {
  traces$: Observable<TraceSummary[]>;
  readonly displayedColumns = ['traceId', 'rootService', 'rootName', 'duration', 'spans', 'status'];

  constructor(private readonly tracesService: TracesService) {
    this.traces$ = this.tracesService.search({ limit: 50 });
  }

  ngOnInit(): void {}

  refresh(): void {
    this.traces$ = this.tracesService.search({ limit: 50 });
  }
}
