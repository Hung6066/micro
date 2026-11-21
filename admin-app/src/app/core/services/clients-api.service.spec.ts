import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { HisHopeRequestCacheService } from "@his-hope/frontend-foundation/query";
import { environment } from "../../../environments/environment";
import { AdminTableApiService } from "./admin-table-api.service";
import { ClientsApiService } from "./clients-api.service";

describe("ClientsApiService", () => {
  let service: ClientsApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        ClientsApiService,
        {
          provide: HisHopeRequestCacheService,
          useValue: { invalidate: jasmine.createSpy("invalidate") },
        },
        { provide: AdminTableApiService, useValue: {} },
      ],
    });
    service = TestBed.inject(ClientsApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  for (const clientType of ["Public", "Confidential"]) {
    it(`sends the API type field when creating a ${clientType} client`, () => {
      service
        .createClient({
          clientId: "test-console",
          displayName: "Test console",
          clientType,
          grantTypes: ["authorization_code"],
          redirectUris: ["https://console.example/callback"],
          scopes: ["openid"],
        })
        .subscribe();

      const request = http.expectOne(`${environment.adminApiUrl}/clients`);
      expect(request.request.method).toBe("POST");
      expect(request.request.body.type).toBe(clientType);
      request.flush({ clientId: "test-console", clientSecret: "", message: "" });
    });
  }
});
