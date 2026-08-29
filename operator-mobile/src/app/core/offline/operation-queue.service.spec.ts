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

  it("moves failed or conflicted records back to pending for an explicit retry", async () => {
    const operation = await service.enqueue({
      tenantKey: "factory-a",
      subjectId: "operator-a",
      endpoint: "/quality-inspections",
      payload: { lotId: "lot-1" },
    });
    await service.sync(async () => ({ kind: "failed", message: "validation" }));
    await service.retry(operation.id);
    expect((await service.entries())[0].status).toBe("pending");
    expect((await service.entries())[0].error).toBeUndefined();
  });

  it("clears all local records when the session is wiped", async () => {
    await service.enqueue({ tenantKey: "factory-a", subjectId: "operator-a", endpoint: "/x", payload: {} });
    await service.clear();
    expect(await service.entries()).toEqual([]);
  });

  it("dead-letters a transient operation after the retry budget and retains pending work", async () => {
    const operation = await service.enqueue({ tenantKey: "factory-a", subjectId: "operator-a", endpoint: "/x", payload: {} });
    for (let attempt = 0; attempt < 5; attempt += 1) {
      await service.sync(async () => ({ kind: "pending", message: "offline" }));
    }
    expect((await service.entries()).find((entry) => entry.id === operation.id)?.status).toBe("failed");
    expect((await service.entries()).find((entry) => entry.id === operation.id)?.attemptCount).toBe(5);
  });

  it("limits retained terminal records without dropping pending work", async () => {
    for (let index = 0; index < 105; index += 1) {
      await service.enqueue({ tenantKey: "factory-a", subjectId: "operator-a", endpoint: `/x/${index}`, payload: {} });
      await service.sync(async () => ({ kind: "failed", message: "invalid" }));
    }
    const entries = await service.entries();
    expect(entries.length).toBe(100);
    expect(entries.every((entry) => entry.status === "failed")).toBeTrue();
  });
});
