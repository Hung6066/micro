import {
  Component,
  OnInit,
  OnDestroy,
  Input,
  Output,
  EventEmitter,
  ChangeDetectionStrategy,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';
import { Subscription } from 'rxjs';
import { LogStreamService } from '../../core/services/log-stream.service';
import { LogEntry } from '../../core/models/log-entry.model';

@Component({
  selector: 'app-log-stream',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    HisHopeTranslatePipe,
  ],
  template: `
    <div class="stream-controls">
      <mat-slide-toggle
        [checked]="enabled"
        (toggleChange)="toggleStream()"
        color="primary"
      >
        <span class="stream-label">
          {{
            enabled
              ? ('dashboard.logStream.following' | hhTranslate: 'Đang theo dõi')
              : ('dashboard.logStream.realTime'
                | hhTranslate: 'Theo dõi thời gian thực')
          }}
        </span>
      </mat-slide-toggle>
      <span class="stream-indicator" [class.active]="enabled"></span>
      @if (enabled && newCount > 0) {
        <span class="stream-count">
          +{{ newCount }}
          {{ 'dashboard.logStream.newRecords' | hhTranslate: 'bản ghi mới' }}
        </span>
      }
    </div>
  `,
  styles: [
    `
      .stream-controls {
        display: flex;
        align-items: center;
        gap: var(--space-inset);
        padding: var(--space-sm) 0;
      }
      .stream-label {
        font-size: var(--font-size-label);
      }
      .stream-indicator {
        width: var(--size-status-dot);
        height: var(--size-status-dot);
        border-radius: 50%;
        background: var(--text-muted, #a1a09b);
        transition: background 300ms ease;
      }
      .stream-indicator.active {
        background: #2f6b4a;
        box-shadow: 0 0 0 3px rgba(47, 107, 74, 0.2);
      }
      .stream-count {
        font-size: var(--font-size-caption);
        color: var(--color-primary, #2f6b4a);
        background: #edf3ec;
        padding: var(--space-hairline) var(--space-sm);
        border-radius: var(--radius-input);
        font-weight: 500;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogStreamComponent implements OnInit, OnDestroy {
  private readonly logStreamService = inject(LogStreamService);

  @Input() service?: string;
  @Input() level?: string;
  @Output() logReceived = new EventEmitter<LogEntry>();

  enabled = false;
  newCount = 0;

  private subscription?: Subscription;

  ngOnInit(): void {}

  toggleStream(): void {
    this.enabled = !this.enabled;
    if (this.enabled) {
      this.newCount = 0;
      this.logStreamService.connect().then(() => {
        this.logStreamService.subscribe(this.service, this.level);
      });
      this.subscription = this.logStreamService.logs$.subscribe((entry) => {
        this.newCount++;
        this.logReceived.emit(entry);
      });
    } else {
      this.logStreamService.unsubscribe(this.service, this.level);
      this.subscription?.unsubscribe();
      this.subscription = undefined;
    }
  }

  resetCount(): void {
    this.newCount = 0;
  }

  ngOnDestroy(): void {
    if (this.enabled) {
      this.logStreamService.unsubscribe(this.service, this.level);
    }
    this.subscription?.unsubscribe();
  }
}
