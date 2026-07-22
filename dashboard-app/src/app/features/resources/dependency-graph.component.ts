import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Resource } from '../../core/models/resource.model';

interface GraphNode {
  id: string;
  label: string;
  type: 'service' | 'database' | 'infrastructure';
  status: string;
  healthStatus: string;
  x: number;
  y: number;
  radius: number;
  resource: Resource;
}

interface GraphEdge {
  source: string;
  target: string;
  type: 'http' | 'grpc' | 'dependency';
}

interface NodeLayout {
  services: GraphNode[];
  databases: GraphNode[];
  infrastructure: GraphNode[];
}

const SERVICE_NAMES_LOWER = [
  'api-gateway', 'gateway', 'apigateway',
  'identity', 'identity-service', 'auth',
  'patient', 'patient-service',
  'appointment', 'appointment-service',
  'clinical', 'clinical-service',
  'lab', 'lab-service',
  'pharmacy', 'pharmacy-service',
  'billing', 'billing-service',
];

const SVG_VIEWBOX = { width: 1000, height: 700 };
const NODE_RADIUS = { service: 24, database: 20, infrastructure: 18 };
const LAYER_SPACING = 150;
const LAYER_PADDING = 80;

@Component({
  selector: 'app-dependency-graph',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="graph-container">
      <!-- Toolbar -->
      <div class="graph-toolbar">
        <span class="graph-title">Service Dependency Graph</span>
        <div class="zoom-controls">
          <button class="zoom-btn" (click)="zoomOut()" title="Zoom out" aria-label="Zoom out">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="5" y1="12" x2="19" y2="12"/>
            </svg>
          </button>
          <span class="zoom-level">{{ zoomLevel() }}%</span>
          <button class="zoom-btn" (click)="zoomIn()" title="Zoom in" aria-label="Zoom in">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="12" y1="5" x2="12" y2="19"/>
              <line x1="5" y1="12" x2="19" y2="12"/>
            </svg>
          </button>
          <button class="zoom-btn zoom-reset" (click)="zoomReset()" title="Reset zoom" aria-label="Reset zoom">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/>
              <path d="M3 3v5h5"/>
            </svg>
          </button>
        </div>
        <div class="graph-legend">
          <span class="legend-item"><span class="legend-dot" style="background:#2F6B4A"></span>Healthy</span>
          <span class="legend-item"><span class="legend-dot" style="background:#B6581C"></span>Degraded</span>
          <span class="legend-item"><span class="legend-dot" style="background:#C25450"></span>Stopped</span>
          <span class="legend-item"><span class="legend-dot" style="background:#A1A09B"></span>Unknown</span>
          <span class="legend-divider"></span>
          <span class="legend-item"><span class="legend-line dashed"></span>HTTP</span>
          <span class="legend-item"><span class="legend-line solid"></span>gRPC</span>
        </div>
      </div>

      <!-- SVG Graph -->
      <div class="graph-canvas" #canvasRef>
        <svg
          [attr.viewBox]="'0 0 ' + SVG_VIEWBOX.width + ' ' + SVG_VIEWBOX.height"
          class="graph-svg"
          (mouseleave)="hoveredNode.set(null)"
        >
          <defs>
            <!-- Arrow markers -->
            <marker id="arrow-http" viewBox="0 0 10 10" refX="22" refY="5"
                    markerWidth="6" markerHeight="6" orient="auto-start-reverse">
              <path d="M 0 0 L 10 5 L 0 10 z" fill="#A1A09B"/>
            </marker>
            <marker id="arrow-grpc" viewBox="0 0 10 10" refX="22" refY="5"
                    markerWidth="6" markerHeight="6" orient="auto-start-reverse">
              <path d="M 0 0 L 10 5 L 0 10 z" fill="#2F6B4A"/>
            </marker>
            <marker id="arrow-dep" viewBox="0 0 10 10" refX="22" refY="5"
                    markerWidth="6" markerHeight="6" orient="auto-start-reverse">
              <path d="M 0 0 L 10 5 L 0 10 z" fill="#787774"/>
            </marker>

            <!-- Node glow on hover -->
            <filter id="node-glow">
              <feDropShadow dx="0" dy="0" stdDeviation="4" flood-color="#2F6B4A" flood-opacity="0.3"/>
            </filter>
          </defs>

          <!-- Background -->
          <rect width="100%" height="100%" fill="#FAFAF8" rx="8"/>

          <!-- Edges -->
          <g class="edges-layer" *ngFor="let edge of edges()">
            <path
              [attr.d]="edgePath(edge)"
              [class.edge-http]="edge.type === 'http'"
              [class.edge-grpc]="edge.type === 'grpc'"
              [class.edge-dep]="edge.type === 'dependency'"
              [class.edge-highlighted]="isConnectedToHovered(edge)"
              [attr.marker-end]="edgeMarker(edge)"
            />
          </g>

          <!-- Nodes -->
          <g class="nodes-layer">
            <g
              *ngFor="let node of allNodes()"
              class="node-group"
              [class.node-highlighted]="hoveredNode() === node.id || isConnected(hoveredNode(), node.id)"
              [class.node-dimmed]="hoveredNode() !== null && hoveredNode() !== node.id && !isConnected(hoveredNode(), node.id)"
              (mouseenter)="hoveredNode.set(node.id)"
              (click)="onNodeClick(node.resource)"
              style="cursor: pointer;"
            >
              <!-- Node circle -->
              <circle
                [attr.cx]="node.x"
                [attr.cy]="node.y"
                [attr.r]="node.radius"
                [attr.fill]="nodeColor(node)"
                [attr.stroke]="nodeStroke(node)"
                stroke-width="2"
                [attr.filter]="hoveredNode() === node.id ? 'url(#node-glow)' : null"
              />
              <!-- Node icon (simple shape based on type) -->
              <g [attr.transform]="'translate(' + (node.x - 8) + ',' + (node.y - 8) + ')'">
                <!-- Service: cube -->
                <ng-container *ngIf="node.type === 'service'">
                  <rect x="2" y="2" width="12" height="12" rx="2" fill="white" opacity="0.85"/>
                  <rect x="4" y="4" width="4" height="4" rx="1" fill="none" stroke="#1A1A1A" stroke-width="0.8" opacity="0.5"/>
                </ng-container>
                <!-- Database: cylinder -->
                <ng-container *ngIf="node.type === 'database'">
                  <ellipse cx="8" cy="4" rx="6" ry="2.5" fill="white" opacity="0.85"/>
                  <rect x="2" y="4" width="12" height="6" fill="white" opacity="0.85"/>
                  <ellipse cx="8" cy="10" rx="6" ry="2.5" fill="white" opacity="0.85"/>
                </ng-container>
                <!-- Infrastructure: diamond -->
                <ng-container *ngIf="node.type === 'infrastructure'">
                  <rect x="3" y="3" width="10" height="10" rx="1" fill="white" opacity="0.85"
                        transform="rotate(45, 8, 8)"/>
                </ng-container>
              </g>
              <!-- Node label -->
              <text
                [attr.x]="node.x"
                [attr.y]="node.y + node.radius + 16"
                text-anchor="middle"
                class="node-label"
              >{{ node.label }}</text>
            </g>
          </g>
        </svg>
      </div>
    </div>
  `,
  styles: [`
    .graph-container {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .graph-toolbar {
      display: flex;
      align-items: center;
      gap: 16px;
      flex-wrap: wrap;
      padding: 4px 0;
    }
    .graph-title {
      font-size: 13px;
      font-weight: 500;
      color: var(--text-secondary, #787774);
      margin-right: auto;
    }
    .zoom-controls {
      display: flex;
      align-items: center;
      gap: 4px;
      background: var(--surface-white, #FFFFFF);
      border: 1px solid var(--border-default, #EAEAEA);
      border-radius: 6px;
      padding: 2px;
    }
    .zoom-btn {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 28px;
      height: 28px;
      border: none;
      background: transparent;
      border-radius: 4px;
      cursor: pointer;
      color: var(--text-secondary, #787774);
      transition: background 150ms ease, color 150ms ease;
    }
    .zoom-btn:hover {
      background: var(--bg-warm, #F7F6F3);
      color: var(--text-primary, #1A1A1A);
    }
    .zoom-btn:active {
      transform: scale(0.95);
    }
    .zoom-reset {
      margin-left: 2px;
    }
    .zoom-level {
      font-size: 12px;
      font-weight: 500;
      color: var(--text-secondary, #787774);
      min-width: 36px;
      text-align: center;
      font-variant-numeric: tabular-nums;
    }
    .graph-legend {
      display: flex;
      align-items: center;
      gap: 12px;
      font-size: 11px;
      color: var(--text-muted, #A1A09B);
    }
    .legend-item {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .legend-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      display: inline-block;
    }
    .legend-divider {
      width: 1px;
      height: 14px;
      background: var(--border-default, #EAEAEA);
    }
    .legend-line {
      display: inline-block;
      width: 16px;
      height: 2px;
      flex-shrink: 0;
    }
    .legend-line.dashed {
      border-top: 2px dashed #A1A09B;
      height: 0;
    }
    .legend-line.solid {
      background: #2F6B4A;
    }
    .graph-canvas {
      background: var(--surface-white, #FFFFFF);
      border: 1px solid var(--border-default, #EAEAEA);
      border-radius: 8px;
      overflow: hidden;
      min-height: 400px;
    }
    .graph-svg {
      display: block;
      width: 100%;
      height: auto;
      min-height: 500px;
    }

    /* Edge styles */
    :host ::ng-deep .edge-http {
      fill: none;
      stroke: #A1A09B;
      stroke-width: 1.5;
      stroke-dasharray: 5 3;
      transition: stroke 150ms ease, stroke-width 150ms ease;
    }
    :host ::ng-deep .edge-grpc {
      fill: none;
      stroke: #2F6B4A;
      stroke-width: 2;
      transition: stroke 150ms ease, stroke-width 150ms ease;
    }
    :host ::ng-deep .edge-dep {
      fill: none;
      stroke: #787774;
      stroke-width: 1.5;
      transition: stroke 150ms ease, stroke-width 150ms ease;
    }
    :host ::ng-deep .edge-highlighted {
      stroke-opacity: 1 !important;
      stroke-width: 2.5 !important;
    }

    /* Node styles */
    :host ::ng-deep .node-group {
      transition: opacity 150ms ease;
    }
    :host ::ng-deep .node-group text.node-label {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, system-ui, sans-serif;
      font-size: 10px;
      fill: var(--text-primary, #1A1A1A);
      font-weight: 500;
      pointer-events: none;
    }
    :host ::ng-deep .node-highlighted {
      opacity: 1;
    }
    :host ::ng-deep .node-dimmed {
      opacity: 0.3;
    }
    :host ::ng-deep .node-group circle {
      transition: filter 150ms ease;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DependencyGraphComponent {
  @Input({ required: true }) resources: Resource[] = [];
  @Output() nodeClick = new EventEmitter<Resource>();

  readonly SVG_VIEWBOX = SVG_VIEWBOX;
  readonly hoveredNode = signal<string | null>(null);
  readonly zoomLevel = signal(100);

  readonly layout = computed<NodeLayout>(() => {
    const resources = this.resources;
    const services: GraphNode[] = [];
    const databases: GraphNode[] = [];
    const infrastructure: GraphNode[] = [];

    for (const r of resources) {
      const type = (r.type ?? '').toLowerCase();
      const node: GraphNode = {
        id: r.name,
        label: r.displayName || r.name,
        type: type === 'service' || type === 'services' ? 'service'
            : type === 'database' || type === 'databases' ? 'database'
            : 'infrastructure',
        status: r.status ?? 'Unknown',
        healthStatus: r.healthStatus ?? 'Unknown',
        x: 0,
        y: 0,
        radius: type === 'service' || type === 'services' ? NODE_RADIUS.service
              : type === 'database' || type === 'databases' ? NODE_RADIUS.database
              : NODE_RADIUS.infrastructure,
        resource: r,
      };

      if (node.type === 'service') services.push(node);
      else if (node.type === 'database') databases.push(node);
      else infrastructure.push(node);
    }

    // Sort services: known order first, then alphabetically
    const serviceOrder = (s: GraphNode) => {
      const idx = SERVICE_NAMES_LOWER.indexOf(s.id.toLowerCase());
      return idx >= 0 ? idx : 100 + s.id.length;
    };
    services.sort((a, b) => serviceOrder(a) - serviceOrder(b));

    // Compute positions
    const gw = SVG_VIEWBOX.width;
    const gh = SVG_VIEWBOX.height;

    // Layer 0: API Gateway at top center
    const gatewayLayer = services.filter(s =>
      ['api-gateway', 'gateway', 'apigateway'].includes(s.id.toLowerCase())
    );
    const otherServices = services.filter(s =>
      !['api-gateway', 'gateway', 'apigateway'].includes(s.id.toLowerCase())
    );

    // Position gateway
    if (gatewayLayer.length > 0) {
      gatewayLayer[0].x = gw / 2;
      gatewayLayer[0].y = LAYER_PADDING + 20;
    }

    // Layer 1: Other services
    const serviceY = LAYER_PADDING + 20 + (gatewayLayer.length > 0 ? LAYER_SPACING : 0) + 30;
    const serviceCount = otherServices.length;
    if (serviceCount > 0) {
      const spacing = Math.min(140, (gw - 80) / serviceCount);
      const totalWidth = (serviceCount - 1) * spacing;
      const startX = (gw - totalWidth) / 2;
      otherServices.forEach((s, i) => {
        s.x = startX + i * spacing;
        s.y = serviceY;
      });
    }

    // Layer 2: Infrastructure
    const infraY = serviceY + LAYER_SPACING;
    const infraCount = infrastructure.length;
    if (infraCount > 0) {
      const spacing = Math.min(140, (gw - 80) / infraCount);
      const totalWidth = (infraCount - 1) * spacing;
      const startX = (gw - totalWidth) / 2;
      infrastructure.forEach((infra, i) => {
        infra.x = startX + i * spacing;
        infra.y = infraY;
      });
    }

    // Layer 3: Databases — position under their respective services
    const dbY = (infraCount > 0 ? infraY : serviceY) + LAYER_SPACING;
    const dbCount = databases.length;
    if (dbCount > 0) {
      const spacing = Math.min(120, (gw - 80) / dbCount);
      const totalWidth = (dbCount - 1) * spacing;
      const startX = (gw - totalWidth) / 2;
      databases.forEach((db, i) => {
        db.x = startX + i * spacing;
        db.y = dbY;
      });
    }

    return { services: [...gatewayLayer, ...otherServices], databases, infrastructure };
  });

  readonly allNodes = computed<GraphNode[]>(() => {
    const l = this.layout();
    return [...l.services, ...l.databases, ...l.infrastructure];
  });

  readonly edges = computed<GraphEdge[]>(() => {
    const l = this.layout();
    const edges: GraphEdge[] = [];
    const allSvc = l.services;
    const allDb = l.databases;
    const allInfra = l.infrastructure;
    const nodeMap = new Map<string, GraphNode>();
    for (const n of this.allNodes()) nodeMap.set(n.id.toLowerCase(), n);

    // 1. Service → database (from databases[] field on ServiceResource)
    for (const svc of allSvc) {
      const svcResource = svc.resource as any;
      const dbNames: string[] = svcResource.databases ?? [];
      for (const dbName of dbNames) {
        if (nodeMap.has(dbName.toLowerCase())) {
          edges.push({ source: svc.id, target: dbName, type: 'dependency' });
        }
      }
    }

    // 2. API Gateway → all services
    const gateway = allSvc.find(s =>
      ['api-gateway', 'gateway', 'apigateway'].includes(s.id.toLowerCase())
    );
    if (gateway) {
      for (const svc of allSvc) {
        if (svc.id !== gateway.id) {
          edges.push({ source: gateway.id, target: svc.id, type: 'http' });
        }
      }
    }

    // 3. All services → RabbitMQ, Redis (infra)
    const infraRabbit = allInfra.find(i => i.id.toLowerCase().includes('rabbit'));
    const infraRedis = allInfra.find(i => i.id.toLowerCase().includes('redis'));
    for (const svc of allSvc) {
      if (infraRabbit) edges.push({ source: svc.id, target: infraRabbit.id, type: 'dependency' });
      if (infraRedis) edges.push({ source: svc.id, target: infraRedis.id, type: 'dependency' });
    }

    // 4. Identity service → all services
    const identity = allSvc.find(s =>
      ['identity', 'identity-service', 'auth'].includes(s.id.toLowerCase())
    );
    if (identity) {
      for (const svc of allSvc) {
        if (svc.id !== identity.id && svc.id !== gateway?.id) {
          edges.push({ source: identity.id, target: svc.id, type: 'grpc' });
        }
      }
    }

    return edges;
  });

  nodeColor(node: GraphNode): string {
    const st = (node.status ?? '').toLowerCase();
    if (st === 'running' || st === 'healthy') return '#2F6B4A';
    if (st === 'degraded' || st === 'degraded') return '#B6581C';
    if (st === 'stopped' || st === 'unhealthy') return '#C25450';
    return '#A1A09B';
  }

  nodeStroke(node: GraphNode): string {
    const st = (node.status ?? '').toLowerCase();
    if (st === 'running' || st === 'healthy') return '#1E5A3A';
    if (st === 'degraded') return '#8E4510';
    if (st === 'stopped' || st === 'unhealthy') return '#A03E3A';
    return '#8A8A85';
  }

  edgePath(edge: GraphEdge): string {
    const nodes = this.allNodes();
    const src = nodes.find(n => n.id === edge.source);
    const tgt = nodes.find(n => n.id === edge.target);
    if (!src || !tgt) return '';

    const dx = tgt.x - src.x;
    const dy = tgt.y - src.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    const curvature = Math.min(40, dist * 0.15);

    // Control point offset perpendicular to the line direction
    const nx = -dy / dist;
    const ny = dx / dist;
    const cx = (src.x + tgt.x) / 2 + nx * curvature;
    const cy = (src.y + tgt.y) / 2 + ny * curvature;

    return `M ${src.x} ${src.y} Q ${cx} ${cy} ${tgt.x} ${tgt.y}`;
  }

  edgeMarker(edge: GraphEdge): string {
    const map: Record<string, string> = {
      'http': 'url(#arrow-http)',
      'grpc': 'url(#arrow-grpc)',
      'dependency': 'url(#arrow-dep)',
    };
    return map[edge.type] ?? 'url(#arrow-dep)';
  }

  isConnectedToHovered(edge: GraphEdge): boolean {
    const h = this.hoveredNode();
    if (!h) return false;
    return edge.source === h || edge.target === h;
  }

  isConnected(hovered: string | null, nodeId: string): boolean {
    if (!hovered || hovered === nodeId) return true;
    return this.edges().some(e =>
      (e.source === hovered && e.target === nodeId) ||
      (e.target === hovered && e.source === nodeId)
    );
  }

  onNodeClick(resource: Resource): void {
    this.nodeClick.emit(resource);
  }

  zoomIn(): void {
    this.zoomLevel.update(z => Math.min(200, z + 25));
  }

  zoomOut(): void {
    this.zoomLevel.update(z => Math.max(50, z - 25));
  }

  zoomReset(): void {
    this.zoomLevel.set(100);
  }
}
