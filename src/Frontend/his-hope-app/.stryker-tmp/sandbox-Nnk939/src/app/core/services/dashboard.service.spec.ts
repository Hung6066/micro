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
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { DashboardService } from './dashboard.service';
import { environment } from '@env/environment';
describe(stryMutAct_9fa48("1026") ? "" : (stryCov_9fa48("1026"), 'DashboardService'), () => {
  if (stryMutAct_9fa48("1027")) {
    {}
  } else {
    stryCov_9fa48("1027");
    let service: DashboardService;
    let httpMock: HttpTestingController;
    beforeEach(() => {
      if (stryMutAct_9fa48("1028")) {
        {}
      } else {
        stryCov_9fa48("1028");
        TestBed.configureTestingModule(stryMutAct_9fa48("1029") ? {} : (stryCov_9fa48("1029"), {
          imports: stryMutAct_9fa48("1030") ? [] : (stryCov_9fa48("1030"), [HttpClientTestingModule])
        }));
        service = TestBed.inject(DashboardService);
        httpMock = TestBed.inject(HttpTestingController);
      }
    });
    afterEach(() => {
      if (stryMutAct_9fa48("1031")) {
        {}
      } else {
        stryCov_9fa48("1031");
        httpMock.verify();
      }
    });
    it(stryMutAct_9fa48("1032") ? "" : (stryCov_9fa48("1032"), 'should get dashboard stats'), () => {
      if (stryMutAct_9fa48("1033")) {
        {}
      } else {
        stryCov_9fa48("1033");
        const mockStats = stryMutAct_9fa48("1034") ? {} : (stryCov_9fa48("1034"), {
          totalPatients: 150,
          todayAppointments: 12,
          activeEncounters: 8,
          pendingDiagnoses: 3,
          pendingLabs: 5,
          outstandingInvoices: 20,
          lowStockMedications: 2,
          newPatientsToday: 3,
          appointmentsTomorrow: 7,
          recentEncounters: stryMutAct_9fa48("1035") ? ["Stryker was here"] : (stryCov_9fa48("1035"), []),
          upcomingAppointments: stryMutAct_9fa48("1036") ? ["Stryker was here"] : (stryCov_9fa48("1036"), [])
        });
        service.getStats().subscribe(stats => {
          if (stryMutAct_9fa48("1037")) {
            {}
          } else {
            stryCov_9fa48("1037");
            expect(stats.totalPatients).toBe(150);
            expect(stats.todayAppointments).toBe(12);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1038") ? `` : (stryCov_9fa48("1038"), `${environment.apiUrl}/dashboard/stats`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1039") ? "" : (stryCov_9fa48("1039"), 'GET'));
        req.flush(mockStats);
      }
    });
    it(stryMutAct_9fa48("1040") ? "" : (stryCov_9fa48("1040"), 'should get recent encounters'), () => {
      if (stryMutAct_9fa48("1041")) {
        {}
      } else {
        stryCov_9fa48("1041");
        const mockResponse = stryMutAct_9fa48("1042") ? {} : (stryCov_9fa48("1042"), {
          items: stryMutAct_9fa48("1043") ? [] : (stryCov_9fa48("1043"), [stryMutAct_9fa48("1044") ? {} : (stryCov_9fa48("1044"), {
            id: stryMutAct_9fa48("1045") ? "" : (stryCov_9fa48("1045"), 'enc-001')
          }), stryMutAct_9fa48("1046") ? {} : (stryCov_9fa48("1046"), {
            id: stryMutAct_9fa48("1047") ? "" : (stryCov_9fa48("1047"), 'enc-002')
          })])
        });
        service.getRecentEncounters(5).subscribe(res => {
          if (stryMutAct_9fa48("1048")) {
            {}
          } else {
            stryCov_9fa48("1048");
            expect(res.items.length).toBe(2);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1049") ? () => undefined : (stryCov_9fa48("1049"), r => stryMutAct_9fa48("1052") ? r.urlWithParams.includes('/dashboard/recent-encounters') || r.urlWithParams.includes('limit=5') : stryMutAct_9fa48("1051") ? false : stryMutAct_9fa48("1050") ? true : (stryCov_9fa48("1050", "1051", "1052"), r.urlWithParams.includes(stryMutAct_9fa48("1053") ? "" : (stryCov_9fa48("1053"), '/dashboard/recent-encounters')) && r.urlWithParams.includes(stryMutAct_9fa48("1054") ? "" : (stryCov_9fa48("1054"), 'limit=5')))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1055") ? "" : (stryCov_9fa48("1055"), 'GET'));
        req.flush(mockResponse);
      }
    });
    it(stryMutAct_9fa48("1056") ? "" : (stryCov_9fa48("1056"), 'should get upcoming appointments'), () => {
      if (stryMutAct_9fa48("1057")) {
        {}
      } else {
        stryCov_9fa48("1057");
        const mockResponse = stryMutAct_9fa48("1058") ? {} : (stryCov_9fa48("1058"), {
          items: stryMutAct_9fa48("1059") ? [] : (stryCov_9fa48("1059"), [stryMutAct_9fa48("1060") ? {} : (stryCov_9fa48("1060"), {
            id: stryMutAct_9fa48("1061") ? "" : (stryCov_9fa48("1061"), 'apt-001')
          })])
        });
        service.getUpcomingAppointments().subscribe(res => {
          if (stryMutAct_9fa48("1062")) {
            {}
          } else {
            stryCov_9fa48("1062");
            expect(res.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1063") ? `` : (stryCov_9fa48("1063"), `${environment.apiUrl}/dashboard/upcoming-appointments`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1064") ? "" : (stryCov_9fa48("1064"), 'GET'));
        req.flush(mockResponse);
      }
    });
    it(stryMutAct_9fa48("1065") ? "" : (stryCov_9fa48("1065"), 'should getRecentEncounters with default limit'), () => {
      if (stryMutAct_9fa48("1066")) {
        {}
      } else {
        stryCov_9fa48("1066");
        service.getRecentEncounters().subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1067") ? () => undefined : (stryCov_9fa48("1067"), r => stryMutAct_9fa48("1070") ? r.urlWithParams.includes('/dashboard/recent-encounters') || r.urlWithParams.includes('limit=5') : stryMutAct_9fa48("1069") ? false : stryMutAct_9fa48("1068") ? true : (stryCov_9fa48("1068", "1069", "1070"), r.urlWithParams.includes(stryMutAct_9fa48("1071") ? "" : (stryCov_9fa48("1071"), '/dashboard/recent-encounters')) && r.urlWithParams.includes(stryMutAct_9fa48("1072") ? "" : (stryCov_9fa48("1072"), 'limit=5')))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1073") ? "" : (stryCov_9fa48("1073"), 'GET'));
        req.flush(stryMutAct_9fa48("1074") ? {} : (stryCov_9fa48("1074"), {
          items: stryMutAct_9fa48("1075") ? ["Stryker was here"] : (stryCov_9fa48("1075"), [])
        }));
      }
    });
    it(stryMutAct_9fa48("1076") ? "" : (stryCov_9fa48("1076"), 'should getStats with full data'), () => {
      if (stryMutAct_9fa48("1077")) {
        {}
      } else {
        stryCov_9fa48("1077");
        const fullStats = stryMutAct_9fa48("1078") ? {} : (stryCov_9fa48("1078"), {
          totalPatients: 100,
          todayAppointments: 5,
          activeEncounters: 3,
          pendingDiagnoses: 1,
          pendingLabs: 2,
          outstandingInvoices: 10,
          lowStockMedications: 0,
          newPatientsToday: 1,
          appointmentsTomorrow: 3,
          recentEncounters: stryMutAct_9fa48("1079") ? [] : (stryCov_9fa48("1079"), [stryMutAct_9fa48("1080") ? {} : (stryCov_9fa48("1080"), {
            id: stryMutAct_9fa48("1081") ? "" : (stryCov_9fa48("1081"), 'enc-001')
          })]),
          upcomingAppointments: stryMutAct_9fa48("1082") ? [] : (stryCov_9fa48("1082"), [stryMutAct_9fa48("1083") ? {} : (stryCov_9fa48("1083"), {
            id: stryMutAct_9fa48("1084") ? "" : (stryCov_9fa48("1084"), 'apt-001')
          })])
        });
        service.getStats().subscribe(stats => {
          if (stryMutAct_9fa48("1085")) {
            {}
          } else {
            stryCov_9fa48("1085");
            expect(stats.recentEncounters.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1086") ? `` : (stryCov_9fa48("1086"), `${environment.apiUrl}/dashboard/stats`));
        req.flush(fullStats);
      }
    });
  }
});