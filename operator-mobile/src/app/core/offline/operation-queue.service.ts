import { Injectable, Optional } from "@angular/core";
import { MobileSecureOidcStorage } from "../secure-oidc-storage.service";
import {
  type OperationCommand,
  type OperationStatus,
  type OperationTransport,
  type QueuedOperation,
} from "./operation-queue.models";

@Injectable({ providedIn: "root" })
export class OperationQueueService {
  private static readonly MAX_ENTRIES = 100;
  private static readonly DEAD_LETTER_ATTEMPTS = 5;
  private readonly queue: QueuedOperation[] = [];

  // The no-argument construction path keeps the queue deterministic in unit tests.
  // eslint-disable-next-line @angular-eslint/prefer-inject
  constructor(@Optional() private readonly secureStorage?: MobileSecureOidcStorage) {
    const stored = secureStorage?.read("operator-mobile.operation-queue.v1");
    if (stored) {
      try {
        this.queue.push(...(JSON.parse(stored) as QueuedOperation[]));
      } catch {
        secureStorage?.remove("operator-mobile.operation-queue.v1");
      }
    }
  }

  async submit(
    command: OperationCommand,
    transport: OperationTransport,
  ): Promise<QueuedOperation> {
    const operation = await this.enqueue(command);
    try {
      const result = await transport(operation);
      this.applyResult(operation, result.kind, "message" in result ? result.message : undefined);
    } catch {
      operation.status = "pending";
    }
    this.persist();
    return operation;
  }

  async enqueue(command: OperationCommand): Promise<QueuedOperation> {
    const operation: QueuedOperation = {
      ...command,
      id: crypto.randomUUID(),
      operationId: crypto.randomUUID(),
      createdAt: new Date().toISOString(),
      attemptCount: 0,
      status: "pending",
    };
    this.queue.push(operation);
    this.persist();
    return operation;
  }

  async sync(transport: OperationTransport): Promise<void> {
    for (const operation of this.queue.filter((item) => item.status === "pending")) {
      operation.attemptCount = (operation.attemptCount ?? 0) + 1;
      operation.lastAttemptAt = new Date().toISOString();
      try {
        const result = await transport(operation);
        const message = "message" in result ? result.message : undefined;
        const terminalStatus = result.kind === "pending" && (operation.attemptCount ?? 0) >= OperationQueueService.DEAD_LETTER_ATTEMPTS ? "failed" : result.kind;
        this.applyResult(operation, terminalStatus, message);
      } catch {
        operation.status = (operation.attemptCount ?? 0) >= OperationQueueService.DEAD_LETTER_ATTEMPTS ? "failed" : "pending";
      }
    }
    this.persist();
  }

  entries(): Promise<QueuedOperation[]> {
    return Promise.resolve(this.queue.map((entry) => ({ ...entry })));
  }

  async retainScope(tenantKey: string, subjectId: string): Promise<void> {
    for (let index = this.queue.length - 1; index >= 0; index -= 1) {
      const operation = this.queue[index];
      if (operation.tenantKey !== tenantKey || operation.subjectId !== subjectId) {
        this.queue.splice(index, 1);
      }
    }
    this.persist();
  }

  async discard(id: string): Promise<void> {
    const index = this.queue.findIndex((entry) => entry.id === id);
    if (index >= 0) this.queue.splice(index, 1);
    this.persist();
  }

  async retry(id: string): Promise<void> {
    const operation = this.queue.find((entry) => entry.id === id);
    if (!operation || (operation.status !== "failed" && operation.status !== "conflict")) return;
    operation.status = "pending";
    operation.error = undefined;
    this.persist();
  }

  async clear(): Promise<void> {
    this.queue.splice(0, this.queue.length);
    this.persist();
  }

  private applyResult(operation: QueuedOperation, status: OperationStatus, message?: string): void {
    operation.status = status;
    operation.error = message;
  }

  private persist(): void {
    this.pruneTerminalEntries();
    this.secureStorage?.write("operator-mobile.operation-queue.v1", JSON.stringify(this.queue));
  }

  private pruneTerminalEntries(): void {
    while (this.queue.length > OperationQueueService.MAX_ENTRIES) {
      const index = this.queue.findIndex((entry) => entry.status === "synced" || entry.status === "failed");
      if (index < 0) return;
      this.queue.splice(index, 1);
    }
  }
}
