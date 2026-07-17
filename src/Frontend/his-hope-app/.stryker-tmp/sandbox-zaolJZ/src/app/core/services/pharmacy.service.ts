// @ts-nocheck
function stryNS_9fa48() {
  var g = typeof globalThis === 'object' && globalThis && globalThis.Math === Math && globalThis || new Function("return this")();
  var ns = g.__stryker__ || (g.__stryker__ = {});
  if (ns.activeMutant === undefined && g.process && g.process.env && g.process.env.__STRYKER_ACTIVE_MUTANT__) {
    ns.activeMutant = g.process.env.__STRYKER_ACTIVE_MUTANT__;
  }
  function retrieveNS() {
    return ns;
  }
  stryNS_9fa48 = retrieveNS;
  return retrieveNS();
}
stryNS_9fa48();
function stryCov_9fa48() {
  var ns = stryNS_9fa48();
  var cov = ns.mutantCoverage || (ns.mutantCoverage = {
    static: {},
    perTest: {}
  });
  function cover() {
    var c = cov.static;
    if (ns.currentTestId) {
      c = cov.perTest[ns.currentTestId] = cov.perTest[ns.currentTestId] || {};
    }
    var a = arguments;
    for (var i = 0; i < a.length; i++) {
      c[a[i]] = (c[a[i]] || 0) + 1;
    }
  }
  stryCov_9fa48 = cover;
  cover.apply(null, arguments);
}
function stryMutAct_9fa48(id) {
  var ns = stryNS_9fa48();
  function isActive(id) {
    if (ns.activeMutant === id) {
      if (ns.hitCount !== void 0 && ++ns.hitCount > ns.hitLimit) {
        throw new Error('Stryker: Hit count limit reached (' + ns.hitCount + ')');
      }
      return true;
    }
    return false;
  }
  stryMutAct_9fa48 = isActive;
  return isActive(id);
}
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { Medication, CreateMedicationRequest, UpdateMedicationRequest, MedicationSearchParams } from '@core/models/medication.model';
import { Prescription, CreatePrescriptionRequest, PrescriptionSearchParams } from '@core/models/prescription.model';
import { PagedResult } from '@core/models/paged-result.model';
import { environment } from '@env/environment';
@Injectable({
  providedIn: 'root'
})
export class PharmacyService {
  private readonly medBaseUrl = stryMutAct_9fa48("1799") ? `` : (stryCov_9fa48("1799"), `${environment.apiUrl}/medications`);
  private readonly rxBaseUrl = stryMutAct_9fa48("1800") ? `` : (stryCov_9fa48("1800"), `${environment.apiUrl}/prescriptions`);
  constructor(private http: HttpClient) {}

  // ─── Medication endpoints ───────────────────────────────────────────────

