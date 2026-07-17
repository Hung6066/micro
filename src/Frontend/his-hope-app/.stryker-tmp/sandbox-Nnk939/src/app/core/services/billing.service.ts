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
import { Invoice, CreateInvoiceRequest, RecordPaymentRequest, InvoiceSearchParams } from '@core/models/invoice.model';
import { PagedResult } from '@core/models/paged-result.model';
import { environment } from '@env/environment';
@Injectable({
  providedIn: 'root'
})
export class BillingService {
  private readonly baseUrl = stryMutAct_9fa48("843") ? `` : (stryCov_9fa48("843"), `${environment.apiUrl}/invoices`);
  constructor(private http: HttpClient) {}
  searchInvoices(params?: InvoiceSearchParams): Observable<PagedResult<Invoice>> {
    if (stryMutAct_9fa48("844")) {
      {}
    } else {
      stryCov_9fa48("844");
      let httpParams = new HttpParams();
      if (stryMutAct_9fa48("846") ? false : stryMutAct_9fa48("845") ? true : (stryCov_9fa48("845", "846"), params)) {
        if (stryMutAct_9fa48("847")) {
          {}
        } else {
          stryCov_9fa48("847");
          if (stryMutAct_9fa48("849") ? false : stryMutAct_9fa48("848") ? true : (stryCov_9fa48("848", "849"), params.searchTerm)) httpParams = httpParams.set(stryMutAct_9fa48("850") ? "" : (stryCov_9fa48("850"), 'q'), params.searchTerm);
          if (stryMutAct_9fa48("852") ? false : stryMutAct_9fa48("851") ? true : (stryCov_9fa48("851", "852"), params.patientId)) httpParams = httpParams.set(stryMutAct_9fa48("853") ? "" : (stryCov_9fa48("853"), 'patientId'), params.patientId);
          if (stryMutAct_9fa48("855") ? false : stryMutAct_9fa48("854") ? true : (stryCov_9fa48("854", "855"), params.statusCode)) httpParams = httpParams.set(stryMutAct_9fa48("856") ? "" : (stryCov_9fa48("856"), 'statusCode'), params.statusCode);
          if (stryMutAct_9fa48("858") ? false : stryMutAct_9fa48("857") ? true : (stryCov_9fa48("857", "858"), params.page)) httpParams = httpParams.set(stryMutAct_9fa48("859") ? "" : (stryCov_9fa48("859"), 'page'), params.page.toString());
          if (stryMutAct_9fa48("861") ? false : stryMutAct_9fa48("860") ? true : (stryCov_9fa48("860", "861"), params.pageSize)) httpParams = httpParams.set(stryMutAct_9fa48("862") ? "" : (stryCov_9fa48("862"), 'pageSize'), params.pageSize.toString());
        }
      }
      return this.http.get<PagedResult<Invoice>>(stryMutAct_9fa48("863") ? `` : (stryCov_9fa48("863"), `${this.baseUrl}/search`), stryMutAct_9fa48("864") ? {} : (stryCov_9fa48("864"), {
        params: httpParams
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  getInvoice(id: string): Observable<Invoice> {
    if (stryMutAct_9fa48("865")) {
      {}
    } else {
      stryCov_9fa48("865");
      return this.http.get<Invoice>(stryMutAct_9fa48("866") ? `` : (stryCov_9fa48("866"), `${this.baseUrl}/${id}`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  getInvoiceByNumber(invoiceNumber: string): Observable<Invoice> {
    if (stryMutAct_9fa48("867")) {
      {}
    } else {
      stryCov_9fa48("867");
      return this.http.get<Invoice>(stryMutAct_9fa48("868") ? `` : (stryCov_9fa48("868"), `${this.baseUrl}/number/${invoiceNumber}`)).pipe(retry(1), catchError(this.handleError));
    }
  }
  createInvoice(data: CreateInvoiceRequest): Observable<Invoice> {
    if (stryMutAct_9fa48("869")) {
      {}
    } else {
      stryCov_9fa48("869");
      return this.http.post<Invoice>(stryMutAct_9fa48("870") ? `` : (stryCov_9fa48("870"), `${this.baseUrl}/`), data).pipe(catchError(this.handleError));
    }
  }
  recordPayment(id: string, data: RecordPaymentRequest): Observable<void> {
    if (stryMutAct_9fa48("871")) {
      {}
    } else {
      stryCov_9fa48("871");
      return this.http.post<void>(stryMutAct_9fa48("872") ? `` : (stryCov_9fa48("872"), `${this.baseUrl}/${id}/payments`), data).pipe(catchError(this.handleError));
    }
  }
  voidInvoice(id: string): Observable<void> {
    if (stryMutAct_9fa48("873")) {
      {}
    } else {
      stryCov_9fa48("873");
      return this.http.post<void>(stryMutAct_9fa48("874") ? `` : (stryCov_9fa48("874"), `${this.baseUrl}/${id}/void`), {}).pipe(catchError(this.handleError));
    }
  }
  getPatientInvoices(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<Invoice>> {
    if (stryMutAct_9fa48("875")) {
      {}
    } else {
      stryCov_9fa48("875");
      const params = new HttpParams().set(stryMutAct_9fa48("876") ? "" : (stryCov_9fa48("876"), 'patientId'), patientId).set(stryMutAct_9fa48("877") ? "" : (stryCov_9fa48("877"), 'page'), page.toString()).set(stryMutAct_9fa48("878") ? "" : (stryCov_9fa48("878"), 'pageSize'), pageSize.toString());
      return this.http.get<PagedResult<Invoice>>(stryMutAct_9fa48("879") ? `` : (stryCov_9fa48("879"), `${this.baseUrl}/search`), stryMutAct_9fa48("880") ? {} : (stryCov_9fa48("880"), {
        params
      })).pipe(retry(1), catchError(this.handleError));
    }
  }
  private handleError(error: HttpErrorResponse): Observable<never> {
    if (stryMutAct_9fa48("881")) {
      {}
    } else {
      stryCov_9fa48("881");
      const errorMessage = error.error instanceof ErrorEvent ? stryMutAct_9fa48("882") ? `` : (stryCov_9fa48("882"), `Client error: ${error.error.message}`) : stryMutAct_9fa48("883") ? `` : (stryCov_9fa48("883"), `Server error: ${error.status} - ${error.message}`);
      console.error(stryMutAct_9fa48("884") ? "" : (stryCov_9fa48("884"), '[BillingService]'), errorMessage);
      return throwError(stryMutAct_9fa48("885") ? () => undefined : (stryCov_9fa48("885"), () => error));
    }
  }
}