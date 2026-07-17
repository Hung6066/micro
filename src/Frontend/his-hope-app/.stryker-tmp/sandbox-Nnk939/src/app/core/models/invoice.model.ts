// @ts-nocheck
export enum InvoiceStatus {
  Draft = 'draft',
  Issued = 'issued',
  PartiallyPaid = 'partially_paid',
  Paid = 'paid',
  Overdue = 'overdue',
  Cancelled = 'cancelled',
  Voided = 'voided',
}

export enum PaymentMethod {
  Cash = 'cash',
  CreditCard = 'credit_card',
  DebitCard = 'debit_card',
  BankTransfer = 'bank_transfer',
  Insurance = 'insurance',
  MobilePayment = 'mobile_payment',
  Other = 'other',
}

export interface InvoiceLineItem {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  amount: number;
  itemCode: string;
  itemTypeCode: string;
  itemTypeName: string;
}

export interface Payment {
  id: string;
  amount: number;
  paymentDate: string;
  methodCode: string;
  methodName: string;
  referenceNumber?: string;
  statusCode: string;
  statusName: string;
}

export interface Invoice {
  id: string;
  patientId: string;
  encounterId?: string;
  invoiceNumber: string;
  invoiceDate: string;
  dueDate?: string;
  statusCode: string;
  statusName: string;
  notes?: string;
  subTotal: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  paidAmount: number;
  balanceDue: number;
  createdAt: string;
  updatedAt?: string;
  lineItems: InvoiceLineItem[];
  payments: Payment[];
  // Extended fields for richer UI
  patientName?: string;
}

export interface CreateInvoiceRequest {
  patientId: string;
  encounterId?: string;
  notes?: string;
  lineItems: {
    description: string;
    quantity: number;
    unitPrice: number;
    itemCode: string;
    itemTypeCode: string;
  }[];
}

export interface RecordPaymentRequest {
  amount: number;
  paymentDate: string;
  methodCode: string;
  referenceNumber?: string;
}

export interface InvoiceSearchParams {
  searchTerm?: string;
  patientId?: string;
  statusCode?: string;
  page?: number;
  pageSize?: number;
}
