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
import { LabOrder, CreateLabOrderRequest, RecordLabResultRequest, LabOrderSearchParams } from '@core/models/lab-order.model';
import { PagedResult } from '@core/models/paged-result.model';
import { environment } from '@env/environment';
@Injectable({
  providedIn: 'root'
})
export class LabService {
  private readonly baseUrl = stryMutAct_9fa48("1325") ? `` : (stryCov_9fa48("1325"), `${environment.apiUrl}/lab-orders`);
  constructor(private http: HttpClient) {}
  searchLabOrders(params?: LabOrderSearchParams): Observable<PagedResult<LabOrder>> {
    if (stryMutAct_9fa48("1326")) {
      {}
    } else {
      stryCov_9fa48("1326");
      let httpParams = new HttpParams();
      if (stryMutAct_9fa48("1328") ? false : stryMutAct_9fa48("1327") ? true : (stryCov_9fa48("1327", "1328"), params)) {
        if (stryMutAct_9fa48("1329")) {
          {}
        } else {
          stryCov_9fa48("1329");
          if (stryMutAct_9fa48("1331") ? false : stryMutAct_9fa48("1330") ? true : (stryCov_9fa48("1330", "1331"), params.searchTerm)) httpParams = httpParams.set(stryMutAct_9fa48("1332") ? "" : (stryCov_9fa48("1332"), 'q'), params.searchTerm);
          if (stryMutAct_9fa48("1334") ? false : stryMutAct_9fa48("1333") ? true : (stryCov_9fa48("1333", "1334"), params.patientId)) httpParams = httpParams.set(stryMutAct_9fa48("1335") ? "" : (stryCov_9fa48("1335"), 'patientId'), params.patientId);
          if (stryMutAct_9fa48("1337") ? false : stryMutAct_9fa48("1336") ? true : (stryCov_9fa48("1336", "1337"), params.statusCode)) httpParams = httpParams.set(stryMutAct_9fa48("1338") ? "" : (stryCov_9fa48("1338"), 'statusCode'), params.statusCode);
          if (stryMutAct_9fa48("1340") ? false : stryMutAct_9fa48("1339") ? true : (stryCov_9fa48("1339", "1340"), params.page)) httpParams = httpParams.set(stryMutAct_9fa48("1341") ? "" : (stryCov_9fa48("1341"), 'page'), params.page.toString());
          if (stryMutAct_9fa48("1343") ? false : stryMutAct_9fa48("1342") ? true : (stryCov_9fa48("1342", "1343"), params.pageSize)) httpParams = httpParams.set(stryMutAct_9fa48("1344") ? "" : (stryCov_9fa48("1344"), 'pageSize'), params.pageSize.toString());
        }
      }
      return this.http.get<PagedResult<LabOrder>>(stryMutAct_9fa48("1345") ? `` : (stryCov_9fa48("1345"), `${this.baseUrl}/search`), stryMutAct_9fa48("1346") ? {} : (stryCov_9fa48("1346"), {
        params: httpParams
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getLabOrder(id: string): Observable<LabOrder> {
    if (stryMutAct_9fa48("1347")) {
      {}
    } else {
      stryCov_9fa48("1347");
      return this.http.get<LabOrder>(stryMutAct_9fa48("1348") ? `` : (stryCov_9fa48("1348"), `${this.baseUrl}/${id}`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  createLabOrder(data: CreateLabOrderRequest): Observable<LabOrder> {
    if (stryMutAct_9fa48("1349")) {
      {}
    } else {
      stryCov_9fa48("1349");
      return this.http.post<LabOrder>(stryMutAct_9fa48("1350") ? `` : (stryCov_9fa48("1350"), `${this.baseUrl}/`), data).pipe(catchError(this.handleError));
    }
  }
  submitLabOrder(id: string): Observable<void> {
    if (stryMutAct_9fa48("1351")) {
      {}
    } else {
      stryCov_9fa48("1351");
      return this.http.post<void>(stryMutAct_9fa48("1352") ? `` : (stryCov_9fa48("1352"), `${this.baseUrl}/${id}/submit`), {}).pipe(catchError(this.handleError));
    }
  }
  collectSpecimen(id: string): Observable<void> {
    if (stryMutAct_9fa48("1353")) {
      {}
    } else {
      stryCov_9fa48("1353");
      return this.http.post<void>(stryMutAct_9fa48("1354") ? `` : (stryCov_9fa48("1354"), `${this.baseUrl}/${id}/collect`), {}).pipe(catchError(this.handleError));
    }
  }
  recordResult(id: string, data: RecordLabResultRequest): Observable<void> {
    if (stryMutAct_9fa48("1355")) {
      {}
    } else {
      stryCov_9fa48("1355");
      return this.http.post<void>(stryMutAct_9fa48("1356") ? `` : (stryCov_9fa48("1356"), `${this.baseUrl}/${id}/result`), data).pipe(catchError(this.handleError));
    }
  }
  cancelLabOrder(id: string): Observable<void> {
    if (stryMutAct_9fa48("1357")) {
      {}
    } else {
      stryCov_9fa48("1357");
      return this.http.post<void>(stryMutAct_9fa48("1358") ? `` : (stryCov_9fa48("1358"), `${this.baseUrl}/${id}/cancel`), {}).pipe(catchError(this.handleError));
    }
  }
  getPatientLabOrders(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<LabOrder>> {
    if (stryMutAct_9fa48("1359")) {
      {}
    } else {
      stryCov_9fa48("1359");
      const params = new HttpParams().set(stryMutAct_9fa48("1360") ? "" : (stryCov_9fa48("1360"), 'patientId'), patientId).set(stryMutAct_9fa48("1361") ? "" : (stryCov_9fa48("1361"), 'page'), page.toString()).set(stryMutAct_9fa48("1362") ? "" : (stryCov_9fa48("1362"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<LabOrder>>(stryMutAct_9fa48("1363") ? `` : (stryCov_9fa48("1363"), `${this.baseUrl}/search`), stryMutAct_9fa48("1364") ? {} : (stryCov_9fa48("1364"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  private handleError(error: HttpErrorResponse): Observable<never> {
    if (stryMutAct_9fa48("1365")) {
      {}
    } else {
      stryCov_9fa48("1365");
      const errorMessage = error.error instanceof ErrorEvent ? stryMutAct_9fa48("1366") ? `` : (stryCov_9fa48("1366"), `Client error: ${error.error.message}`) : stryMutAct_9fa48("1367") ? `` : (stryCov_9fa48("1367"), `Server error: ${error.status} - ${error.message}`);
      console.error(stryMutAct_9fa48("1368") ? "" : (stryCov_9fa48("1368"), '[LabService]'), errorMessage);
      return throwError(stryMutAct_9fa48("1369") ? () => undefined : (stryCov_9fa48("1369"), () => error));
    }
  }
}