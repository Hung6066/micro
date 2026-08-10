import { Injectable } from '@angular/core';
import { Observable, of, BehaviorSubject } from 'rxjs';
import { delay } from 'rxjs/operators';
import { Invoice, CreateInvoiceRequest, RecordPaymentRequest, InvoiceSearchParams, Payment, InvoiceLineItem } from '@core/models/invoice.model';
import { PagedResult } from '@core/models/paged-result.model';
import { mockInvoices, mockPatients } from './mock-data';

@Injectable({ providedIn: 'root' })
export class MockBillingService {
  private invoicesSubject = new BehaviorSubject<Invoice[]>([...mockInvoices]);

  private delayMs(): number {
    return 300 + Math.floor(Math.random() * 200);
  }

  searchInvoices(params?: InvoiceSearchParams): Observable<PagedResult<Invoice>> {
    let filtered = this.invoicesSubject.value;
    if (params?.searchTerm) {
      const q = params.searchTerm.toLowerCase();
      filtered = filtered.filter(
        (inv) =>
          inv.invoiceNumber.toLowerCase().includes(q) ||
          (inv.patientName && inv.patientName.toLowerCase().includes(q)),
      );
    }
    if (params?.patientId) {
      filtered = filtered.filter((inv) => inv.patientId === params.patientId);
    }
    if (params?.statusCode) {
      filtered = filtered.filter((inv) => inv.statusCode === params.statusCode);
    }
    const page = params?.page || 1;
    const pageSize = params?.pageSize || 20;
    const total = filtered.length;
    const start = (page - 1) * pageSize;
    const result: PagedResult<Invoice> = {
      items: filtered.slice(start, start + pageSize),
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
    return of(result).pipe(delay(this.delayMs()));
  }

  getInvoice(id: string): Observable<Invoice> {
    const inv = this.invoicesSubject.value.find((i) => i.id === id);
    return of(inv!).pipe(delay(this.delayMs()));
  }

  getInvoiceByNumber(invoiceNumber: string): Observable<Invoice> {
    const inv = this.invoicesSubject.value.find((i) => i.invoiceNumber === invoiceNumber);
    return of(inv!).pipe(delay(this.delayMs()));
  }

  createInvoice(data: CreateInvoiceRequest): Observable<Invoice> {
    const patient = mockPatients.find((p) => p.id === data.patientId);
    const now = new Date();
    const invNum = `HD-${now.getFullYear()}-${String(this.invoicesSubject.value.length + 1).padStart(5, '0')}`;
    const lineItems: InvoiceLineItem[] = data.lineItems.map((li, idx) => ({
      id: `li-${String(this.invoicesSubject.value.length * 10 + idx + 1).padStart(3, '0')}`,
      description: li.description,
      quantity: li.quantity,
      unitPrice: li.unitPrice,
      amount: li.quantity * li.unitPrice,
      itemCode: li.itemCode,
      itemTypeCode: li.itemTypeCode,
      itemTypeName:
        li.itemTypeCode === 'exam'
          ? 'Khám bệnh'
          : li.itemTypeCode === 'lab'
            ? 'Xét nghiệm'
            : li.itemTypeCode === 'medication'
              ? 'Thuốc'
              : li.itemTypeCode === 'imaging'
                ? 'Chẩn đoán hình ảnh'
                : li.itemTypeCode === 'supply'
                  ? 'Vật tư'
                  : 'Khác',
    }));
    const subTotal = lineItems.reduce((sum, li) => sum + li.amount, 0);
    const taxAmount = Math.round(subTotal * 0.1);
    const totalAmount = subTotal + taxAmount;
    const newInvoice: Invoice = {
      id: `inv-${String(this.invoicesSubject.value.length + 1).padStart(3, '0')}`,
      patientId: data.patientId,
      encounterId: data.encounterId,
      invoiceNumber: invNum,
      invoiceDate: now.toISOString(),
      statusCode: 'issued',
      statusName: 'Đã phát hành',
      notes: data.notes,
      subTotal,
      taxAmount,
      discountAmount: 0,
      totalAmount,
      paidAmount: 0,
      balanceDue: totalAmount,
      createdAt: now.toISOString(),
      lineItems,
      payments: [],
      patientName: patient?.fullName,
    };
    this.invoicesSubject.next([...this.invoicesSubject.value, newInvoice]);
    return of(newInvoice).pipe(delay(this.delayMs()));
  }

  recordPayment(id: string, data: RecordPaymentRequest): Observable<void> {
    const invoices = this.invoicesSubject.value;
    const index = invoices.findIndex((inv) => inv.id === id);
    if (index === -1) {
      return of(undefined).pipe(delay(this.delayMs()));
    }
    const now = new Date().toISOString();
    const newPayment: Payment = {
      id: `pmt-${String(invoices[index].payments.length + 1).padStart(3, '0')}`,
      amount: data.amount,
      paymentDate: data.paymentDate || now,
      methodCode: data.methodCode,
      methodName:
        data.methodCode === 'cash'
          ? 'Tiền mặt'
          : data.methodCode === 'bank_transfer'
            ? 'Chuyển khoản'
            : data.methodCode === 'credit_card'
              ? 'Thẻ tín dụng'
              : data.methodCode === 'insurance'
                ? 'Bảo hiểm'
                : 'Khác',
      referenceNumber: data.referenceNumber,
      statusCode: 'completed',
      statusName: 'Hoàn thành',
    };
    const paidAmount = invoices[index].paidAmount + data.amount;
    const balanceDue = invoices[index].totalAmount - paidAmount;
    let statusCode = invoices[index].statusCode;
    let statusName = invoices[index].statusName;
    if (balanceDue <= 0) {
      statusCode = 'paid';
      statusName = 'Đã thanh toán';
    } else if (paidAmount > 0) {
      statusCode = 'partially_paid';
      statusName = 'Đã thanh toán một phần';
    }
    invoices[index] = {
      ...invoices[index],
      paidAmount,
      balanceDue,
      statusCode,
      statusName,
      payments: [...invoices[index].payments, newPayment],
      updatedAt: now,
    };
    this.invoicesSubject.next([...invoices]);
    return of(undefined).pipe(delay(this.delayMs()));
  }

  voidInvoice(id: string): Observable<void> {
    const invoices = this.invoicesSubject.value;
    const index = invoices.findIndex((inv) => inv.id === id);
    if (index !== -1) {
      invoices[index] = {
        ...invoices[index],
        statusCode: 'voided',
        statusName: 'Đã hủy',
        updatedAt: new Date().toISOString(),
      };
      this.invoicesSubject.next([...invoices]);
    }
    return of(undefined).pipe(delay(this.delayMs()));
  }

  getPatientInvoices(patientId: string, page = 1, pageSize = 20): Observable<PagedResult<Invoice>> {
    const filtered = this.invoicesSubject.value.filter((inv) => inv.patientId === patientId);
    const total = filtered.length;
    const start = (page - 1) * pageSize;
    const result: PagedResult<Invoice> = {
      items: filtered.slice(start, start + pageSize),
      totalCount: total,
      page,
      pageSize,
      hasNextPage: start + pageSize < total,
      hasPreviousPage: page > 1,
    };
    return of(result).pipe(delay(this.delayMs()));
  }

  /** Extra mock method: returns count of invoices with outstanding balance */
  getOutstandingCount(): Observable<number> {
    const count = this.invoicesSubject.value.filter(
      (inv) => inv.statusCode !== 'paid' && inv.statusCode !== 'voided' && inv.balanceDue > 0,
    ).length;
    return of(count).pipe(delay(this.delayMs()));
  }
}
