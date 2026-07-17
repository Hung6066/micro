// @ts-nocheck
function stryNS_9fa48() {
  var g = typeof globalThis === 'object' && globalThis && globalThis.Math === Math && globalThis || new Function("return this")();
  var ns = g.__stryker__ || (g.__stryker__ = {});
  if (ns.activeMutant === undefined && g.process && g.process.env && g.process.env.__STRYKER_ACTIVE_MUTANT__) {
    ns.activeMutant = g.process.env.__STRYKER_ACTIVE_MUTANT__;
  }
  function retrieveNS() {
    return ns;
  }
  stryNS_9fa48 = retrieveNS;
  return retrieveNS();
}
stryNS_9fa48();
function stryCov_9fa48() {
  var ns = stryNS_9fa48();
  var cov = ns.mutantCoverage || (ns.mutantCoverage = {
    static: {},
    perTest: {}
  });
  function cover() {
    var c = cov.static;
    if (ns.currentTestId) {
      c = cov.perTest[ns.currentTestId] = cov.perTest[ns.currentTestId] || {};
    }
    var a = arguments;
    for (var i = 0; i < a.length; i++) {
      c[a[i]] = (c[a[i]] || 0) + 1;
    }
  }
  stryCov_9fa48 = cover;
  cover.apply(null, arguments);
}
function stryMutAct_9fa48(id) {
  var ns = stryNS_9fa48();
  function isActive(id) {
    if (ns.activeMutant === id) {
      if (ns.hitCount !== void 0 && ++ns.hitCount > ns.hitLimit) {
        throw new Error('Stryker: Hit count limit reached (' + ns.hitCount + ')');
      }
      return true;
    }
    return false;
  }
  stryMutAct_9fa48 = isActive;
  return isActive(id);
}
import { Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { environment } from '@env/environment';
export interface ErrorContext {
  correlationId: string;
  message: string;
  type: string;
  stack?: string;
  url: string;
  userAction?: string;
  timestamp: string;
  userId?: string;
}
@Injectable({
  providedIn: 'root'
})
export class ErrorService {
  private currentAction = stryMutAct_9fa48("1099") ? "Stryker was here!" : (stryCov_9fa48("1099"), '');
  private readonly apiUrl = stryMutAct_9fa48("1100") ? `` : (stryCov_9fa48("1100"), `${environment.apiUrl}/errors`);
  constructor(private http: HttpClient, private authService: AuthService) {}
  getCorrelationId(error: HttpErrorResponse): string {
    if (stryMutAct_9fa48("1101")) {
      {}
    } else {
      stryCov_9fa48("1101");
      const fromHeaders = stryMutAct_9fa48("1102") ? error.headers.get('X-Correlation-Id') : (stryCov_9fa48("1102"), error.headers?.get(stryMutAct_9fa48("1103") ? "" : (stryCov_9fa48("1103"), 'X-Correlation-Id')));
      if (stryMutAct_9fa48("1105") ? false : stryMutAct_9fa48("1104") ? true : (stryCov_9fa48("1104", "1105"), fromHeaders)) return fromHeaders;
      if (stryMutAct_9fa48("1108") ? typeof error.error === 'object' || error.error?.correlationId : stryMutAct_9fa48("1107") ? false : stryMutAct_9fa48("1106") ? true : (stryCov_9fa48("1106", "1107", "1108"), (stryMutAct_9fa48("1110") ? typeof error.error !== 'object' : stryMutAct_9fa48("1109") ? true : (stryCov_9fa48("1109", "1110"), typeof error.error === (stryMutAct_9fa48("1111") ? "" : (stryCov_9fa48("1111"), 'object')))) && (stryMutAct_9fa48("1112") ? error.error.correlationId : (stryCov_9fa48("1112"), error.error?.correlationId)))) {
        if (stryMutAct_9fa48("1113")) {
          {}
        } else {
          stryCov_9fa48("1113");
          return error.error.correlationId;
        }
      }
      return this.generateCorrelationId();
    }
  }
  buildErrorContext(error: unknown): ErrorContext {
    if (stryMutAct_9fa48("1114")) {
      {}
    } else {
      stryCov_9fa48("1114");
      const timestamp = new Date().toISOString();
      const user = stryMutAct_9fa48("1115") ? this.authService['currentUserSubject'].value : (stryCov_9fa48("1115"), this.authService[stryMutAct_9fa48("1116") ? "" : (stryCov_9fa48("1116"), 'currentUserSubject')]?.value);
      if (stryMutAct_9fa48("1118") ? false : stryMutAct_9fa48("1117") ? true : (stryCov_9fa48("1117", "1118"), error instanceof HttpErrorResponse)) {
        if (stryMutAct_9fa48("1119")) {
          {}
        } else {
          stryCov_9fa48("1119");
          return stryMutAct_9fa48("1120") ? {} : (stryCov_9fa48("1120"), {
            correlationId: this.getCorrelationId(error),
            message: this.getHttpErrorMessage(error),
            type: stryMutAct_9fa48("1121") ? `` : (stryCov_9fa48("1121"), `HTTP_${error.status}`),
            url: stryMutAct_9fa48("1122") ? error.url && window.location.href : (stryCov_9fa48("1122"), error.url ?? window.location.href),
            userAction: stryMutAct_9fa48("1125") ? this.currentAction && undefined : stryMutAct_9fa48("1124") ? false : stryMutAct_9fa48("1123") ? true : (stryCov_9fa48("1123", "1124", "1125"), this.currentAction || undefined),
            timestamp,
            userId: stryMutAct_9fa48("1126") ? user.id : (stryCov_9fa48("1126"), user?.id)
          });
        }
      }
      if (stryMutAct_9fa48("1128") ? false : stryMutAct_9fa48("1127") ? true : (stryCov_9fa48("1127", "1128"), error instanceof Error)) {
        if (stryMutAct_9fa48("1129")) {
          {}
        } else {
          stryCov_9fa48("1129");
          return stryMutAct_9fa48("1130") ? {} : (stryCov_9fa48("1130"), {
            correlationId: this.generateCorrelationId(),
            message: error.message,
            type: error.name,
            stack: error.stack,
            url: window.location.href,
            userAction: stryMutAct_9fa48("1133") ? this.currentAction && undefined : stryMutAct_9fa48("1132") ? false : stryMutAct_9fa48("1131") ? true : (stryCov_9fa48("1131", "1132", "1133"), this.currentAction || undefined),
            timestamp,
            userId: stryMutAct_9fa48("1134") ? user.id : (stryCov_9fa48("1134"), user?.id)
          });
        }
      }
      return stryMutAct_9fa48("1135") ? {} : (stryCov_9fa48("1135"), {
        correlationId: this.generateCorrelationId(),
        message: (stryMutAct_9fa48("1138") ? typeof error !== 'string' : stryMutAct_9fa48("1137") ? false : stryMutAct_9fa48("1136") ? true : (stryCov_9fa48("1136", "1137", "1138"), typeof error === (stryMutAct_9fa48("1139") ? "" : (stryCov_9fa48("1139"), 'string')))) ? error : stryMutAct_9fa48("1140") ? "" : (stryCov_9fa48("1140"), 'An unknown error occurred'),
        type: stryMutAct_9fa48("1141") ? "" : (stryCov_9fa48("1141"), 'UNKNOWN'),
        url: window.location.href,
        userAction: stryMutAct_9fa48("1144") ? this.currentAction && undefined : stryMutAct_9fa48("1143") ? false : stryMutAct_9fa48("1142") ? true : (stryCov_9fa48("1142", "1143", "1144"), this.currentAction || undefined),
        timestamp,
        userId: stryMutAct_9fa48("1145") ? user.id : (stryCov_9fa48("1145"), user?.id)
      });
    }
  }
  reportError(context: ErrorContext): Observable<void> {
    if (stryMutAct_9fa48("1146")) {
      {}
    } else {
      stryCov_9fa48("1146");
      return this.http.post<void>(this.apiUrl, context).pipe(catchError(stryMutAct_9fa48("1147") ? () => undefined : (stryCov_9fa48("1147"), () => of(void 0))));
    }
  }
  trackUserAction(action: string): void {
    if (stryMutAct_9fa48("1148")) {
      {}
    } else {
      stryCov_9fa48("1148");
      this.currentAction = action;
    }
  }
  private getHttpErrorMessage(error: HttpErrorResponse): string {
    if (stryMutAct_9fa48("1149")) {
      {}
    } else {
      stryCov_9fa48("1149");
      if (stryMutAct_9fa48("1152") ? error.status !== 0 : stryMutAct_9fa48("1151") ? false : stryMutAct_9fa48("1150") ? true : (stryCov_9fa48("1150", "1151", "1152"), error.status === 0)) {
        if (stryMutAct_9fa48("1153")) {
          {}
        } else {
          stryCov_9fa48("1153");
          return stryMutAct_9fa48("1154") ? "" : (stryCov_9fa48("1154"), 'Unable to connect to the server. Please check your connection.');
        }
      }
      if (stryMutAct_9fa48("1157") ? error.error.error : stryMutAct_9fa48("1156") ? false : stryMutAct_9fa48("1155") ? true : (stryCov_9fa48("1155", "1156", "1157"), error.error?.error)) return error.error.error;
      if (stryMutAct_9fa48("1160") ? error.error.message : stryMutAct_9fa48("1159") ? false : stryMutAct_9fa48("1158") ? true : (stryCov_9fa48("1158", "1159", "1160"), error.error?.message)) return error.error.message;
      if (stryMutAct_9fa48("1162") ? false : stryMutAct_9fa48("1161") ? true : (stryCov_9fa48("1161", "1162"), error.message)) return error.message;
      return stryMutAct_9fa48("1163") ? `` : (stryCov_9fa48("1163"), `HTTP error ${error.status}`);
    }
  }
  private generateCorrelationId(): string {
    if (stryMutAct_9fa48("1164")) {
      {}
    } else {
      stryCov_9fa48("1164");
      const timestamp = Date.now().toString(36);
      const random = stryMutAct_9fa48("1165") ? Math.random().toString(36) : (stryCov_9fa48("1165"), Math.random().toString(36).substring(2, 8));
      return stryMutAct_9fa48("1166") ? `` : (stryCov_9fa48("1166"), `hh-${timestamp}-${random}`);
    }
  }
}