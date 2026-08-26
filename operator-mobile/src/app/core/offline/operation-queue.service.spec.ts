import { OperationQueueService } from "./operation-queue.service";
import type { QueuedOperation } from "./operation-queue.models";

describe("OperationQueueService", () => {
  let service: OperationQueueService;

  beforeEach(() => {
    service = new OperationQueueService();
  });

  it("creates one stable operation id and keeps a network failure pending", async () => {
    const command: Omit<QueuedOperation, "id" | "operationId" | "createdAt" | "status"> = {
      tenantKey: "factory-a",
      subjectId: "operator-a",
      endpoint: "/production-batches/batch-1/operations",
      payload: { outputQuantity: 42 },
      expectedVersion: "W/\"batch-v1\"",
    };

    const result = await service.submit(command, async () => {
      throw new TypeError("offline");
    });

    expect(result.status).toBe("pending");
    expect(result.operationId).toMatch(/^[0-9a-f-]{36}$/);
    expect((await service.entries())[0].operationId).toBe(result.operationId);
  });

  it("marks a conflict without retrying a 409 response", async () => {
    const command = {
      tenantKey: "factory-a",
      subjectId: "operator-a",
      endpoint: "/quality-inspections",
      payload: { lotId: "lot-1" },
      expectedVersion: "W/\"lot-v1\"",
    };
    let attempts = 0;
    await service.enqueue(command);

    await service.sync(async () => {
      attempts += 1;
      return { kind: "conflict", statusCode: 409, message: "stale" } as const;
    });

    expect(attempts).toBe(1);
    expect((await service.entries())[0].status).toBe("conflict");
  });
});
