import { ChangeDetectorRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { HisHopeI18nService } from '@his-hope/frontend-foundation';
import { AdminApiService } from '../../core/services/admin-api.service';
import { DatabasePlatformPageComponent } from './database-platform-page.component';

describe('DatabasePlatformPageComponent', () => {
  it('marks the view for rendering after database resources load', () => {
    const cdr = jasmine.createSpyObj<ChangeDetectorRef>('ChangeDetectorRef', ['markForCheck']);
    const api = jasmine.createSpyObj<AdminApiService>('AdminApiService', ['getPlatformResources']);
    api.getPlatformResources.and.returnValue(of([{ name: 'identitydb', type: 'database', healthStatus: 'Healthy', connections: 2 }]));

    TestBed.configureTestingModule({
      providers: [
        { provide: AdminApiService, useValue: api },
        { provide: ChangeDetectorRef, useValue: cdr },
        { provide: HisHopeI18nService, useValue: { t: (_key: string, fallback: string) => fallback } },
      ],
    });
    const component = TestBed.runInInjectionContext(() => new DatabasePlatformPageComponent());

    component.load();

    expect(component.loading).toBeFalse();
    expect(component.databases).toHaveSize(1);
    expect(cdr.markForCheck).toHaveBeenCalled();
  });
});
