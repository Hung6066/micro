import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, InjectionToken, inject } from "@angular/core";
import { catchError, map, Observable, of, tap } from "rxjs";
import { HisHopeI18nService } from "./his-hope-i18n.service";

export const HIS_HOPE_LOCALIZATION_API_URL = new InjectionToken<string>(
  "HIS_HOPE_LOCALIZATION_API_URL",
  { providedIn: "root", factory: () => "/api/v1" },
);

interface LocalizationResponse {
  values?: Record<string, string>;
}

@Injectable({ providedIn: "root" })
export class HisHopeLocalizationApiService {
  private readonly http = inject(HttpClient);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly apiUrl = inject(HIS_HOPE_LOCALIZATION_API_URL).replace(/\/$/, "");

  load(locale = this.i18n.apiLocale()): Observable<Record<string, string>> {
    const params = new HttpParams().set("locale", locale);
    return this.http.get<LocalizationResponse>(`${this.apiUrl}/localization`, { params }).pipe(
      tap(response => this.i18n.registerTranslations(locale, response.values ?? {})),
      map(response => response.values ?? {}),
      catchError(() => of({})),
    );
  }

  savePreferredLanguage(locale = this.i18n.apiLocale()): Observable<unknown> {
    return this.http.put(`${this.apiUrl}/auth/me/preferences`, { preferredLanguage: locale }).pipe(
      catchError(() => of(null)),
    );
  }
}
