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
;
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ClinicalService } from './clinical.service';
import { environment } from '@env/environment';
describe(stryMutAct_9fa48("886") ? "" : (stryCov_9fa48("886"), 'ClinicalService'), () => {
  if (stryMutAct_9fa48("887")) {
    {}
  } else {
    stryCov_9fa48("887");
    let service: ClinicalService;
    let httpMock: HttpTestingController;
    beforeEach(() => {
      if (stryMutAct_9fa48("888")) {
        {}
      } else {
        stryCov_9fa48("888");
        TestBed.configureTestingModule(stryMutAct_9fa48("889") ? {} : (stryCov_9fa48("889"), {
          imports: stryMutAct_9fa48("890") ? [] : (stryCov_9fa48("890"), [HttpClientTestingModule])
        }));
        service = TestBed.inject(ClinicalService);
        httpMock = TestBed.inject(HttpTestingController);
      }
    });
    afterEach(() => {
      if (stryMutAct_9fa48("891")) {
        {}
      } else {
        stryCov_9fa48("891");
        httpMock.verify();
      }
    });
    it(stryMutAct_9fa48("892") ? "" : (stryCov_9fa48("892"), 'should list encounters with pagination'), () => {
      if (stryMutAct_9fa48("893")) {
        {}
      } else {
        stryCov_9fa48("893");
        const mockResult = stryMutAct_9fa48("894") ? {} : (stryCov_9fa48("894"), {
          items: stryMutAct_9fa48("895") ? [] : (stryCov_9fa48("895"), [stryMutAct_9fa48("896") ? {} : (stryCov_9fa48("896"), {
            id: stryMutAct_9fa48("897") ? "" : (stryCov_9fa48("897"), 'enc-001')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("898") ? true : (stryCov_9fa48("898"), false),
          hasPreviousPage: stryMutAct_9fa48("899") ? true : (stryCov_9fa48("899"), false)
        });
        service.list(1, 20).subscribe(result => {
          if (stryMutAct_9fa48("900")) {
            {}
          } else {
            stryCov_9fa48("900");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("901") ? () => undefined : (stryCov_9fa48("901"), r => stryMutAct_9fa48("904") ? r.url !== `${environment.apiUrl}/encounters/search` : stryMutAct_9fa48("903") ? false : stryMutAct_9fa48("902") ? true : (stryCov_9fa48("902", "903", "904"), r.url === (stryMutAct_9fa48("905") ? `` : (stryCov_9fa48("905"), `${environment.apiUrl}/encounters/search`)))));
        expect(req.request.method).toBe(stryMutAct_9fa48("906") ? "" : (stryCov_9fa48("906"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("907") ? "" : (stryCov_9fa48("907"), 'should search encounters'), () => {
      if (stryMutAct_9fa48("908")) {
        {}
      } else {
        stryCov_9fa48("908");
        service.search(stryMutAct_9fa48("909") ? "" : (stryCov_9fa48("909"), 'pain')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("910") ? () => undefined : (stryCov_9fa48("910"), r => stryMutAct_9fa48("913") ? r.url === `${environment.apiUrl}/encounters/search` || r.params.get('q') === 'pain' : stryMutAct_9fa48("912") ? false : stryMutAct_9fa48("911") ? true : (stryCov_9fa48("911", "912", "913"), (stryMutAct_9fa48("915") ? r.url !== `${environment.apiUrl}/encounters/search` : stryMutAct_9fa48("914") ? true : (stryCov_9fa48("914", "915"), r.url === (stryMutAct_9fa48("916") ? `` : (stryCov_9fa48("916"), `${environment.apiUrl}/encounters/search`)))) && (stryMutAct_9fa48("918") ? r.params.get('q') !== 'pain' : stryMutAct_9fa48("917") ? true : (stryCov_9fa48("917", "918"), r.params.get(stryMutAct_9fa48("919") ? "" : (stryCov_9fa48("919"), 'q')) === (stryMutAct_9fa48("920") ? "" : (stryCov_9fa48("920"), 'pain')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("921") ? "" : (stryCov_9fa48("921"), 'GET'));
        req.flush(stryMutAct_9fa48("922") ? {} : (stryCov_9fa48("922"), {
          items: stryMutAct_9fa48("923") ? ["Stryker was here"] : (stryCov_9fa48("923"), []),
          totalCount: 0,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("924") ? true : (stryCov_9fa48("924"), false),
          hasPreviousPage: stryMutAct_9fa48("925") ? true : (stryCov_9fa48("925"), false)
        }));
      }
    });
    it(stryMutAct_9fa48("926") ? "" : (stryCov_9fa48("926"), 'should start an encounter'), () => {
      if (stryMutAct_9fa48("927")) {
        {}
      } else {
        stryCov_9fa48("927");
        const request = stryMutAct_9fa48("928") ? {} : (stryCov_9fa48("928"), {
          patientId: stryMutAct_9fa48("929") ? "" : (stryCov_9fa48("929"), 'pat-001'),
          encounterType: stryMutAct_9fa48("930") ? "" : (stryCov_9fa48("930"), 'consultation')
        });
        service.start(request as any).subscribe(enc => {
          if (stryMutAct_9fa48("931")) {
            {}
          } else {
            stryCov_9fa48("931");
            expect(enc.id).toBe(stryMutAct_9fa48("932") ? "" : (stryCov_9fa48("932"), 'enc-002'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("933") ? `` : (stryCov_9fa48("933"), `${environment.apiUrl}/encounters/`));
        expect(req.request.method).toBe(stryMutAct_9fa48("934") ? "" : (stryCov_9fa48("934"), 'POST'));
        req.flush(stryMutAct_9fa48("935") ? {} : (stryCov_9fa48("935"), {
          id: stryMutAct_9fa48("936") ? "" : (stryCov_9fa48("936"), 'enc-002'),
          ...request
        }));
      }
    });
    it(stryMutAct_9fa48("937") ? "" : (stryCov_9fa48("937"), 'should record vitals'), () => {
      if (stryMutAct_9fa48("938")) {
        {}
      } else {
        stryCov_9fa48("938");
        const request = stryMutAct_9fa48("939") ? {} : (stryCov_9fa48("939"), {
          temperature: 37.0,
          heartRate: 78
        });
        service.recordVitals(stryMutAct_9fa48("940") ? "" : (stryCov_9fa48("940"), 'enc-001'), request as any).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("941") ? `` : (stryCov_9fa48("941"), `${environment.apiUrl}/encounters/enc-001/vitals`));
        expect(req.request.method).toBe(stryMutAct_9fa48("942") ? "" : (stryCov_9fa48("942"), 'POST'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("943") ? "" : (stryCov_9fa48("943"), 'should add diagnosis'), () => {
      if (stryMutAct_9fa48("944")) {
        {}
      } else {
        stryCov_9fa48("944");
        const request = stryMutAct_9fa48("945") ? {} : (stryCov_9fa48("945"), {
          diagnosisCode: stryMutAct_9fa48("946") ? "" : (stryCov_9fa48("946"), 'J45'),
          description: stryMutAct_9fa48("947") ? "" : (stryCov_9fa48("947"), 'Asthma')
        });
        service.addDiagnosis(stryMutAct_9fa48("948") ? "" : (stryCov_9fa48("948"), 'enc-001'), request as any).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("949") ? `` : (stryCov_9fa48("949"), `${environment.apiUrl}/encounters/enc-001/diagnosis`));
        expect(req.request.method).toBe(stryMutAct_9fa48("950") ? "" : (stryCov_9fa48("950"), 'POST'));
        expect(req.request.body).toEqual(request);
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("951") ? "" : (stryCov_9fa48("951"), 'should complete encounter'), () => {
      if (stryMutAct_9fa48("952")) {
        {}
      } else {
        stryCov_9fa48("952");
        service.complete(stryMutAct_9fa48("953") ? "" : (stryCov_9fa48("953"), 'enc-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("954") ? `` : (stryCov_9fa48("954"), `${environment.apiUrl}/encounters/enc-001/complete`));
        expect(req.request.method).toBe(stryMutAct_9fa48("955") ? "" : (stryCov_9fa48("955"), 'PUT'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("956") ? "" : (stryCov_9fa48("956"), 'should get encounter by id'), () => {
      if (stryMutAct_9fa48("957")) {
        {}
      } else {
        stryCov_9fa48("957");
        service.getById(stryMutAct_9fa48("958") ? "" : (stryCov_9fa48("958"), 'enc-001')).subscribe(enc => {
          if (stryMutAct_9fa48("959")) {
            {}
          } else {
            stryCov_9fa48("959");
            expect(enc.id).toBe(stryMutAct_9fa48("960") ? "" : (stryCov_9fa48("960"), 'enc-001'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("961") ? `` : (stryCov_9fa48("961"), `${environment.apiUrl}/encounters/enc-001`));
        expect(req.request.method).toBe(stryMutAct_9fa48("962") ? "" : (stryCov_9fa48("962"), 'GET'));
        req.flush(stryMutAct_9fa48("963") ? {} : (stryCov_9fa48("963"), {
          id: stryMutAct_9fa48("964") ? "" : (stryCov_9fa48("964"), 'enc-001')
        }));
      }
    });
    it(stryMutAct_9fa48("965") ? "" : (stryCov_9fa48("965"), 'should start encounter handles error'), () => {
      if (stryMutAct_9fa48("966")) {
        {}
      } else {
        stryCov_9fa48("966");
        service.start({
          patientId: 'pat-001',
          encounterType: 'consultation'
        } as any).subscribe(stryMutAct_9fa48("967") ? {} : (stryCov_9fa48("967"), {
          error: stryMutAct_9fa48("968") ? () => undefined : (stryCov_9fa48("968"), error => expect(error).toBeTruthy())
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("969") ? `` : (stryCov_9fa48("969"), `${environment.apiUrl}/encounters/`));
        req.flush(stryMutAct_9fa48("970") ? "" : (stryCov_9fa48("970"), 'Error'), stryMutAct_9fa48("971") ? {} : (stryCov_9fa48("971"), {
          status: 400,
          statusText: stryMutAct_9fa48("972") ? "" : (stryCov_9fa48("972"), 'Bad Request')
        }));
      }
    });
    it(stryMutAct_9fa48("973") ? "" : (stryCov_9fa48("973"), 'should search with pagination params'), () => {
      if (stryMutAct_9fa48("974")) {
        {}
      } else {
        stryCov_9fa48("974");
        service.search(stryMutAct_9fa48("975") ? "" : (stryCov_9fa48("975"), 'test'), 2, 50).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("976") ? () => undefined : (stryCov_9fa48("976"), r => stryMutAct_9fa48("979") ? r.params.get('q') === 'test' && r.params.get('page') === '2' || r.params.get('pageSize') === '50' : stryMutAct_9fa48("978") ? false : stryMutAct_9fa48("977") ? true : (stryCov_9fa48("977", "978", "979"), (stryMutAct_9fa48("981") ? r.params.get('q') === 'test' || r.params.get('page') === '2' : stryMutAct_9fa48("980") ? true : (stryCov_9fa48("980", "981"), (stryMutAct_9fa48("983") ? r.params.get('q') !== 'test' : stryMutAct_9fa48("982") ? true : (stryCov_9fa48("982", "983"), r.params.get(stryMutAct_9fa48("984") ? "" : (stryCov_9fa48("984"), 'q')) === (stryMutAct_9fa48("985") ? "" : (stryCov_9fa48("985"), 'test')))) && (stryMutAct_9fa48("987") ? r.params.get('page') !== '2' : stryMutAct_9fa48("986") ? true : (stryCov_9fa48("986", "987"), r.params.get(stryMutAct_9fa48("988") ? "" : (stryCov_9fa48("988"), 'page')) === (stryMutAct_9fa48("989") ? "" : (stryCov_9fa48("989"), '2')))))) && (stryMutAct_9fa48("991") ? r.params.get('pageSize') !== '50' : stryMutAct_9fa48("990") ? true : (stryCov_9fa48("990", "991"), r.params.get(stryMutAct_9fa48("992") ? "" : (stryCov_9fa48("992"), 'pageSize')) === (stryMutAct_9fa48("993") ? "" : (stryCov_9fa48("993"), '50')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("994") ? "" : (stryCov_9fa48("994"), 'GET'));
        req.flush(stryMutAct_9fa48("995") ? {} : (stryCov_9fa48("995"), {
          items: stryMutAct_9fa48("996") ? ["Stryker was here"] : (stryCov_9fa48("996"), []),
          totalCount: 0,
          page: 2,
          pageSize: 50,
          hasNextPage: stryMutAct_9fa48("997") ? true : (stryCov_9fa48("997"), false),
          hasPreviousPage: stryMutAct_9fa48("998") ? true : (stryCov_9fa48("998"), false)
        }));
      }
    });
  }
});