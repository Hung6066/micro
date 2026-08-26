export type OperationStatus = "pending" | "synced" | "failed" | "conflict";

export interface QueuedOperation {
  id: string;
  operationId: string;
  tenantKey: string;
  subjectId: string;
  endpoint: string;
  payload: unknown;
  expectedVersion?: string;
  createdAt: string;
  status: OperationStatus;
  error?: string;
}

export interface OperationCommand {
  tenantKey: string;
  subjectId: string;
  endpoint: string;
  payload: unknown;
  expectedVersion?: string;
}

export type OperationTransportResult =
  | { kind: "synced" }
  | { kind: "pending"; message: string }
  | { kind: "failed"; message: string }
  | { kind: "conflict"; statusCode: 409; message: string };

export type OperationTransport = (
  operation: QueuedOperation,
) => Promise<OperationTransportResult>;
