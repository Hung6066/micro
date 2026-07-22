import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HealthTimelineComponent } from './health-timeline.component';
import { ResourceService } from '../../core/services/resource.service';
import { Resource, ServiceResource } from '../../core/models/resource.model';
import { of } from 'rxjs';

describe('HealthTimelineComponent', () => {
  let component: HealthTimelineComponent;
  let fixture: ComponentFixture<HealthTimelineComponent>;
  let mockResourceService: jasmine.SpyObj<ResourceService>;

  const mockResources: Resource[] = [
    {
      name: 'api-gateway',
      displayName: 'API Gateway',
      status: 'Running',
      healthStatus: 'Healthy',
      type: 'Service',
    } as ServiceResource,
    {
      name: 'identity',
      displayName: 'Identity Service',
      status: 'Running',
      healthStatus: 'Healthy',
      type: 'Service',
    } as ServiceResource,
    {
      name: 'patient',
      displayName: 'Patient Service',
      status: 'Running',
      healthStatus: 'Degraded',
      type: 'Service',
    } as ServiceResource,
    {
      name: 'appointment',
      displayName: 'Appointment Service',
      status: 'Stopped',
      healthStatus: 'Unhealthy',
      type: 'Service',
    } as ServiceResource,
  ];

  beforeEach(async () => {
    mockResourceService = jasmine.createSpyObj('ResourceService', ['getAll']);
    mockResourceService.getAll.and.returnValue(of(mockResources));

    await TestBed.configureTestingModule({
      imports: [HealthTimelineComponent],
      providers: [
        { provide: ResourceService, useValue: mockResourceService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HealthTimelineComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should poll resources on init and populate service order', () => {
    expect(mockResourceService.getAll).toHaveBeenCalled();
    expect(component.serviceOrder.length).toBe(4);
    expect(component.serviceOrder).toContain('api-gateway');
    expect(component.serviceOrder).toContain('identity');
    expect(component.serviceOrder).toContain('patient');
    expect(component.serviceOrder).toContain('appointment');
  });

  it('should compute segments from poll data', () => {
    expect(component.segments.length).toBeGreaterThan(0);
    // Each service should have at least one segment
    const serviceSegments = new Set(component.segments.map(s => s.serviceName));
    expect(serviceSegments.size).toBe(4);
  });

  it('should count incidents (Unhealthy segments near current time)', () => {
    // appointment is Unhealthy, so incidentCount should be >= 1
    expect(component.incidentCount).toBeGreaterThanOrEqual(1);
  });

  it('should return correct status color', () => {
    expect(component.getStatusColor('Healthy')).toBe('#2F6B4A');
    expect(component.getStatusColor('Unhealthy')).toBe('#C25450');
    expect(component.getStatusColor('Degraded')).toBe('#B6581C');
    expect(component.getStatusColor('Unknown')).toBe('#A1A09B');
    expect(component.getStatusColor('')).toBe('#A1A09B');
  });

  it('should compute row Y positions correctly', () => {
    const y0 = component.rowY(0);
    const y1 = component.rowY(1);
    expect(y1 - y0).toBe(30); // ROW_H (24) + ROW_GAP (6)
  });

  it('should filter segments by service name', () => {
    const segs = component.segmentsFor('api-gateway');
    expect(segs.length).toBeGreaterThan(0);
    expect(segs.every(s => s.serviceName === 'api-gateway')).toBeTrue();
  });

  it('should return display name from history', () => {
    expect(component.displayNameOf('api-gateway')).toBe('API Gateway');
    expect(component.displayNameOf('unknown')).toBe('unknown');
  });

  it('should have correct tick labels', () => {
    const ticks = component.ticks;
    expect(ticks.length).toBe(5);
    expect(ticks[0].label).toBe('24h ago');
    expect(ticks[4].label).toBe('Now');
    expect(ticks[4].x).toBeGreaterThan(ticks[0].x);
  });

  it('should show and hide tooltip', () => {
    const seg = component.segments[0];
    if (!seg) return;

    const mockEvent = { clientX: 100, clientY: 200, currentTarget: { closest: () => ({ getBoundingClientRect: () => ({ left: 0, top: 0 }) }) } } as unknown as MouseEvent;

    component.showTooltip(mockEvent, seg);
    expect(component.tooltip.visible).toBeTrue();
    expect(component.tooltip.displayName).toBe(seg.displayName);
    expect(component.tooltip.status).toBe(seg.status);

    component.hideTooltip();
    expect(component.tooltip.visible).toBeFalse();
  });

  it('should select incident on unhealthy segment click', () => {
    const unhealthySeg = component.segments.find(s => s.status === 'Unhealthy');
    if (!unhealthySeg) return;

    component.onSegClick(unhealthySeg);
    expect(component.selectedIncident).not.toBeNull();
    expect(component.selectedIncident!.displayName).toBe(unhealthySeg.displayName);
  });

  it('should ignore click on non-unhealthy segment', () => {
    const healthySeg = component.segments.find(s => s.status !== 'Unhealthy');
    if (!healthySeg) return;

    component.onSegClick(healthySeg);
    expect(component.selectedIncident).toBeNull();
  });

  it('should render SVG with segments', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const svg = compiled.querySelector('svg');
    expect(svg).toBeTruthy();
    expect(svg?.getAttribute('viewBox')).toContain('0 0');
  });

  it('should render a legend', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const legend = compiled.querySelector('.ht-legend');
    expect(legend).toBeTruthy();
    expect(legend!.textContent).toContain('Healthy');
    expect(legend!.textContent).toContain('Degraded');
    expect(legend!.textContent).toContain('Down');
    expect(legend!.textContent).toContain('Unknown');
  });

  it('should show incident chip when incidents exist', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
    const chip = compiled.querySelector('.incident-chip');
    // appointment is Unhealthy, so there should be at least 1 incident
    expect(component.incidentCount).toBeGreaterThan(0);
    expect(chip).toBeTruthy();
    expect(chip!.textContent).toContain('incident');
  });

  it('should render empty state when no data', () => {
    mockResourceService.getAll.and.returnValue(of([]));
    const emptyFixture = TestBed.createComponent(HealthTimelineComponent);
    emptyFixture.detectChanges();
    const compiled = emptyFixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.ht-empty')).toBeTruthy();
    expect(compiled.querySelector('.ht-empty')!.textContent).toContain('Waiting');
  });
});
