import { DestroyRef } from "@angular/core";
import { of, throwError } from "rxjs";
import { HisHopePageResult } from "@his-hope/frontend-foundation/contracts";
import { HisHopeResourceTableController } from "./his-hope-resource-table.controller";

describe("HisHopeResourceTableController", () => {
  const destroyRef = {
    onDestroy: () => undefined,
    destroyed: false,
  } as unknown as DestroyRef;
  const i18n = {
    t: (_key: string, fallback: string) => fallback,
  };

  function pageResult(
    items: Array<{ id: string }>,
  ): HisHopePageResult<{ id: string }> {
    return {
      items,
      totalCount: items.length,
      page: 1,
      pageSize: 20,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    };
  }

  it("notifies OnPush hosts when an action error is set", () => {
    const onStateChange = jasmine.createSpy("onStateChange");
    const table = new HisHopeResourceTableController({
      destroyRef,
      i18n,
      initialQuery: { page: 1, pageSize: 20 },
      loader: () => of(pageResult([{ id: "1" }])),
      loadErrorMessageKey: "admin.loadFailed",
      loadErrorFallback: "Failed to load.",
      onStateChange,
    });

    table.setActionError("Unable to publish.");

    expect(table.error).toBe("Unable to publish.");
    expect(onStateChange).toHaveBeenCalled();
  });

  it("reloads after a successful bulk action", () => {
    const loader = jasmine
      .createSpy("loader")
      .and.returnValue(of(pageResult([{ id: "1" }])));
    const table = new HisHopeResourceTableController({
      destroyRef,
      i18n,
      initialQuery: { page: 1, pageSize: 20 },
      loader,
      loadErrorMessageKey: "admin.loadFailed",
      loadErrorFallback: "Failed to load.",
    });

    table.runBulkAction(
      {
        actionId: "delete",
        rowKeys: ["1"],
        query: { page: 1, pageSize: 20 },
      },
      () => of({ actionId: "delete", requested: 1, updated: 1 }),
      "admin.updateFailed",
      "Failed to update.",
    );

    expect(loader).toHaveBeenCalled();
  });

  it("surfaces a bulk-action failure without treating it as success", () => {
    const table = new HisHopeResourceTableController({
      destroyRef,
      i18n,
      initialQuery: { page: 1, pageSize: 20 },
      loader: () => of(pageResult([])),
      loadErrorMessageKey: "admin.loadFailed",
      loadErrorFallback: "Failed to load.",
    });

    table.runBulkAction(
      {
        actionId: "delete",
        rowKeys: ["1"],
        query: { page: 1, pageSize: 20 },
      },
      () => throwError(() => new Error("nope")),
      "admin.updateFailed",
      "Failed to update.",
    );

    expect(table.error).toBe("Failed to update.");
    expect(table.bulkLoading).toBeFalse();
  });
});
