import {
  Component,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  inject,
  ChangeDetectorRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { interval, Subscription } from 'rxjs';
import { startWith, switchMap } from 'rxjs/operators';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';
import { ResourceService } from '../../core/services/resource.service';
import { Resource } from '../../core/models/resource.model';

// ── Constants ──
const POLL_INTERVAL_MS = 30_000;
const HISTORY_WINDOW_MS = 24 * 60 * 60 * 1000;
const SVG_VIEWBOX_W = 960;
const LABEL_W = 140;
const CHART_PAD = 16;
const ROW_H = 24;
const ROW_GAP = 6;
const TOP_PAD = 8;
const BOTTOM_PAD = 36;
const BAR_X0 = LABEL_W + CHART_PAD;
const BAR_X1 = SVG_VIEWBOX_W - CHART_PAD;
const BAR_W = BAR_X1 - BAR_X0;

// ── Types ──
interface HealthTransition {
  timestamp: number;
  status: string;
}

interface ServiceHealthEntry {
  name: string;
  displayName: string;
  transitions: HealthTransition[];
}

interface TimelineSegment {
  status: string;
  x: number;
  w: number;
  startMs: number;
  endMs: number;
  serviceName: string;
  displayName: string;
}

interface TickMark {
  x: number;
  label: string;
}

// ── Helpers ──
const STATUS_COLORS: Record<string, string> = {
  Healthy: '#2F6B4A',
  Unhealthy: '#C25450',
  Degraded: '#B6581C',
};

function statusColor(s: string): string {
  return STATUS_COLORS[s] ?? '#A1A09B';
}

function fmtDuration(ms: number): string {
  if (ms < 60_000) return '<1m';
  const h = Math.floor(ms / 3_600_000);
  const m = Math.floor((ms % 3_600_000) / 60_000);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

function fmtTime(ts: number): string {
  const d = new Date(ts);
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
}

@Component({
  selector: 'app-health-timeline',
  standalone: true,
  imports: [CommonModule, MatChipsModule, MatIconModule, HisHopeTranslatePipe],
  template: `
    <section class="ht-section">
      <!-- Header -->
      <div class="ht-header">
        <h3 class="ht-title">
          {{
            'dashboard.resources.healthTimeline'
              | hhTranslate: 'Health Timeline (24h)'
          }}
        </h3>
        @if (incidentCount > 0) {
          <mat-chip
            class="incident-chip"
            color="warn"
            highlighted
            disableRipple
          >
            {{
              'dashboard.resources.incidents'
                | hhTranslate: '{count} incident(s)' : { count: incidentCount }
            }}
          </mat-chip>
        }
        @if (incidentCount === 0 && serviceOrder.length > 0) {
          <span class="ht-subtitle">{{
            'dashboard.resources.allHealthy' | hhTranslate: 'All healthy'
          }}</span>
        }
      </div>

      <!-- Chart -->
      <div class="ht-chart-wrap" #chartWrap>
        <!-- Empty state -->
        @if (serviceOrder.length === 0) {
          <div class="ht-empty">
            <mat-icon>timeline</mat-icon>
            <p>
              {{
                'dashboard.resources.waitingHealthData'
                  | hhTranslate: 'Waiting for health data…'
              }}
            </p>
          </div>
        }

        <!-- SVG -->
        @if (serviceOrder.length > 0) {
          <svg
            class="ht-svg"
            [attr.viewBox]="'0 0 ' + SVG_VIEWBOX_W + ' ' + svgHeight"
            preserveAspectRatio="xMidYMid meet"
            (mouseleave)="hideTooltip()"
          >
            <!-- Grid lines -->
            @for (t of ticks; track $index) {
              <line
                [attr.x1]="t.x"
                [attr.x2]="t.x"
                y1="0"
                [attr.y2]="svgHeight - BOTTOM_PAD"
                class="ht-gridline"
              />
            }

            <!-- Per-service rows -->
            @for (name of serviceOrder; track name; let i = $index) {
              <g [attr.transform]="'translate(0,' + rowY(i) + ')'">
                <!-- Label -->
                <text
                  [attr.x]="LABEL_W - 8"
                  [attr.y]="ROW_H / 2 + 5"
                  text-anchor="end"
                  class="ht-label"
                >
                  {{ displayNameOf(name) }}
                </text>

                <!-- Bar background -->
                <rect
                  [attr.x]="BAR_X0"
                  y="2"
                  [attr.width]="BAR_W"
                  [attr.height]="ROW_H - 4"
                  class="ht-bar-bg"
                />

                <!-- Segments -->
                @for (seg of segmentsFor(name); track $index) {
                  @if (seg.w > 0) {
                    <rect
                      [attr.x]="seg.x"
                      y="2"
                      [attr.width]="seg.w"
                      [attr.height]="ROW_H - 4"
                      [attr.fill]="getStatusColor(seg.status)"
                      rx="3"
                      class="ht-seg"
                      (mouseenter)="showTooltip($event, seg)"
                      (mouseleave)="hideTooltip()"
                      (click)="onSegClick(seg)"
                    />
                  }
                }
              </g>
            }

            <!-- Time axis -->
            <g
              [attr.transform]="
                'translate(0,' + (svgHeight - BOTTOM_PAD + 8) + ')'
              "
            >
              @for (t of ticks; track $index) {
                <text
                  [attr.x]="t.x"
                  y="12"
                  text-anchor="middle"
                  class="ht-axis"
                >
                  {{ t.label }}
                </text>
              }
            </g>
          </svg>
        }

        <!-- Tooltip -->
        @if (tooltip.visible) {
          <div
            class="ht-tooltip"
            [style.left.px]="tooltip.x"
            [style.top.px]="tooltip.y"
          >
            <div class="ht-tooltip-name">{{ tooltip.displayName }}</div>
            <div class="ht-tooltip-status" [style.color]="tooltip.color">
              {{ tooltip.status }}
            </div>
            <div class="ht-tooltip-time">{{ tooltip.timeRange }}</div>
          </div>
        }
      </div>

      <!-- Legend -->
      @if (serviceOrder.length > 0) {
        <div class="ht-legend">
          <span class="ht-legend-item"
            ><span class="ht-dot" style="background:#2F6B4A"></span
            >{{ 'dashboard.resources.healthy' | hhTranslate: 'Healthy' }}</span
          >
          <span class="ht-legend-item"
            ><span class="ht-dot" style="background:#B6581C"></span
            >{{
              'dashboard.resources.degraded' | hhTranslate: 'Degraded'
            }}</span
          >
          <span class="ht-legend-item"
            ><span class="ht-dot" style="background:#C25450"></span
            >{{ 'dashboard.resources.down' | hhTranslate: 'Down' }}</span
          >
          <span class="ht-legend-item"
            ><span class="ht-dot" style="background:#A1A09B"></span
            >{{ 'dashboard.resources.unknown' | hhTranslate: 'Unknown' }}</span
          >
        </div>
      }

      <!-- Incident detail -->
      @if (selectedIncident) {
        <div class="ht-incident">
          <div class="ht-incident-header">
            <mat-icon>warning</mat-icon>
            <span class="ht-incident-title"
              >{{
                'dashboard.resources.incidentLabel' | hhTranslate: 'Incident'
              }}: {{ selectedIncident.displayName }}</span
            >
            <button class="ht-incident-close" (click)="selectedIncident = null">
              &times;
            </button>
          </div>
          <div class="ht-incident-body">
            <div class="ht-incident-row">
              <span class="ht-incident-label">{{
                'dashboard.resources.started' | hhTranslate: 'Started'
              }}</span>
              <span>{{ selectedIncident.startedAt }}</span>
            </div>
            <div class="ht-incident-row">
              <span class="ht-incident-label">{{
                'dashboard.resources.duration' | hhTranslate: 'Duration'
              }}</span>
              <span>{{ selectedIncident.duration }}</span>
            </div>
          </div>
        </div>
      }
    </section>
  `,
  styles: [
    `
      .ht-section {
        margin-top: var(--space-3xl);
      }
      .ht-header {
        display: flex;
        align-items: center;
        gap: var(--space-md);
        margin-bottom: var(--space-lg);
      }
      .ht-title {
        font-size: var(--font-size-toolbar);
        font-weight: 600;
        color: var(--text-primary, #1a1a1a);
        margin: 0;
      }
      .ht-subtitle {
        font-size: var(--font-size-label);
        color: var(--text-secondary, #787774);
      }
      .incident-chip {
        --mdc-chip-elevated-container-color: #fdebec;
        --mdc-chip-label-text-color: #c25450;
        font-size: var(--font-size-caption);
        font-weight: 500;
      }
      .ht-chart-wrap {
        position: relative;
        background: var(--surface-white, #ffffff);
        border: 1px solid var(--border-default, #eaeaea);
        border-radius: var(--radius-card);
        overflow: hidden;
      }
      .ht-svg {
        display: block;
        width: 100%;
        height: auto;
      }
      .ht-empty {
        display: flex;
        flex-direction: column;
        align-items: center;
        padding: var(--space-4xl) var(--space-2xl);
        color: var(--text-muted, #a1a09b);
        text-align: center;
        gap: var(--space-sm);
      }
      .ht-empty mat-icon {
        font-size: var(--font-size-display-lg);
        width: var(--button-height);
        height: var(--button-height);
        opacity: 0.4;
      }
      .ht-empty p {
        font-size: var(--font-size-body);
      }

      /* Grid lines */
      .ht-gridline {
        stroke: var(--border-light, #f0f0ee);
        stroke-width: 1;
      }

      /* Bar background */
      .ht-bar-bg {
        fill: #f7f6f3;
        rx: 3;
      }

      /* Segment */
      .ht-seg {
        cursor: pointer;
        transition: opacity 150ms ease;
      }
      .ht-seg:hover {
        opacity: 0.8;
      }
      .ht-seg:active {
        opacity: 0.6;
      }

      /* Labels */
      .ht-label {
        font-size: var(--font-size-caption);
        font-weight: 500;
        fill: var(--text-primary, #1a1a1a);
        font-family: var(--font-sans);
      }
      .ht-axis {
        font-size: var(--font-size-overline);
        fill: var(--text-muted, #a1a09b);
        font-family: var(--font-sans);
      }

      /* Tooltip */
      .ht-tooltip {
        position: absolute;
        background: #1a1a1a;
        color: #ffffff;
        font-size: var(--font-size-caption);
        border-radius: var(--radius-button);
        padding: var(--space-sm) var(--space-md);
        pointer-events: none;
        z-index: 100;
        line-height: 1.5;
        white-space: nowrap;
        transform: translate(-50%, -120%);
        box-shadow: var(--shadow-dropdown);
      }
      .ht-tooltip-name {
        font-weight: 600;
      }
      .ht-tooltip-status {
        font-weight: 500;
      }
      .ht-tooltip-time {
        color: #a1a09b;
        font-size: var(--font-size-nav);
      }

      /* Legend */
      .ht-legend {
        display: flex;
        flex-wrap: wrap;
        gap: var(--space-lg);
        padding: var(--space-md) 0 0;
        font-size: var(--font-size-caption);
        color: var(--text-secondary, #787774);
      }
      .ht-legend-item {
        display: flex;
        align-items: center;
        gap: var(--space-xs);
      }
      .ht-dot {
        width: var(--size-status-dot);
        height: var(--size-status-dot);
        border-radius: 50%;
        flex-shrink: 0;
      }

      /* Incident detail */
      .ht-incident {
        margin-top: var(--space-md);
        background: #fdebec;
        border: 1px solid #f5c6c4;
        border-radius: var(--radius-card);
        overflow: hidden;
        font-size: var(--font-size-label);
      }
      .ht-incident-header {
        display: flex;
        align-items: center;
        gap: var(--space-sm);
        padding: var(--space-inset) var(--space-lg);
        background: rgba(194, 84, 80, 0.08);
        font-weight: 600;
        color: #c25450;
      }
      .ht-incident-header mat-icon {
        font-size: var(--font-size-icon-sm);
        width: 18px;
        height: 18px;
      }
      .ht-incident-title {
        flex: 1;
      }
      .ht-incident-close {
        background: none;
        border: none;
        font-size: var(--font-size-section);
        cursor: pointer;
        color: #c25450;
        line-height: 1;
        padding: 0 var(--space-2xs);
      }
      .ht-incident-body {
        padding: var(--space-md) var(--space-lg);
        display: flex;
        flex-direction: column;
        gap: var(--space-xs);
      }
      .ht-incident-row {
        display: flex;
        gap: var(--space-md);
      }
      .ht-incident-label {
        color: var(--text-muted, #a1a09b);
        min-width: 72px;
        font-weight: 500;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HealthTimelineComponent implements OnInit, OnDestroy {
  // ── Injected ──
  private readonly resourceService = inject(ResourceService);
  private readonly cdr = inject(ChangeDetectorRef);

  // ── Constants exposed to template ──
  readonly SVG_VIEWBOX_W = SVG_VIEWBOX_W;
  readonly LABEL_W = LABEL_W;
  readonly BAR_X0 = BAR_X0;
  readonly BAR_W = BAR_W;
  readonly ROW_H = ROW_H;
  readonly BOTTOM_PAD = BOTTOM_PAD;

  // ── State ──
  private history = new Map<string, ServiceHealthEntry>();
  private pollSub?: Subscription;
  private lastPollMs = 0;

  serviceOrder: string[] = [];
  segments: TimelineSegment[] = [];
  incidentCount = 0;
  svgHeight = 120;

  tooltip = {
    visible: false,
    x: 0,
    y: 0,
    displayName: '',
    status: '',
    color: '',
    timeRange: '',
  };

  selectedIncident: {
    displayName: string;
    startedAt: string;
    duration: string;
  } | null = null;

  // ── Ticks ──
  get ticks(): TickMark[] {
    const labels = ['24h ago', '18h ago', '12h ago', '6h ago', 'Now'];
    const positions = [0, 0.25, 0.5, 0.75, 1];
    return positions.map((p, i) => ({
      x: BAR_X0 + p * BAR_W,
      label: labels[i],
    }));
  }

  // ── Lifecycle ──
  ngOnInit(): void {
    this.pollSub = interval(POLL_INTERVAL_MS)
      .pipe(
        startWith(0),
        switchMap(() => this.resourceService.getAll()),
      )
      .subscribe({
        next: (resources) => this.processPoll(resources),
        error: () => {
          /* poll errors silently — keep last known state */
        },
      });
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  // ── Template helpers ──
  rowY(index: number): number {
    return TOP_PAD + index * (ROW_H + ROW_GAP);
  }

  displayNameOf(name: string): string {
    return this.history.get(name)?.displayName ?? name;
  }

  segmentsFor(name: string): TimelineSegment[] {
    return this.segments.filter((s) => s.serviceName === name);
  }

  getStatusColor(s: string): string {
    return statusColor(s);
  }

  // ── Poll processing ──
  private processPoll(resources: Resource[]): void {
    const now = Date.now();
    this.lastPollMs = now;
    const cutoff = now - HISTORY_WINDOW_MS;
    const seen = new Set<string>();

    // Merge current snapshot into history
    for (const r of resources) {
      seen.add(r.name);
      const newStatus = r.healthStatus || 'Unknown';
      let entry = this.history.get(r.name);
      if (!entry) {
        // Brand-new service: seed a transition at cutoff
        entry = {
          name: r.name,
          displayName: r.displayName || r.name,
          transitions: [{ timestamp: cutoff, status: newStatus }],
        };
        this.history.set(r.name, entry);
      } else {
        entry.displayName = r.displayName || r.name;
        const last = entry.transitions[entry.transitions.length - 1];
        if (last.status !== newStatus) {
          // Status changed — record transition at current time
          entry.transitions.push({ timestamp: now, status: newStatus });
        }
      }
    }

    // Clean up history: remove stale entries, trim old transitions
    for (const [name, entry] of this.history) {
      if (!seen.has(name)) {
        this.history.delete(name);
        continue;
      }
      // Prune transitions older than cutoff, but keep at least the first
      while (
        entry.transitions.length > 2 &&
        entry.transitions[1].timestamp < cutoff
      ) {
        entry.transitions.shift();
      }
      // Clamp first transition to cutoff
      if (
        entry.transitions.length > 0 &&
        entry.transitions[0].timestamp < cutoff
      ) {
        entry.transitions[0].timestamp = cutoff;
      }
    }

    this.serviceOrder = Array.from(this.history.keys());

    // Rebuild segments
    const segs: TimelineSegment[] = [];
    const windowStart = now - HISTORY_WINDOW_MS;

    for (const name of this.serviceOrder) {
      const entry = this.history.get(name)!;
      const tx = entry.transitions;

      for (let i = 0; i < tx.length; i++) {
        const t = tx[i];
        const next = tx[i + 1];
        const segStart = t.timestamp;
        const segEnd = next ? next.timestamp : now;

        // Skip zero-width segments
        if (segEnd - segStart < 1000) continue;

        const relS = (segStart - windowStart) / HISTORY_WINDOW_MS;
        const relE = (segEnd - windowStart) / HISTORY_WINDOW_MS;
        const x = BAR_X0 + relS * BAR_W;
        const w = (relE - relS) * BAR_W;

        segs.push({
          status: t.status,
          x,
          w: Math.max(w, 2),
          startMs: segStart,
          endMs: segEnd,
          serviceName: name,
          displayName: entry.displayName,
        });
      }
    }
    this.segments = segs;

    // Count current incidents (unhealthy segments within last 60s)
    this.incidentCount = segs.filter(
      (s) => s.status === 'Unhealthy' && s.endMs > now - 60_000,
    ).length;

    // SVG height
    const rows = this.serviceOrder.length;
    this.svgHeight =
      rows > 0 ? TOP_PAD + rows * (ROW_H + ROW_GAP) + BOTTOM_PAD : 120;

    this.cdr.markForCheck();
  }

  // ── Tooltip ──
  showTooltip(event: MouseEvent, seg: TimelineSegment): void {
    const rect = (event.currentTarget as HTMLElement)
      .closest('.ht-chart-wrap')
      ?.getBoundingClientRect();
    if (!rect) return;

    this.tooltip = {
      visible: true,
      x: event.clientX - rect.left,
      y: event.clientY - rect.top,
      displayName: seg.displayName,
      status: seg.status,
      color: statusColor(seg.status),
      timeRange: `${fmtTime(seg.startMs)} – ${fmtTime(seg.endMs)} (${fmtDuration(seg.endMs - seg.startMs)})`,
    };
    this.cdr.markForCheck();
  }

  hideTooltip(): void {
    this.tooltip = { ...this.tooltip, visible: false };
    this.cdr.markForCheck();
  }

  // ── Click incident ──
  onSegClick(seg: TimelineSegment): void {
    if (seg.status !== 'Unhealthy') return;

    this.selectedIncident = {
      displayName: seg.displayName,
      startedAt: fmtTime(seg.startMs),
      duration: fmtDuration(seg.endMs - seg.startMs),
    };
    this.cdr.markForCheck();
  }
}
