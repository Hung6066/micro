import { Component, OnInit, OnDestroy, Input, Output, EventEmitter, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
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
  ],
  template: `
    <div class="stream-controls">
      <mat-slide-toggle
        [checked]="enabled"
        (toggleChange)="toggleStream()"
        color="primary">
        <span class="stream-label">
          {{ enabled ? 'Đang theo dõi' : 'Theo dõi thời gian thực' }}
        </span>
      </mat-slide-toggle>
      <span class="stream-indicator" [class.active]="enabled"></span>
      <span class="stream-count" *ngIf="enabled && newCount > 0">
        +{{ newCount }} bản ghi mới
      </span>
    </div>
  `,
  styles: [`
    .stream-controls {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 8px 0;
    }
    .stream-label {
      font-size: 13px;
    }
    .stream-indicator {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--text-muted, #A1A09B);
      transition: background 300ms ease;
    }
    .stream-indicator.active {
      background: #2F6B4A;
      box-shadow: 0 0 0 3px rgba(47, 107, 74, 0.2);
    }
    .stream-count {
      font-size: 12px;
      color: var(--color-primary, #2F6B4A);
      background: #EDF3EC;
      padding: 2px 8px;
      border-radius: 4px;
      font-weight: 500;
    }
  `],
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
      this.subscription = this.logStreamService.logs$.subscribe(entry => {
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
