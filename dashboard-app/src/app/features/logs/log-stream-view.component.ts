import {
  Component,
  OnInit,
  OnDestroy,
  Input,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  ViewChild,
  ElementRef,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Subscription } from 'rxjs';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';
import { LogStreamService } from '../../core/services/log-stream.service';
import { LogEntry } from '../../core/models/log-entry.model';

@Component({
  selector: 'app-log-stream-view',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, HisHopeTranslatePipe],
  template: `
    <div class="stream-container">
      <!-- Controls bar -->
      <div class="stream-controls">
        <div class="stream-status">
          <span class="status-dot" [class.active]="connected"></span>
          <span class="status-text">{{
            connected
              ? ('dashboard.logStream.streaming' | hhTranslate: 'Streaming')
              : ('dashboard.logStream.disconnected'
                | hhTranslate: 'Disconnected')
          }}</span>
          @if (streamLogs.length > 0) {
            <span class="log-count">
              {{ streamLogs.length }}
              {{ 'dashboard.logStream.entries' | hhTranslate: 'entries' }}
            </span>
          }
        </div>
        <div class="stream-actions">
          <button
            mat-stroked-button
            size="small"
            (click)="toggleStream()"
            [disabled]="connecting"
          >
            <mat-icon>{{ connected ? 'pause' : 'play_arrow' }}</mat-icon>
            {{
              connected
                ? ('dashboard.logStream.pause' | hhTranslate: 'Pause')
                : ('dashboard.logStream.start' | hhTranslate: 'Start')
            }}
          </button>
          <button
            mat-stroked-button
            size="small"
            (click)="clearLogs()"
            [disabled]="streamLogs.length === 0"
          >
            <mat-icon>clear_all</mat-icon>
            {{ 'dashboard.logStream.clear' | hhTranslate: 'Clear' }}
          </button>
          <button
            mat-stroked-button
            size="small"
            (click)="scrollToBottom()"
            [disabled]="autoScroll"
          >
            <mat-icon>vertical_align_bottom</mat-icon>
            {{
              autoScroll
                ? ('dashboard.logStream.autoScrollOn'
                  | hhTranslate: 'Auto-scroll ON')
                : ('dashboard.logStream.scrollToBottom'
                  | hhTranslate: 'Scroll to bottom')
            }}
          </button>
        </div>
      </div>

      <!-- Stream list -->
      <div class="stream-list" #streamList>
        @for (entry of streamLogs; track entry.id) {
          <div
            class="stream-entry"
            [class.level-error]="
              entry.level === 'Error' || entry.level === 'Critical'
            "
            [class.level-warning]="entry.level === 'Warning'"
            [class.level-debug]="entry.level === 'Debug'"
          >
            <div class="entry-header">
              <span class="entry-time">{{
                entry.timestamp | date: 'HH:mm:ss.SSS'
              }}</span>
              <span
                class="entry-level"
                [class]="'level-' + entry.level.toLowerCase()"
              >
                {{ entry.level }}
              </span>
              <span class="entry-service">{{ entry.service }}</span>
            </div>
            <div class="entry-message">{{ entry.message }}</div>
            @if (entry.exception) {
              <div class="entry-detail">
                <pre class="entry-exception">{{ entry.exception }}</pre>
              </div>
            }
          </div>
        }

        @if (streamLogs.length === 0 && !connected) {
          <div class="stream-empty">
            <mat-icon>radio_button_unchecked</mat-icon>
            <p>
              {{
                'dashboard.logStream.clickStartToBegin'
                  | hhTranslate
                    : 'Click Start to begin streaming logs in real time.'
              }}
            </p>
          </div>
        }
      </div>
    </div>
  `,
  styles: [
    `
      .stream-container {
        display: flex;
        flex-direction: column;
        height: 100%;
        min-height: 400px;
      }
      .stream-controls {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 12px 16px;
        border-bottom: 1px solid var(--border-default, #eaeaea);
        flex-wrap: wrap;
        gap: 8px;
      }
      .stream-status {
        display: flex;
        align-items: center;
        gap: 8px;
        font-size: 13px;
      }
      .status-dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background: var(--text-muted, #a1a09b);
        transition: background 300ms ease;
      }
      .status-dot.active {
        background: #2f6b4a;
        box-shadow: 0 0 0 3px rgba(47, 107, 74, 0.2);
      }
      .status-text {
        color: var(--text-secondary, #787774);
        font-weight: 500;
      }
      .log-count {
        font-size: 12px;
        color: var(--color-primary, #2f6b4a);
        background: #edf3ec;
        padding: 1px 8px;
        border-radius: 4px;
        font-weight: 500;
      }
      .stream-actions {
        display: flex;
        gap: 6px;
      }
      .stream-actions button {
        font-size: 12px;
        line-height: 28px;
      }
      .stream-actions mat-icon {
        font-size: 16px;
        width: 16px;
        height: 16px;
      }
      .stream-list {
        flex: 1;
        overflow-y: auto;
        padding: 4px 0;
        background: #fafaf8;
        font-family: var(--font-mono, 'Cascadia Mono', Consolas, monospace);
        font-size: 12px;
      }
      .stream-entry {
        padding: 6px 16px;
        border-bottom: 1px solid #f0f0ee;
        transition: background 150ms ease;
        animation: fadeIn 300ms ease;
      }
      .stream-entry:hover {
        background: rgba(0, 0, 0, 0.015);
      }
      .stream-entry.level-error {
        background: #fdebec;
      }
      .stream-entry.level-warning {
        background: #fdf0e2;
      }
      .stream-entry.level-debug {
        color: var(--text-muted, #a1a09b);
      }
      .entry-header {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 2px;
      }
      .entry-time {
        color: var(--text-muted, #a1a09b);
        font-size: 11px;
        flex-shrink: 0;
      }
      .entry-level {
        display: inline-block;
        padding: 0 6px;
        border-radius: 3px;
        font-size: 10px;
        font-weight: 600;
        letter-spacing: 0.03em;
        text-transform: uppercase;
        flex-shrink: 0;
      }
      .entry-level.level-error,
      .entry-level.level-critical {
        background: #fdebec;
        color: #c25450;
      }
      .entry-level.level-warning {
        background: #fdf0e2;
        color: #b6581c;
      }
      .entry-level.level-information {
        background: #e1f3fe;
        color: #2563eb;
      }
      .entry-level.level-debug {
        background: #f3edf8;
        color: #6b4fa0;
      }
      .entry-service {
        font-size: 11px;
        font-weight: 500;
        color: var(--text-secondary, #787774);
        flex-shrink: 0;
      }
      .entry-message {
        color: var(--text-primary, #1a1a1a);
        line-height: 1.5;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .entry-detail {
        margin-top: 4px;
      }
      .entry-exception {
        margin: 0;
        font-size: 11px;
        color: #c25450;
        background: #fdebec;
        padding: 6px 10px;
        border-radius: 3px;
        white-space: pre-wrap;
        line-height: 1.4;
        max-height: 120px;
        overflow-y: auto;
      }
      .stream-empty {
        display: flex;
        flex-direction: column;
        align-items: center;
        padding: 64px 24px;
        color: var(--text-muted, #a1a09b);
        text-align: center;
        font-family: var(--font-sans);
      }
      .stream-empty mat-icon {
        font-size: 40px;
        width: 40px;
        height: 40px;
        margin-bottom: 12px;
        opacity: 0.5;
      }
      .stream-empty p {
        font-size: 14px;
        line-height: 1.6;
        max-width: 280px;
      }
      @keyframes fadeIn {
        from {
          opacity: 0;
          transform: translateY(-4px);
        }
        to {
          opacity: 1;
          transform: translateY(0);
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogStreamViewComponent implements OnInit, OnDestroy {
  private readonly logStreamService = inject(LogStreamService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly elementRef = inject(ElementRef);

  @Input() service = '';
  @Input() level = '';

  @ViewChild('streamList', { static: false })
  streamListEl?: ElementRef<HTMLElement>;

  connected = false;
  connecting = false;
  autoScroll = true;
  streamLogs: LogEntry[] = [];

  private subscription?: Subscription;

  ngOnInit(): void {}

  ngOnDestroy(): void {
    this.disconnect();
  }

  toggleStream(): void {
    if (this.connected) {
      this.disconnect();
    } else {
      this.connect();
    }
  }

  private connect(): void {
    this.connecting = true;
    this.logStreamService.connect().then(() => {
      this.logStreamService.subscribe(
        this.service || undefined,
        this.level || undefined,
      );
      this.connected = true;
      this.connecting = false;
      this.subscription = this.logStreamService.logs$.subscribe((entry) => {
        this.streamLogs = [...this.streamLogs, entry];
        this.cdr.markForCheck();
        if (this.autoScroll) {
          setTimeout(() => this.scrollToBottom(), 0);
        }
      });
      this.cdr.markForCheck();
    });
  }

  private disconnect(): void {
    if (this.connected) {
      this.logStreamService.unsubscribe(
        this.service || undefined,
        this.level || undefined,
      );
    }
    this.subscription?.unsubscribe();
    this.subscription = undefined;
    this.connected = false;
    this.connecting = false;
    this.cdr.markForCheck();
  }

  clearLogs(): void {
    this.streamLogs = [];
    this.cdr.markForCheck();
  }

  scrollToBottom(): void {
    this.autoScroll = true;
    setTimeout(() => {
      if (this.streamListEl?.nativeElement) {
        this.streamListEl.nativeElement.scrollTop =
          this.streamListEl.nativeElement.scrollHeight;
      }
    }, 0);
  }
}
