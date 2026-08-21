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
        padding: var(--space-md) var(--space-lg);
        border-bottom: 1px solid var(--border-default, #eaeaea);
        flex-wrap: wrap;
        gap: var(--space-sm);
      }
      .stream-status {
        display: flex;
        align-items: center;
        gap: var(--space-sm);
        font-size: var(--font-size-label);
      }
      .status-dot {
        width: var(--size-status-dot);
        height: var(--size-status-dot);
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
        font-size: var(--font-size-caption);
        color: var(--color-primary, #2f6b4a);
        background: #edf3ec;
        padding: 1px var(--space-sm);
        border-radius: var(--radius-input);
        font-weight: 500;
      }
      .stream-actions {
        display: flex;
        gap: var(--space-xs);
      }
      .stream-actions button {
        font-size: var(--font-size-caption);
        line-height: 28px;
      }
      .stream-actions mat-icon {
        font-size: var(--font-size-toolbar);
        width: var(--size-timeline-rail);
        height: var(--size-timeline-rail);
      }
      .stream-list {
        flex: 1;
        overflow-y: auto;
        padding: var(--space-2xs) 0;
        background: #fafaf8;
        font-family: var(--font-mono);
        font-size: var(--font-size-caption);
      }
      .stream-entry {
        padding: var(--space-xs) var(--space-lg);
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
        gap: var(--space-sm);
        margin-bottom: var(--space-hairline);
      }
      .entry-time {
        color: var(--text-muted, #a1a09b);
        font-size: var(--font-size-nav);
        flex-shrink: 0;
      }
      .entry-level {
        display: inline-block;
        padding: 0 var(--space-xs);
        border-radius: 3px;
        font-size: var(--font-size-overline);
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
        font-size: var(--font-size-nav);
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
        margin-top: var(--space-2xs);
      }
      .entry-exception {
        margin: 0;
        font-size: var(--font-size-nav);
        color: #c25450;
        background: #fdebec;
        padding: var(--space-xs) var(--space-inset);
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
        padding: var(--shell-header-height) var(--space-2xl);
        color: var(--text-muted, #a1a09b);
        text-align: center;
        font-family: var(--font-sans);
      }
      .stream-empty mat-icon {
        font-size: var(--font-size-display-lg);
        width: var(--button-height);
        height: var(--button-height);
        margin-bottom: var(--space-md);
        opacity: 0.5;
      }
      .stream-empty p {
        font-size: var(--font-size-body);
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
