import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { Observable } from 'rxjs';
import { ResourceService } from '../../core/services/resource.service';
import { Resource } from '../../core/models/resource.model';

@Component({
  selector: 'app-resources-page',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTableModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Tài nguyên hệ thống</h1>
    </div>

    <mat-card>
      <mat-card-content>
        <table mat-table [dataSource]="(resources$ | async) ?? []" class="mat-elevation-z0">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Tên</th>
            <td mat-cell *matCellDef="let r">{{ r.displayName || r.name }}</td>
          </ng-container>

          <ng-container matColumnDef="type">
            <th mat-header-cell *matHeaderCellDef>Loại</th>
            <td mat-cell *matCellDef="let r">{{ r.type }}</td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Trạng thái</th>
            <td mat-cell *matCellDef="let r">
              <span class="status-badge" [class]="'status-' + r.status">
                {{ r.status }}
              </span>
            </td>
          </ng-container>

          <ng-container matColumnDef="health">
            <th mat-header-cell *matHeaderCellDef>Sức khỏe</th>
            <td mat-cell *matCellDef="let r">
              <span class="status-badge" [class]="'status-' + r.healthStatus">
                {{ r.healthStatus }}
              </span>
            </td>
          </ng-container>

          <ng-container matColumnDef="version">
            <th mat-header-cell *matHeaderCellDef>Phiên bản</th>
            <td mat-cell *matCellDef="let r">{{ r.version || '-' }}</td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns"></tr>

          <tr class="mat-row" *matNoDataRow>
            <td class="mat-cell empty-state" [attr.colspan]="displayedColumns.length">
              <mat-icon>inventory_2</mat-icon>
              <p>Không có tài nguyên nào</p>
            </td>
          </tr>
        </table>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    table { width: 100%; }
    .status-badge { text-transform: uppercase; font-size: 11px; padding: 2px 8px; border-radius: 4px; }
    .status-running, .status-healthy { background: #EDF3EC; color: #2F6B4A; }
    .status-stopped, .status-unhealthy { background: #FDEBEC; color: #C25450; }
    .status-degraded { background: #FDF0E2; color: #B6581C; }
    .status-unknown { background: #F3EDF8; color: #6B4FA0; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourcesPageComponent implements OnInit {
  readonly resources$: Observable<Resource[]>;
  readonly displayedColumns = ['name', 'type', 'status', 'health', 'version'];

  constructor(private readonly resourceService: ResourceService) {
    this.resources$ = this.resourceService.getAll();
  }

  ngOnInit(): void {}
}
