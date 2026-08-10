import { HttpClient } from "@angular/common/http";
import { Inject, Injectable, InjectionToken } from "@angular/core";
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
  private readonly apiUrl: string;

  constructor(
    private readonly http: HttpClient,
    private readonly i18n: HisHopeI18nService,
    @Inject(HIS_HOPE_LOCALIZATION_API_URL) apiUrl: string,
  ) {
    this.apiUrl = apiUrl.replace(/\/$/, "");
  }

  load(locale = this.i18n.apiLocale()): Observable<Record<string, string>> {
    const canonicalLocale = locale === "en" ? "en-US" : locale;
    return this.http.get<LocalizationResponse>(`${this.apiUrl}/localization`, { params: { locale: canonicalLocale } }).pipe(
      tap(response => this.i18n.registerTranslations(canonicalLocale, response.values ?? {})),
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
