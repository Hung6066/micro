import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DependencyGraphComponent } from './dependency-graph.component';
import { Resource, ServiceResource, DatabaseResource, InfrastructureResource } from '../../core/models/resource.model';

describe('DependencyGraphComponent', () => {
  let component: DependencyGraphComponent;
  let fixture: ComponentFixture<DependencyGraphComponent>;

  const mockResources: Resource[] = [
    {
      name: 'api-gateway',
      displayName: 'API Gateway',
      status: 'Running',
      healthStatus: 'Healthy',
      type: 'Service',
      version: '1.0.0',
    } as ServiceResource,
    {
      name: 'identity',
      displayName: 'Identity Service',
      status: 'Running',
      healthStatus: 'Healthy',
      type: 'Service',
      version: '1.0.0',
      databases: ['identity-db'],
    } as ServiceResource,
    {
      name: 'patient',
      displayName: 'Patient Service',
      status: 'Running',
      healthStatus: 'Healthy',
      type: 'Service',
      version: '1.0.0',
      databases: ['patient-db'],
    } as ServiceResource,
    {
      name: 'appointment',
      displayName: 'Appointment Service',
      status: 'Stopped',
      healthStatus: 'Unhealthy',
      type: 'Service',
      version: '1.0.0',
      databases: ['appointment-db'],
    } as ServiceResource,
    {
      name: 'identity-db',
      displayName: 'Identity Database',
      status: 'Running',
      healthStatus: 'Healthy',
      type: 'Database',
      databaseType: 'PostgreSQL',
      connectionState: 'Connected',
    } as DatabaseResource,
    {
      name: 'patient-db',
      displayName: 'Patient Database',
      status: 'Running',
      healthStatus: 'Healthy',
      type: 'Database',
      databaseType: 'PostgreSQL',
      connectionState: 'Connected',
    } as DatabaseResource,
    {
      name: 'appointment-db',
      displayName: 'Appointment Database',
      status: 'Running',
      healthStatus: 'Healthy',
      type: 'Database',
      databaseType: 'PostgreSQL',
      connectionState: 'Connected',
    } as DatabaseResource,
    {
      name: 'rabbitmq',
      displayName: 'RabbitMQ',
      status: 'Running',
      healthStatus: 'Healthy',
      type: 'Infrastructure',
      infrastructureType: 'Message Queue',
    } as InfrastructureResource,
    {
      name: 'redis',
      displayName: 'Redis Cache',
      status: 'Degraded',
      healthStatus: 'Degraded',
      type: 'Infrastructure',
      infrastructureType: 'Cache',
    } as InfrastructureResource,
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DependencyGraphComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DependencyGraphComponent);
    component = fixture.componentInstance;
    component.resources = mockResources;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should compute layout with correct number of nodes', () => {
    const layout = component.layout();
    expect(layout.services.length).toBe(4); // api-gateway, identity, patient, appointment
    expect(layout.databases.length).toBe(3); // identity-db, patient-db, appointment-db
    expect(layout.infrastructure.length).toBe(2); // rabbitmq, redis
  });

  it('should compute edges based on topology rules', () => {
    const edges = component.edges();
    // API Gateway → all other services (3 HTTP edges)
    const httpEdges = edges.filter(e => e.type === 'http');
    expect(httpEdges.length).toBe(3);

    // Identity → all other services except itself and gateway via gRPC (2 gRPC edges)
    const grpcEdges = edges.filter(e => e.type === 'grpc');
    expect(grpcEdges.length).toBe(2);

    // Service → database edges (3 dependency edges)
    const dbEdges = edges.filter(e => e.type === 'dependency' && e.source !== e.target);
    expect(dbEdges.length).toBeGreaterThanOrEqual(3);

    // All services → RabbitMQ + Redis (4 services × 2 = 8)
    const infraEdges = edges.filter(e =>
      e.target === 'rabbitmq' || e.target === 'redis'
    );
    expect(infraEdges.length).toBe(8);
  });

  it('should have all nodes with positions assigned', () => {
    const nodes = component.allNodes();
    for (const node of nodes) {
      expect(node.x).toBeGreaterThan(0);
      expect(node.y).toBeGreaterThan(0);
    }
  });

  it('should emit nodeClick on node click', () => {
    spyOn(component.nodeClick, 'emit');
    const resource = mockResources[0];
    component.onNodeClick(resource);
    expect(component.nodeClick.emit).toHaveBeenCalledWith(resource);
  });

  it('should zoom in and out', () => {
    expect(component.zoomLevel()).toBe(100);
    component.zoomIn();
    expect(component.zoomLevel()).toBe(125);
    component.zoomIn();
    expect(component.zoomLevel()).toBe(150);
    component.zoomOut();
    expect(component.zoomLevel()).toBe(125);
    component.zoomReset();
    expect(component.zoomLevel()).toBe(100);
  });

  it('should clamp zoom between 50 and 200', () => {
    component.zoomLevel.set(50);
    component.zoomOut();
    expect(component.zoomLevel()).toBe(50);
    component.zoomLevel.set(200);
    component.zoomIn();
    expect(component.zoomLevel()).toBe(200);
  });

  it('should return correct node color based on status', () => {
    const layout = component.layout();
    const running = layout.services.find(s => s.status === 'Running')!;
    const stopped = layout.services.find(s => s.status === 'Stopped')!;
    const degraded = component.layout().infrastructure.find(s => s.status === 'Degraded')!;

    expect(component.nodeColor(running)).toBe('#2F6B4A');
    expect(component.nodeColor(stopped)).toBe('#C25450');
    expect(component.nodeColor(degraded)).toBe('#B6581C');
  });

  it('should detect connection between nodes', () => {
    const apiGateway = component.layout().services.find(s => s.id === 'api-gateway')!;
    const patient = component.layout().services.find(s => s.id === 'patient')!;
    expect(component.isConnected(apiGateway.id, patient.id)).toBeTrue();
  });

  it('should detect edges connected to hovered node', () => {
    const edges = component.edges();
    const apiGatewayEdge = edges.find(e => e.source === 'api-gateway');
    component.hoveredNode.set('api-gateway');
    expect(component.isConnectedToHovered(apiGatewayEdge!)).toBeTrue();
  });

  it('should render SVG element', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const svg = compiled.querySelector('svg');
    expect(svg).toBeTruthy();
    expect(svg?.getAttribute('viewBox')).toContain('0 0');
  });
});
