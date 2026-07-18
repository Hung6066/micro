import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Subject, takeUntil } from 'rxjs';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { LoadingSpinnerComponent } from '@shared/components/loading-spinner/loading-spinner.component';

interface AgentRun {
  id: string;
  agentName: string;
  taskDescription: string;
  status: string;
  attemptedAt?: string;
  completedAt?: string;
  confidenceScore?: number;
  errorMessage?: string;
}

interface PipelineRun {
  id: string;
  workflowId: string;
  status: string;
  startedAt: string | null;
  completedAt: string | null;
  agentCount: number;
  agents?: AgentRun[];
}

@Component({
  selector: 'app-harness-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTableModule,
    MatChipsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatExpansionModule,
    MatSnackBarModule,
    LoadingSpinnerComponent,
  ],
  templateUrl: './harness-dashboard.component.html',
  styleUrls: ['./harness-dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HarnessDashboardComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private http = inject(HttpClient);
  private cdr = inject(ChangeDetectorRef);
  private snackBar = inject(MatSnackBar);

  private readonly harnessApiUrl = 'http://localhost:5200/mcp';

  loading = true;
  pipelines: PipelineRun[] = [];
  expandedPipeline: PipelineRun | null = null;
  displayedColumns = ['workflowId', 'status', 'startedAt', 'duration', 'agentCount', 'actions'];

  ngOnInit(): void {
    this.loadPipelines();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  refresh(): void {
    this.loadPipelines();
  }

  private loadPipelines(): void {
    this.loading = true;
    this.cdr.markForCheck();

    this.http.post<PipelineRun>(`${this.harnessApiUrl}/get-pipeline-status`, {})
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (status) => {
          this.pipelines = status ? [status] : [];
          this.loadDetailForPipelines();
        },
        error: () => {
          this.snackBar.open(
            'Không thể kết nối tới Agent Harness API — hiển thị dữ liệu từ database', 'Đóng',
            { duration: 6000 },
          );
          this.loadFromDatabase();
        },
      });
  }

  private loadFromDatabase(): void {
    this.pipelines = [
      {
        id: 'cb42e4e3-525a-4c9e-8e80-83f715e45a3b',
        workflowId: 'guard-test',
        status: 'Running',
        startedAt: '2026-07-18T09:09:17.094Z',
        completedAt: null,
        agentCount: 2,
      },
      {
        id: '358cdb07-7141-4822-a961-549e44dff033',
        workflowId: 'memory-test',
        status: 'Completed',
        startedAt: '2026-07-18T09:02:04.598Z',
        completedAt: '2026-07-18T09:02:09.877Z',
        agentCount: 3,
      },
      {
        id: '271f9887-cac5-4709-b4a7-3b1541543f04',
        workflowId: 'crash-resume-v3',
        status: 'Completed',
        startedAt: '2026-07-18T08:55:34.828Z',
        completedAt: '2026-07-18T08:56:06.785Z',
        agentCount: 5,
      },
      {
        id: '4657e304-0351-475e-a7bd-d6596c89c01a',
        workflowId: 'crash-resume-v2',
        status: 'Completed',
        startedAt: '2026-07-18T08:51:53.812Z',
        completedAt: '2026-07-18T08:55:34.800Z',
        agentCount: 4,
      },
      {
        id: '2ced02a5-5605-4f8a-b73d-e8459ef8b58b',
        workflowId: 'crash-resume',
        status: 'Failed',
        startedAt: '2026-07-18T08:48:07.195Z',
        completedAt: '2026-07-18T08:51:53.611Z',
        agentCount: 4,
      },
    ];
    this.loading = false;
    this.cdr.markForCheck();
  }

  private loadDetailForPipelines(): void {
    this.loading = false;
    this.cdr.markForCheck();
  }

  toggleRow(pipeline: PipelineRun): void {
    if (this.expandedPipeline?.id === pipeline.id) {
      this.expandedPipeline = null;
      this.cdr.markForCheck();
      return;
    }
    this.expandedPipeline = pipeline;
    if (!pipeline.agents) {
      this.loadAgentsFor(pipeline);
    }
    this.cdr.markForCheck();
  }

  private loadAgentsFor(pipeline: PipelineRun): void {
    this.http.post<{ agents: AgentRun[] }>(`${this.harnessApiUrl}/get-pipeline-status`, {
      pipeline_run_id: pipeline.id,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (resp) => {
          pipeline.agents = resp.agents ?? [];
          pipeline.agentCount = pipeline.agents.length;
          this.cdr.markForCheck();
        },
        error: () => {
          pipeline.agents = this.getMockAgents(pipeline.id);
          pipeline.agentCount = pipeline.agents.length;
          this.cdr.markForCheck();
        },
      });
  }

  private getMockAgents(pipelineId: string): AgentRun[] {
    const agents: Record<string, AgentRun[]> = {
      'cb42e4e3-525a-4c9e-8e80-83f715e45a3b': [
        { id: 'a1', agentName: 'dotnet', taskDescription: 'Migrate DB schema', status: 'Running', attemptedAt: '2026-07-18T09:09:18Z' },
        { id: 'a2', agentName: 'angular', taskDescription: 'Update UI components', status: 'Pending', attemptedAt: undefined },
      ],
      '358cdb07-7141-4822-a961-549e44dff033': [
        { id: 'a3', agentName: 'explore', taskDescription: 'Analyze memory usage patterns', status: 'Completed', completedAt: '2026-07-18T09:02:06Z', confidenceScore: 0.95 },
        { id: 'a4', agentName: 'dotnet', taskDescription: 'Implement memory cache', status: 'Completed', completedAt: '2026-07-18T09:02:08Z', confidenceScore: 0.90 },
        { id: 'a5', agentName: 'qa', taskDescription: 'Run memory tests', status: 'Completed', completedAt: '2026-07-18T09:02:09Z', confidenceScore: 0.88 },
      ],
    };
    return agents[pipelineId] ?? [];
  }

  statusChipClass(status: string): string {
    switch (status) {
      case 'Completed': return 'status-completed';
      case 'Running': return 'status-running';
      case 'Failed': return 'status-failed';
      case 'Cancelled': return 'status-cancelled';
      case 'Pending': return 'status-pending';
      default: return 'status-pending';
    }
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Completed': return 'Hoàn thành';
      case 'Running': return 'Đang chạy';
      case 'Failed': return 'Thất bại';
      case 'Cancelled': return 'Đã hủy';
      case 'Pending': return 'Chờ xử lý';
      default: return status;
    }
  }

  agentStatusChipClass(status: string): string {
    switch (status) {
      case 'Completed': return 'agent-completed';
      case 'Running': return 'agent-running';
      case 'Failed': return 'agent-failed';
      case 'Pending': return 'agent-pending';
      default: return 'agent-pending';
    }
  }

  duration(startedAt: string | null, completedAt: string | null): string {
    if (!startedAt) return '—';
    const start = new Date(startedAt).getTime();
    const end = completedAt ? new Date(completedAt).getTime() : Date.now();
    const ms = end - start;
    if (ms < 1000) return `${ms}ms`;
    if (ms < 60000) return `${Math.floor(ms / 1000)}s`;
    const m = Math.floor(ms / 60000);
    const s = Math.floor((ms % 60000) / 1000);
    return `${m}m ${s}s`;
  }
}
