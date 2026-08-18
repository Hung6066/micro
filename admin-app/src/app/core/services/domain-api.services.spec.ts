import { provideHttpClient } from "@angular/common/http";
import { provideHttpClientTesting, HttpTestingController } from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation";
import { environment } from "../../../environments/environment";
import { AdminTableApiService } from "./admin-table-api.service";
import { ClientsApiService } from "./clients-api.service";
import { RolesApiService } from "./roles-api.service";
import { UsersApiService } from "./users-api.service";

describe("domain API HTTP contracts", () => {
  let http: HttpTestingController;
  const cache = jasmine.createSpyObj<HisHopeRequestCacheService>("cache", ["getOrLoad", "invalidate"]);
  const tableApi = jasmine.createSpyObj<AdminTableApiService>("tableApi", ["bulk", "export", "getViews", "saveView", "deleteView"]);

  beforeEach(() => {
    cache.getOrLoad.and.callFake((_key: string, loader: () => unknown) => loader() as never);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: HisHopeRequestCacheService, useValue: cache },
        { provide: AdminTableApiService, useValue: tableApi },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("sends the clients page query and normalizes client type", () => {
    TestBed.inject(ClientsApiService).getClientsPage({ page: 2, pageSize: 25, search: " clinic " }).subscribe((result) => {
      expect(result.items[0].clientType).toBe("public");
    });
    const request = http.expectOne((item) => item.url === `${environment.adminApiUrl}/clients`);
    expect(request.request.method).toBe("GET");
    expect(request.request.params.get("page")).toBe("2");
    expect(request.request.params.get("pageSize")).toBe("25");
    expect(request.request.params.get("search")).toBe("clinic");
    request.flush({ items: [{ clientId: "c1", displayName: "Clinic", clientType: undefined, type: "public" }], totalCount: 1, page: 2, pageSize: 25 });
  });

  it("creates a role with the role payload", () => {
    const role = { name: "Reviewer", description: "Review access" };
    TestBed.inject(RolesApiService).createRole(role).subscribe();
    const request = http.expectOne(`${environment.adminApiUrl}/roles`);
    expect(request.request.method).toBe("POST");
    expect(request.request.body).toEqual(role);
    request.flush({ ...role, id: "r1" });
  });

  it("updates a user using an encoded identifier", () => {
    const requestBody = { email: "new@example.test" };
    TestBed.inject(UsersApiService).updateUser("user/1", requestBody).subscribe();
    const request = http.expectOne(`${environment.adminApiUrl}/users/user%2F1`);
    expect(request.request.method).toBe("PUT");
    expect(request.request.body).toEqual(requestBody);
    request.flush({ id: "u1", userName: "user", email: requestBody.email, roles: [], isActive: true });
  });
});
