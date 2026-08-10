CREATE TABLE billingdb.Invoices (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PatientId UUID NOT NULL,
    EncounterId UUID,
    InvoiceNumber STRING(100) NOT NULL UNIQUE,
    InvoiceDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    DueDate TIMESTAMPTZ,
    Status STRING(50) NOT NULL DEFAULT 'Draft',
    SubTotal DECIMAL(18,2) NOT NULL DEFAULT 0,
    TaxAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    PaidAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    BalanceDue DECIMAL(18,2) NOT NULL DEFAULT 0,
    Notes STRING,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_invoices_status CHECK (Status IN ('Draft', 'Submitted', 'PartiallyPaid', 'Paid', 'Cancelled', 'Overdue')),
    INDEX idx_invoices_patient (PatientId),
    INDEX idx_invoices_status (Status),
    INDEX idx_invoices_invoicedate (InvoiceDate),
    INDEX idx_invoices_duedate (DueDate)
);

CREATE TABLE billingdb.InvoiceLineItems (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    InvoiceId UUID NOT NULL REFERENCES billingdb.Invoices(Id) ON DELETE CASCADE,
    Description STRING(1000) NOT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    UnitPrice DECIMAL(18,2) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    ItemCode STRING(50),
    ItemType STRING(50),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_invoicelineitems_itemtype CHECK (ItemType IS NULL OR ItemType IN ('Service', 'Medication', 'Supply', 'Procedure', 'Lab', 'Consultation')),
    INDEX idx_invoicelineitems_invoice (InvoiceId)
);

CREATE TABLE billingdb.Payments (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    InvoiceId UUID NOT NULL REFERENCES billingdb.Invoices(Id) ON DELETE CASCADE,
    PatientId UUID NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    PaymentDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    Method STRING(50) NOT NULL,
    ReferenceNumber STRING(200),
    Status STRING(50) NOT NULL DEFAULT 'Pending',
    Notes STRING,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_payments_method CHECK (Method IN ('Cash', 'CreditCard', 'DebitCard', 'Insurance', 'BankTransfer', 'Cheque')),
    CONSTRAINT chk_payments_status CHECK (Status IN ('Pending', 'Completed', 'Failed', 'Refunded')),
    INDEX idx_payments_invoice (InvoiceId),
    INDEX idx_payments_patient (PatientId),
    INDEX idx_payments_paymentdate (PaymentDate)
);

CREATE TABLE billingdb.OutboxMessages (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Type STRING(500) NOT NULL,
    Content JSONB NOT NULL,
    CorrelationId STRING(200),
    CausationId STRING(200),
    OccurredOn TIMESTAMPTZ NOT NULL DEFAULT now(),
    ProcessedOn TIMESTAMPTZ,
    Status STRING(50) NOT NULL DEFAULT 'Pending',
    Error STRING(1000),
    RetryCount INT DEFAULT 0,
    LastRetryOn TIMESTAMPTZ,
    LockExpiresAt TIMESTAMPTZ,
    INDEX idx_outbox_status_occurred (Status, OccurredOn)
);

-- Backward-compatibility for existing deployments
ALTER TABLE billingdb.OutboxMessages ADD COLUMN IF NOT EXISTS CorrelationId STRING(200);
ALTER TABLE billingdb.OutboxMessages ADD COLUMN IF NOT EXISTS CausationId STRING(200);
ALTER TABLE billingdb.OutboxMessages ADD COLUMN IF NOT EXISTS OccurredOn TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE billingdb.OutboxMessages ADD COLUMN IF NOT EXISTS ProcessedOn TIMESTAMPTZ;
ALTER TABLE billingdb.OutboxMessages ADD COLUMN IF NOT EXISTS Status STRING(50) NOT NULL DEFAULT 'Pending';
ALTER TABLE billingdb.OutboxMessages ADD COLUMN IF NOT EXISTS LastRetryOn TIMESTAMPTZ;
ALTER TABLE billingdb.OutboxMessages ADD COLUMN IF NOT EXISTS LockExpiresAt TIMESTAMPTZ;

