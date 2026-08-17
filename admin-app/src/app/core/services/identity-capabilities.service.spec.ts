import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AdminApiService } from './admin-api.service';
import { IdentityCapabilitiesService } from './identity-capabilities.service';

describe('IdentityCapabilitiesService', () => {
  let service: IdentityCapabilitiesService;

  beforeEach(() => {
    const api = {
      getDevicePosturePolicy: () => of({ id: 'default', mode: 'observe', providers: ['advanced-compliance'], evidenceTtlSeconds: 900, requiredSignals: [], version: '1', updatedAt: '2026-01-01T00:00:00Z' }),
      getDevicePostureAssessments: () => of([]),
      getIdentitySettings: () => of([{ key: 'PROVISIONING_MODE', value: 'dry-run' }]),
      getAuditLogs: () => of({ items: [], totalCount: 0, page: 1, pageSize: 25 }),
      getProvisioningJobs: () => of([]),
      getMtlsBindings: () => of([]),
      getRadiusEapTlsStatus: () => of({ enabled: false, trustedCaConfigured: false, trustedCaReachable: false, sharedSecretManagedBy: 'radius-outpost' }),
      getSecuritySignalStatus: () => of({ enabled: false, subscriptionCount: 0, subscriptions: [], pending: 0, failed: 0 }),
      getDeliveryHealth: () => of({ mode: 'dry-run', ssfEnabled: false, generatedAt: '2026-01-01T00:00:00Z', deliveries: [] }),
    } as unknown as AdminApiService;
    TestBed.configureTestingModule({ providers: [IdentityCapabilitiesService, { provide: AdminApiService, useValue: api }] });
    service = TestBed.inject(IdentityCapabilitiesService);
  });

  it('loads a complete redacted capability state without vendor secrets', (done) => {
    service.loadState().subscribe(state => {
      expect(state.policy?.mode).toBe('observe');
      expect(state.settings[0].value).toBe('dry-run');
      expect(state.provisioningJobs).toEqual([]);
      expect(state.mtlsBindings).toEqual([]);
      expect(state.deliveryHealth?.deliveries).toEqual([]);
      done();
    });
  });

  it('normalizes unknown failures without exposing response bodies', () => {
    expect(service.normalizeError(new Error('secret-token'))).toEqual({ status: 0, code: 'http_unknown', correlationId: undefined });
  });
});
