import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable, map } from "rxjs";
import { environment } from "../../../environments/environment";

interface PasskeyRegistrationOptionsResponse {
  challenge: string;
  rp: PublicKeyCredentialRpEntity;
  user: Omit<PublicKeyCredentialUserEntity, "id"> & { id: string };
  pubKeyCredParams: PublicKeyCredentialParameters[];
  timeout?: number;
  excludeCredentials?: Array<Omit<PublicKeyCredentialDescriptor, "id"> & { id: string }>;
  authenticatorSelection?: AuthenticatorSelectionCriteria;
  attestation?: AttestationConveyancePreference;
}

export interface PasskeyRegistrationResponse {
  id: string;
  rawId: string;
  type: string;
  response: {
    clientDataJSON: string;
    attestationObject: string;
  };
}

@Injectable({ providedIn: "root" })
export class PasskeyApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.authApiUrl;

  getStatus(): Observable<{ registered: boolean }> {
    return this.http.get<{ registered: boolean }>(`${this.baseUrl}/passkeys/status`);
  }

  getRegistrationOptions(): Observable<PublicKeyCredentialCreationOptions> {
    return this.http
      .post<PasskeyRegistrationOptionsResponse>(`${this.baseUrl}/passkeys/register/options`, {})
      .pipe(map((options) => ({
        ...options,
        challenge: decodeBase64Url(options.challenge),
        user: { ...options.user, id: decodeBase64Url(options.user.id) },
        excludeCredentials: options.excludeCredentials?.map((credential) => ({
          ...credential,
          id: decodeBase64Url(credential.id),
        })),
      })));
  }

  completeRegistration(payload: PasskeyRegistrationResponse): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/passkeys/register/complete`, payload);
  }
}

function decodeBase64Url(value: string): Uint8Array<ArrayBuffer> {
  const normalized = value.replace(/-/g, "+").replace(/_/g, "/");
  const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "=");
  const binary = atob(padded);
  const bytes = new Uint8Array(new ArrayBuffer(binary.length));
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }
  return bytes;
}
