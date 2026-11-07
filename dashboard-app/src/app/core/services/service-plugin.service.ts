import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, shareReplay } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { ServicePlugin } from '../models/service-plugin.model';

@Injectable({ providedIn: 'root' })
export class ServicePluginService {
  private readonly http = inject(HttpClient);
  private readonly pluginsUrl = `${environment.apiUrl}/plugins`;
  readonly plugins$: Observable<ServicePlugin[]> = this.http
    .get<ServicePlugin[]>(this.pluginsUrl)
    .pipe(catchError(() => of([])), shareReplay({ bufferSize: 1, refCount: true }));

}
