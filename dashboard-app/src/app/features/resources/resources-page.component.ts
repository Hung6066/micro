import { Component, OnInit, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { BehaviorSubject, Observable, combineLatest, of } from 'rxjs';
import { catchError, finalize, map, shareReplay, switchMap, tap } from 'rxjs/operators';
import { ResourceService } from '../../core/services/resource.service';
import { LifecycleService } from '../../core/services/lifecycle.service';
import { Resource } from '../../core/models/resource.model';
import { ResourceCardComponent } from '../../shared/resource-card/resource-card.component';
import { ResourceDetailComponent } from './resource-detail.component';

interface GroupedResources {
  services: Resource[];
  databases: Resource[];
  infrastructure: Resource[];
  total: number;
}

@Component({
  selector: 'app-resources-page',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    ResourceCardComponent,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">System Resources</h1>
      <button mat-stroked-button (click)="refresh()" [disabled]="(loading$ | async) ?? false">
        <mat-icon>refresh</mat-icon>
        Refresh
      </button>
    </div>

    <!-- Loading spinner -->
    <div class="loading-state" *ngIf="(loading$ | async) && !(error$ | async)">
      <mat-spinner diameter="32"></mat-spinner>
      <span style="margin-top: 12px;">Loading resources...</span>
    </div>

    <!-- Error state -->
    <div class="error-state" *ngIf="error$ | async as err">
      <mat-icon class="error-icon">error_outline</mat-icon>
      <p class="error-message">{{ err }}</p>
      <button mat-raised-button color="primary" (click)="refresh()">
        <mat-icon>refresh</mat-icon>
        Retry
      </button>
    </div>

    <!-- Resource groups -->
    <ng-container *ngIf="grouped$ | async as grouped">
      <!-- Loading overlay for existing content -->
      <div class="loading-overlay" *ngIf="(loading$ | async) && !(error$ | async) && grouped.total > 0">
        <mat-spinner diameter="20"></mat-spinner>
        <span>Refreshing...</span>
      </div>

      <!-- Services -->
      <section class="resource-group" *ngIf="grouped.services.length > 0">
        <div class="group-header">
          <mat-icon>dns</mat-icon>
          <h2 class="group-title">Services</h2>
          <span class="group-count">{{ grouped.services.length }}</span>
        </div>
        <div class="card-grid">
          <app-resource-card
            *ngFor="let r of grouped.services"
            [resource]="r"
            (cardClick)="openDetail($event)"
            (start)="onStart($event)"
            (stop)="onStop($event)"
            (restart)="onRestart($event)">
          </app-resource-card>
        </div>
      </section>

      <!-- Databases -->
      <section class="resource-group" *ngIf="grouped.databases.length > 0">
        <div class="group-header">
          <mat-icon>storage</mat-icon>
          <h2 class="group-title">Databases</h2>
          <span class="group-count">{{ grouped.databases.length }}</span>
        </div>
        <div class="card-grid">
          <app-resource-card
            *ngFor="let r of grouped.databases"
            [resource]="r"
            (cardClick)="openDetail($event)"
            (start)="onStart($event)"
            (stop)="onStop($event)"
            (restart)="onRestart($event)">
          </app-resource-card>
        </div>
      </section>

      <!-- Infrastructure -->
      <section class="resource-group" *ngIf="grouped.infrastructure.length > 0">
        <div class="group-header">
          <mat-icon>cloud</mat-icon>
          <h2 class="group-title">Infrastructure</h2>
          <span class="group-count">{{ grouped.infrastructure.length }}</span>
        </div>
        <div class="card-grid">
          <app-resource-card
            *ngFor="let r of grouped.infrastructure"
            [resource]="r"
            (cardClick)="openDetail($event)"
            (start)="onStart($event)"
            (stop)="onStop($event)"
            (restart)="onRestart($event)">
          </app-resource-card>
        </div>
      </section>

      <!-- Empty state when loaded with no data -->
      <div class="empty-state" *ngIf="grouped.total === 0">
        <mat-icon>inventory_2</mat-icon>
        <p>No resources found</p>
      </div>
    </ng-container>
  `,
  styles: [`
    .loading-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 64px 24px;
      color: var(--text-secondary, #787774);
    }
    .error-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 48px 24px;
      text-align: center;
      background: #FDEBEC;
      border: 1px solid #F5C6C4;
      border-radius: 8px;
      gap: 12px;
    }
    .loading-overlay {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      padding: 8px;
      font-size: 13px;
      color: var(--text-secondary, #787774);
    }
    .error-icon {
      font-size: 40px;
      width: 40px;
      height: 40px;
      color: #C25450;
    }
    .error-message {
      font-size: 14px;
      color: #C25450;
      max-width: 400px;
    }
    .resource-group {
      margin-bottom: 32px;
    }
    .group-header {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 16px;
    }
    .group-header mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
      color: var(--text-secondary, #787774);
    }
    .group-title {
      font-size: 16px;
      font-weight: 600;
      color: var(--text-primary, #1A1A1A);
      margin: 0;
    }
    .group-count {
      font-size: 12px;
      color: var(--text-muted, #A1A09B);
      background: var(--bg-warm, #F7F6F3);
      padding: 0 8px;
      border-radius: 10px;
      line-height: 20px;
    }
    .card-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 16px;
    }
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 48px 24px;
      color: var(--text-muted, #A1A09B);
      text-align: center;
    }
    .empty-state mat-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      margin-bottom: 16px;
      opacity: 0.4;
    }
    .empty-state p {
      font-size: 14px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourcesPageComponent implements OnInit {
  private readonly resourceService = inject(ResourceService);
  private readonly lifecycleService = inject(LifecycleService);
  private readonly dialog = inject(MatDialog);

  private readonly refreshTrigger = new BehaviorSubject<void>(undefined);

  readonly loading$ = new BehaviorSubject<boolean>(true);
  readonly error$ = new BehaviorSubject<string | null>(null);

  readonly resources$: Observable<Resource[]> = this.refreshTrigger.pipe(
    tap(() => {
      this.loading$.next(true);
      this.error$.next(null);
    }),
    switchMap(() =>
      this.resourceService.getAll().pipe(
        catchError(err => {
          const msg = err?.message ?? err?.statusText ?? 'Failed to load resources. Please try again.';
          this.error$.next(msg);
          this.loading$.next(false);
          return of([] as Resource[]);
        }),
        finalize(() => {
          this.loading$.next(false);
        }),
      ),
    ),
    shareReplay(1),
  );

  readonly grouped$: Observable<GroupedResources> = this.resources$.pipe(
    map(resources => {
      const grouped: GroupedResources = { services: [], databases: [], infrastructure: [], total: 0 };
      for (const r of resources) {
        const type = (r.type ?? '').toLowerCase();
        if (type === 'service' || type === 'services') {
          grouped.services.push(r);
        } else if (type === 'database' || type === 'databases') {
          grouped.databases.push(r);
        } else {
          grouped.infrastructure.push(r);
        }
      }
      grouped.total = grouped.services.length + grouped.databases.length + grouped.infrastructure.length;
      return grouped;
    }),
  );

  ngOnInit(): void {}

  refresh(): void {
    this.refreshTrigger.next();
  }

  openDetail(resource: Resource): void {
    this.dialog.open(ResourceDetailComponent, {
      data: { resource },
      width: '600px',
      maxWidth: '90vw',
      panelClass: 'resource-detail-panel',
    });
  }

  onStart(resource: Resource): void {
    this.lifecycleService.start(resource.name).subscribe({
      next: () => this.refresh(),
      error: () => console.error(`Failed to start ${resource.name}`),
    });
  }

  onStop(resource: Resource): void {
    this.lifecycleService.stop(resource.name).subscribe({
      next: () => this.refresh(),
      error: () => console.error(`Failed to stop ${resource.name}`),
    });
  }

  onRestart(resource: Resource): void {
    this.lifecycleService.restart(resource.name).subscribe({
      next: () => this.refresh(),
      error: () => console.error(`Failed to restart ${resource.name}`),
    });
  }
}
