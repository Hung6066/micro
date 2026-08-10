import { TestBed } from "@angular/core/testing";
import { HisHopeErrorReportingService } from "./his-hope-error-reporting.service";

describe("HisHopeErrorReportingService", () => {
  let service: HisHopeErrorReportingService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(HisHopeErrorReportingService);
  });

  it("keeps reported events in memory", () => {
    service.report({ message: "boom", severity: "error" });
    expect(service.events()).toEqual([{ message: "boom", severity: "error" }]);
  });

  it("forwards every reported event to a configured reporter", () => {
    const reporter = jasmine.createSpy("reporter");
    service.configure(reporter);
    service.report({ message: "boom", severity: "fatal" });
    expect(reporter).toHaveBeenCalledWith(
      jasmine.objectContaining({ message: "boom", severity: "fatal" }),
    );
  });

  it("caps in-memory history at 50 events, dropping the oldest first", () => {
    for (let i = 0; i < 60; i++)
      service.report({ message: `e${i}`, severity: "warning" });
    const events = service.events();
    expect(events.length).toBe(50);
    expect(events[0].message).toBe("e10");
    expect(events[49].message).toBe("e59");
  });

  it("clears history on demand", () => {
    service.report({ message: "x", severity: "error" });
    service.clear();
    expect(service.events()).toEqual([]);
  });
});
