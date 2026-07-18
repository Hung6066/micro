import { Component, OnInit, OnDestroy, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Store } from '@ngrx/store';
import { Subject, timer } from 'rxjs';
import { takeUntil, tap } from 'rxjs/operators';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { selectError } from '@store/error/error.selectors';
import { clearError, ErrorPayload } from '@store/error/error.actions';

@Component({
  selector: 'app-error-bar',
  standalone: true,
  imports: [CommonModule, MatProgressBarModule, MatButtonModule, MatIconModule, MatSnackBarModule, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error$ | async; as error) {
    <div class="error-bar" [class]="'error-bar--' + getSeverity(error.code)">
      <div class="error-bar__content">
        <mat-icon class="error-bar__icon">{{ getIcon(error.code) }}</mat-icon>
        <div class="error-bar__text">
          <span class="error-bar__message">{{ error.message }}</span>
          @if (error.correlationId) {
          <span class="error-bar__ref">
            Ref: {{ error.correlationId }}
          </span>
          }
        </div>
        <div class="error-bar__actions">
          @if (error.correlationId) {
          <button
            mat-icon-button
            [matTooltip]="'Copy Reference ID'"
            (click)="copyCorrelationId(error.correlationId)"
          >
            <mat-icon>content_copy</mat-icon>
          </button>
          }
          <button mat-icon-button (click)="dismiss()">
            <mat-icon>close</mat-icon>
          </button>
        </div>
      </div>
    </div>
    }
  `,
  styles: [`
    .error-bar {
      padding: 8px 16px;
      font-size: 14px;
      border-bottom: 1px solid;
      transition: opacity 0.3s ease;
    }
    .error-bar__content {
      display: flex;
      align-items: center;
      gap: 12px;
      max-width: 1200px;
      margin: 0 auto;
    }
    .error-bar__icon {
      flex-shrink: 0;
    }
    .error-bar__text {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .error-bar__message {
      font-weight: 500;
    }
    .error-bar__ref {
      font-size: 12px;
      font-family: 'Courier New', monospace;
      opacity: 0.8;
    }
    .error-bar__actions {
      display: flex;
      align-items: center;
      gap: 4px;
      flex-shrink: 0;
    }
    .error-bar--HTTP_5XX,
    .error-bar--HTTP_0 {
      background: #d32f2f;
      color: #fff;
      border-color: #b71c1c;
    }
    .error-bar--HTTP_4XX {
      background: #f57c00;
      color: #fff;
      border-color: #e65100;
    }
    .error-bar--default {
      background: #455a64;
      color: #fff;
      border-color: #37474f;
    }
    .error-bar--UNKNOWN {
      background: #757575;
      color: #fff;
      border-color: #616161;
    }
    .error-bar ::ng-deep .mat-icon {
      color: inherit;
    }
  `],
})
export class ErrorBarComponent implements OnInit, OnDestroy {
  error$ = this.store.select(selectError);

  private destroy$ = new Subject<void>();

  constructor(
    private store: Store,
    private snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    this.error$.pipe(
      takeUntil(this.destroy$),
    ).subscribe((error) => {
      if (error.message) {
        timer(8000).pipe(
          takeUntil(this.destroy$),
          tap(() => this.store.dispatch(clearError())),
        ).subscribe();
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  getSeverity(code: string | null): string {
    if (!code) return 'default';
    if (code === 'HTTP_0' || code.startsWith('HTTP_5')) return 'HTTP_5XX';
    if (code.startsWith('HTTP_4')) return 'HTTP_4XX';
    return 'default';
  }

  getIcon(code: string | null): string {
    if (!code) return 'error_outline';
    if (code === 'HTTP_0') return 'wifi_off';
    if (code.startsWith('HTTP_5')) return 'cloud_off';
    if (code.startsWith('HTTP_4')) return 'warning_amber';
    return 'error_outline';
  }

  copyCorrelationId(correlationId: string): void {
    navigator.clipboard.writeText(correlationId).then(() => {
      this.snackBar.open('Reference ID copied to clipboard', 'OK', {
        duration: 3000,
      });
    });
  }

  dismiss(): void {
    this.store.dispatch(clearError());
  }
}
