import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation/query";
import { environment } from "../../../environments/environment";
import { IamGroupMember } from "../contracts/iam.contracts";
import { IamApiService } from "./iam-api.service";

describe("IamApiService group membership", () => {
  let service: IamApiService;
  let http: HttpTestingController;
  const membersUrl = `${environment.adminApiUrl}/iam/groups/group%2F1/members`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        IamApiService,
        {
          provide: HisHopeRequestCacheService,
          useValue: { invalidate: jasmine.createSpy("invalidate") },
        },
      ],
    });
    service = TestBed.inject(IamApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("lists the members of a group", () => {
    const members: IamGroupMember[] = [
      {
        userId: "user-1",
        email: "ops@example.com",
        displayName: "Ops Admin",
        createdAt: "2026-11-20T10:00:00Z",
        createdBy: "admin@example.com",
      },
    ];
    let result: IamGroupMember[] | undefined;

    service.getGroupMembers("group/1").subscribe((value) => (result = value));

    const request = http.expectOne(membersUrl);
    expect(request.request.method).toBe("GET");
    request.flush(members);
    expect(result).toEqual(members);
  });

  it("adds a user to a group", () => {
    service.addGroupMember("group/1", "user 1").subscribe();

    const request = http.expectOne(`${membersUrl}/user%201`);
    expect(request.request.method).toBe("POST");
    request.flush(null, { status: 204, statusText: "No Content" });
  });

  it("removes a user from a group", () => {
    service.removeGroupMember("group/1", "user-1").subscribe();

    const request = http.expectOne(`${membersUrl}/user-1`);
    expect(request.request.method).toBe("DELETE");
    request.flush(null, { status: 204, statusText: "No Content" });
  });
});
