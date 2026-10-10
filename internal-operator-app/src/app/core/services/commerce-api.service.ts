import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import {
  HisHopeCommerceRfqDto,
  HisHopeCommerceRfqListResponse,
  HisHopeRespondCommerceRfqRequest,
  HisHopeCommerceOrderListResponse,
} from "@his-hope/frontend-foundation/contracts";
import { environment } from "../../../environments/environment";

@Injectable({ providedIn: "root" })
export class CommerceApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.commerceApiUrl;

  getRfqs() {
    return this.http.get<HisHopeCommerceRfqListResponse>(`${this.base}/rfqs`);
  }

  getOrders() {
    return this.http.get<HisHopeCommerceOrderListResponse>(`${this.base}/orders`);
  }

  respondToRfq(rfqId: string, body: HisHopeRespondCommerceRfqRequest) {
    return this.http.patch<HisHopeCommerceRfqDto>(`${this.base}/rfqs/${rfqId}/respond`, body);
  }
}
