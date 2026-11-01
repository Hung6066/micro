import { Injectable, inject } from "@angular/core";
import { HttpClient, HttpParams } from "@angular/common/http";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";

export interface OperatorDirectoryUser {
  id: string;
  username: string;
  email: string;
  firstName?: string | null;
  lastName?: string | null;
  isActive: boolean;
}

@Injectable({ providedIn: "root" })
export class AdminDirectoryService {
  private readonly http = inject(HttpClient);

  getUsers(pageSize = 100): Observable<{ items: OperatorDirectoryUser[] }> {
    const params = new HttpParams().set("page", "1").set("pageSize", String(pageSize));
    return this.http.get<{ items: OperatorDirectoryUser[] }>(`${environment.adminApiUrl}/users`, { params });
  }
}
