CREATE TABLE "Invoices" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PatientId UUID NOT NULL,
    EncounterId UUID,
    InvoiceNumber VARCHAR(100) NOT NULL UNIQUE,
    InvoiceDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    DueDate TIMESTAMPTZ,
    Status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    SubTotal DECIMAL(18,2) NOT NULL DEFAULT 0,
    TaxAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    PaidAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    BalanceDue DECIMAL(18,2) NOT NULL DEFAULT 0,
    Notes TEXT,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_invoices_status CHECK (Status IN ('Draft', 'Submitted', 'PartiallyPaid', 'Paid', 'Cancelled', 'Overdue'))
);

CREATE TABLE "InvoiceLineItems" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    InvoiceId UUID NOT NULL REFERENCES "Invoices"(Id) ON DELETE CASCADE,
    Description VARCHAR(1000) NOT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    UnitPrice DECIMAL(18,2) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    ItemCode VARCHAR(50),
    ItemType VARCHAR(50),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_invoicelineitems_itemtype CHECK (ItemType IS NULL OR ItemType IN ('Service', 'Medication', 'Supply', 'Procedure', 'Lab', 'Consultation'))
);

CREATE TABLE "Payments" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    InvoiceId UUID NOT NULL REFERENCES "Invoices"(Id) ON DELETE CASCADE,
    PatientId UUID NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    PaymentDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    Method VARCHAR(50) NOT NULL,
    ReferenceNumber VARCHAR(200),
    Status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    Notes TEXT,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_payments_method CHECK (Method IN ('Cash', 'CreditCard', 'DebitCard', 'Insurance', 'BankTransfer', 'Cheque')),
    CONSTRAINT chk_payments_status CHECK (Status IN ('Pending', 'Completed', 'Failed', 'Refunded'))
);

CREATE TABLE "OutboxMessages" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Type VARCHAR(500) NOT NULL,
    Content JSONB NOT NULL,
    CorrelationId VARCHAR(200),
    CausationId VARCHAR(200),
    OccurredOn TIMESTAMPTZ NOT NULL DEFAULT now(),
    ProcessedOn TIMESTAMPTZ,
    Status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    Error VARCHAR(1000),
    RetryCount INT DEFAULT 0,
    LastRetryOn TIMESTAMPTZ,
    LockExpiresAt TIMESTAMPTZ
);

-- Backward-compatibility for existing deployments