  searchMedications(params?: MedicationSearchParams): Observable<PagedResult<Medication>> {
    if (stryMutAct_9fa48("1801")) {
      {}
    } else {
      stryCov_9fa48("1801");
      let httpParams = new HttpParams();
      if (stryMutAct_9fa48("1803") ? false : stryMutAct_9fa48("1802") ? true : (stryCov_9fa48("1802", "1803"), params)) {
        if (stryMutAct_9fa48("1804")) {
          {}
        } else {
          stryCov_9fa48("1804");
          if (stryMutAct_9fa48("1806") ? false : stryMutAct_9fa48("1805") ? true : (stryCov_9fa48("1805", "1806"), params.searchTerm)) httpParams = httpParams.set(stryMutAct_9fa48("1807") ? "" : (stryCov_9fa48("1807"), 'q'), params.searchTerm);
          if (stryMutAct_9fa48("1809") ? false : stryMutAct_9fa48("1808") ? true : (stryCov_9fa48("1808", "1809"), params.page)) httpParams = httpParams.set(stryMutAct_9fa48("1810") ? "" : (stryCov_9fa48("1810"), 'page'), params.page.toString());
          if (stryMutAct_9fa48("1812") ? false : stryMutAct_9fa48("1811") ? true : (stryCov_9fa48("1811", "1812"), params.pageSize)) httpParams = httpParams.set(stryMutAct_9fa48("1813") ? "" : (stryCov_9fa48("1813"), 'pageSize'), params.pageSize.toString());
        }
      }
      return this.http.get<PagedResult<Medication>>(stryMutAct_9fa48("1814") ? `` : (stryCov_9fa48("1814"), `${this.medBaseUrl}/search`), stryMutAct_9fa48("1815") ? {} : (stryCov_9fa48("1815"), {
        params: httpParams
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getMedication(id: string): Observable<Medication> {
    if (stryMutAct_9fa48("1816")) {
      {}
    } else {
      stryCov_9fa48("1816");
      return this.http.get<Medication>(stryMutAct_9fa48("1817") ? `` : (stryCov_9fa48("1817"), `${this.medBaseUrl}/${id}`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  createMedication(data: CreateMedicationRequest): Observable<Medication> {
    if (stryMutAct_9fa48("1818")) {
      {}
    } else {
      stryCov_9fa48("1818");
      return this.http.post<Medication>(stryMutAct_9fa48("1819") ? `` : (stryCov_9fa48("1819"), `${this.medBaseUrl}/`), data).pipe(catchError(this.handleError));
    }
  }
  updateMedication(id: string, data: UpdateMedicationRequest): Observable<Medication> {
    if (stryMutAct_9fa48("1820")) {
      {}
    } else {
      stryCov_9fa48("1820");
      return this.http.put<Medication>(stryMutAct_9fa48("1821") ? `` : (stryCov_9fa48("1821"), `${this.medBaseUrl}/${id}`), data).pipe(catchError(this.handleError));
    }
  }
  deactivateMedication(id: string): Observable<void> {
    if (stryMutAct_9fa48("1822")) {
      {}
    } else {
      stryCov_9fa48("1822");
      return this.http.patch<void>(stryMutAct_9fa48("1823") ? `` : (stryCov_9fa48("1823"), `${this.medBaseUrl}/${id}/deactivate`), {}).pipe(catchError(this.handleError));
    }
  }

  // ─── Prescription endpoints ─────────────────────────────────────────────

  searchPrescriptions(params?: PrescriptionSearchParams): Observable<PagedResult<Prescription>> {
    if (stryMutAct_9fa48("1824")) {
      {}
    } else {
      stryCov_9fa48("1824");
      let httpParams = new HttpParams();
      if (stryMutAct_9fa48("1826") ? false : stryMutAct_9fa48("1825") ? true : (stryCov_9fa48("1825", "1826"), params)) {
        if (stryMutAct_9fa48("1827")) {
          {}
        } else {
          stryCov_9fa48("1827");
          if (stryMutAct_9fa48("1829") ? false : stryMutAct_9fa48("1828") ? true : (stryCov_9fa48("1828", "1829"), params.searchTerm)) httpParams = httpParams.set(stryMutAct_9fa48("1830") ? "" : (stryCov_9fa48("1830"), 'q'), params.searchTerm);
          if (stryMutAct_9fa48("1832") ? false : stryMutAct_9fa48("1831") ? true : (stryCov_9fa48("1831", "1832"), params.patientId)) httpParams = httpParams.set(stryMutAct_9fa48("1833") ? "" : (stryCov_9fa48("1833"), 'patientId'), params.patientId);
          if (stryMutAct_9fa48("1835") ? false : stryMutAct_9fa48("1834") ? true : (stryCov_9fa48("1834", "1835"), params.statusCode)) httpParams = httpParams.set(stryMutAct_9fa48("1836") ? "" : (stryCov_9fa48("1836"), 'statusCode'), params.statusCode);
          if (stryMutAct_9fa48("1838") ? false : stryMutAct_9fa48("1837") ? true : (stryCov_9fa48("1837", "1838"), params.page)) httpParams = httpParams.set(stryMutAct_9fa48("1839") ? "" : (stryCov_9fa48("1839"), 'page'), params.page.toString());
          if (stryMutAct_9fa48("1841") ? false : stryMutAct_9fa48("1840") ? true : (stryCov_9fa48("1840", "1841"), params.pageSize)) httpParams = httpParams.set(stryMutAct_9fa48("1842") ? "" : (stryCov_9fa48("1842"), 'pageSize'), params.pageSize.toString());
        }
      }
      return this.http.get<PagedResult<Prescription>>(stryMutAct_9fa48("1843") ? `` : (stryCov_9fa48("1843"), `${this.rxBaseUrl}/search`), stryMutAct_9fa48("1844") ? {} : (stryCov_9fa48("1844"), {
        params: httpParams
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getPrescription(id: string): Observable<Prescription> {
    if (stryMutAct_9fa48("1845")) {
      {}
    } else {
      stryCov_9fa48("1845");
      return this.http.get<Prescription>(stryMutAct_9fa48("1846") ? `` : (stryCov_9fa48("1846"), `${this.rxBaseUrl}/${id}`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  createPrescription(data: CreatePrescriptionRequest): Observable<Prescription> {
    if (stryMutAct_9fa48("1847")) {
      {}
    } else {
      stryCov_9fa48("1847");
      return this.http.post<Prescription>(stryMutAct_9fa48("1848") ? `` : (stryCov_9fa48("1848"), `${this.rxBaseUrl}/`), data).pipe(catchError(this.handleError));
    }
  }
  fillPrescription(id: string): Observable<void> {
    if (stryMutAct_9fa48("1849")) {
      {}
    } else {
      stryCov_9fa48("1849");
      return this.http.post<void>(stryMutAct_9fa48("1850") ? `` : (stryCov_9fa48("1850"), `${this.rxBaseUrl}/${id}/fill`), {}).pipe(catchError(this.handleError));
    }
  }
  cancelPrescription(id: string): Observable<void> {
    if (stryMutAct_9fa48("1851")) {
      {}
    } else {
      stryCov_9fa48("1851");
      return this.http.post<void>(stryMutAct_9fa48("1852") ? `` : (stryCov_9fa48("1852"), `${this.rxBaseUrl}/${id}/cancel`), {}).pipe(catchError(this.handleError));
    }
  }
  getPatientPrescriptions(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<Prescription>> {
    if (stryMutAct_9fa48("1853")) {
      {}
    } else {
      stryCov_9fa48("1853");
      const params = new HttpParams().set(stryMutAct_9fa48("1854") ? "" : (stryCov_9fa48("1854"), 'patientId'), patientId).set(stryMutAct_9fa48("1855") ? "" : (stryCov_9fa48("1855"), 'page'), page.toString()).set(stryMutAct_9fa48("1856") ? "" : (stryCov_9fa48("1856"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<Prescription>>(stryMutAct_9fa48("1857") ? `` : (stryCov_9fa48("1857"), `${this.rxBaseUrl}/search`), stryMutAct_9fa48("1858") ? {} : (stryCov_9fa48("1858"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }

  // ─── Error handler ──────────────────────────────────────────────────────

  private handleError(error: HttpErrorResponse): Observable<never> {
    if (stryMutAct_9fa48("1859")) {
      {}
    } else {
      stryCov_9fa48("1859");
      const errorMessage = error.error instanceof ErrorEvent ? stryMutAct_9fa48("1860") ? `` : (stryCov_9fa48("1860"), `Client error: ${error.error.message}`) : stryMutAct_9fa48("1861") ? `` : (stryCov_9fa48("1861"), `Server error: ${error.status} - ${error.message}`);
      console.error(stryMutAct_9fa48("1862") ? "" : (stryCov_9fa48("1862"), '[PharmacyService]'), errorMessage);
      return throwError(stryMutAct_9fa48("1863") ? () => undefined : (stryCov_9fa48("1863"), () => error));
    }
  }
}