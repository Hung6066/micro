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
      status: "pending",
    };
    this.queue.push(operation);
    this.persist();
    return operation;
  }

  async sync(transport: OperationTransport): Promise<void> {
    for (const operation of this.queue.filter((item) => item.status === "pending")) {
      try {
        const result = await transport(operation);
        this.applyResult(operation, result.kind, "message" in result ? result.message : undefined);
      } catch {
        operation.status = "pending";
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

  private applyResult(operation: QueuedOperation, status: OperationStatus, message?: string): void {
    operation.status = status;
    operation.error = message;
  }

  private persist(): void {
    this.secureStorage?.write("operator-mobile.operation-queue.v1", JSON.stringify(this.queue));
  }
}
