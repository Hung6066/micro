import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { Invoice, CreateInvoiceRequest, RecordPaymentRequest, InvoiceSearchParams } from '@core/models/invoice.model';
import { PagedResult } from '@core/models/paged-result.model';
import { environment } from '@env/environment';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private readonly baseUrl = `${environment.apiUrl}/invoices`;

  private http = inject(HttpClient);

  searchInvoices(params?: InvoiceSearchParams): Observable<PagedResult<Invoice>> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.searchTerm) httpParams = httpParams.set('q', params.searchTerm);
      if (params.patientId) httpParams = httpParams.set('patientId', params.patientId);
      if (params.statusCode) httpParams = httpParams.set('statusCode', params.statusCode);
      if (params.page) httpParams = httpParams.set('page', params.page.toString());
      if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    }
    return this.http.get<PagedResult<Invoice>>(`${this.baseUrl}/search`, { params: httpParams }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getInvoice(id: string): Observable<Invoice> {
    return this.http.get<Invoice>(`${this.baseUrl}/${id}`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  getInvoiceByNumber(invoiceNumber: string): Observable<Invoice> {
    return this.http.get<Invoice>(`${this.baseUrl}/number/${invoiceNumber}`).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  createInvoice(data: CreateInvoiceRequest): Observable<Invoice> {
    return this.http.post<Invoice>(`${this.baseUrl}/`, data).pipe(
      catchError(this.handleError),
    );
  }

  recordPayment(id: string, data: RecordPaymentRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/payments`, data).pipe(
      catchError(this.handleError),
    );
  }

  voidInvoice(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/void`, {}).pipe(
      catchError(this.handleError),
    );
  }

  getPatientInvoices(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<Invoice>> {
    const params = new HttpParams()
      .set('patientId', patientId)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<Invoice>>(`${this.baseUrl}/search`, { params }).pipe(
      retry(1),
      catchError(this.handleError),
    );
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    const errorMessage = error.error instanceof ErrorEvent
      ? `Client error: ${error.error.message}`
      : `Server error: ${error.status} - ${error.message}`;
    console.error('[BillingService]', errorMessage);
    return throwError(() => error);
  }
}
