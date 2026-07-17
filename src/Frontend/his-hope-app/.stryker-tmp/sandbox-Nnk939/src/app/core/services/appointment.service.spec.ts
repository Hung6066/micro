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
import { AppointmentService } from './appointment.service';
import { environment } from '@env/environment';
describe(stryMutAct_9fa48("281") ? "" : (stryCov_9fa48("281"), 'AppointmentService'), () => {
  if (stryMutAct_9fa48("282")) {
    {}
  } else {
    stryCov_9fa48("282");
    let service: AppointmentService;
    let httpMock: HttpTestingController;
    beforeEach(() => {
      if (stryMutAct_9fa48("283")) {
        {}
      } else {
        stryCov_9fa48("283");
        TestBed.configureTestingModule(stryMutAct_9fa48("284") ? {} : (stryCov_9fa48("284"), {
          imports: stryMutAct_9fa48("285") ? [] : (stryCov_9fa48("285"), [HttpClientTestingModule])
        }));
        service = TestBed.inject(AppointmentService);
        httpMock = TestBed.inject(HttpTestingController);
      }
    });
    afterEach(() => {
      if (stryMutAct_9fa48("286")) {
        {}
      } else {
        stryCov_9fa48("286");
        httpMock.verify();
      }
    });
    it(stryMutAct_9fa48("287") ? "" : (stryCov_9fa48("287"), 'should list appointments with pagination'), () => {
      if (stryMutAct_9fa48("288")) {
        {}
      } else {
        stryCov_9fa48("288");
        const mockResult = stryMutAct_9fa48("289") ? {} : (stryCov_9fa48("289"), {
          items: stryMutAct_9fa48("290") ? [] : (stryCov_9fa48("290"), [stryMutAct_9fa48("291") ? {} : (stryCov_9fa48("291"), {
            id: stryMutAct_9fa48("292") ? "" : (stryCov_9fa48("292"), 'apt-001'),
            patientId: stryMutAct_9fa48("293") ? "" : (stryCov_9fa48("293"), 'pat-001')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("294") ? true : (stryCov_9fa48("294"), false),
          hasPreviousPage: stryMutAct_9fa48("295") ? true : (stryCov_9fa48("295"), false)
        });
        service.list(1, 20).subscribe(result => {
          if (stryMutAct_9fa48("296")) {
            {}
          } else {
            stryCov_9fa48("296");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("297") ? () => undefined : (stryCov_9fa48("297"), r => stryMutAct_9fa48("300") ? r.url !== `${environment.apiUrl}/appointments/search` : stryMutAct_9fa48("299") ? false : stryMutAct_9fa48("298") ? true : (stryCov_9fa48("298", "299", "300"), r.url === (stryMutAct_9fa48("301") ? `` : (stryCov_9fa48("301"), `${environment.apiUrl}/appointments/search`)))));
        expect(req.request.method).toBe(stryMutAct_9fa48("302") ? "" : (stryCov_9fa48("302"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("303") ? "" : (stryCov_9fa48("303"), 'should search appointments'), () => {
      if (stryMutAct_9fa48("304")) {
        {}
      } else {
        stryCov_9fa48("304");
        const mockResult = stryMutAct_9fa48("305") ? {} : (stryCov_9fa48("305"), {
          items: stryMutAct_9fa48("306") ? [] : (stryCov_9fa48("306"), [stryMutAct_9fa48("307") ? {} : (stryCov_9fa48("307"), {
            id: stryMutAct_9fa48("308") ? "" : (stryCov_9fa48("308"), 'apt-002')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("309") ? true : (stryCov_9fa48("309"), false),
          hasPreviousPage: stryMutAct_9fa48("310") ? true : (stryCov_9fa48("310"), false)
        });
        service.search(stryMutAct_9fa48("311") ? "" : (stryCov_9fa48("311"), 'test')).subscribe(result => {
          if (stryMutAct_9fa48("312")) {
            {}
          } else {
            stryCov_9fa48("312");
            expect(result.totalCount).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("313") ? () => undefined : (stryCov_9fa48("313"), r => stryMutAct_9fa48("316") ? r.url === `${environment.apiUrl}/appointments/search` || r.params.get('q') === 'test' : stryMutAct_9fa48("315") ? false : stryMutAct_9fa48("314") ? true : (stryCov_9fa48("314", "315", "316"), (stryMutAct_9fa48("318") ? r.url !== `${environment.apiUrl}/appointments/search` : stryMutAct_9fa48("317") ? true : (stryCov_9fa48("317", "318"), r.url === (stryMutAct_9fa48("319") ? `` : (stryCov_9fa48("319"), `${environment.apiUrl}/appointments/search`)))) && (stryMutAct_9fa48("321") ? r.params.get('q') !== 'test' : stryMutAct_9fa48("320") ? true : (stryCov_9fa48("320", "321"), r.params.get(stryMutAct_9fa48("322") ? "" : (stryCov_9fa48("322"), 'q')) === (stryMutAct_9fa48("323") ? "" : (stryCov_9fa48("323"), 'test')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("324") ? "" : (stryCov_9fa48("324"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("325") ? "" : (stryCov_9fa48("325"), 'should get appointment by id'), () => {
      if (stryMutAct_9fa48("326")) {
        {}
      } else {
        stryCov_9fa48("326");
        service.getById(stryMutAct_9fa48("327") ? "" : (stryCov_9fa48("327"), 'apt-001')).subscribe(apt => {
          if (stryMutAct_9fa48("328")) {
            {}
          } else {
            stryCov_9fa48("328");
            expect(apt.id).toBe(stryMutAct_9fa48("329") ? "" : (stryCov_9fa48("329"), 'apt-001'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("330") ? `` : (stryCov_9fa48("330"), `${environment.apiUrl}/appointments/apt-001`));
        expect(req.request.method).toBe(stryMutAct_9fa48("331") ? "" : (stryCov_9fa48("331"), 'GET'));
        req.flush(stryMutAct_9fa48("332") ? {} : (stryCov_9fa48("332"), {
          id: stryMutAct_9fa48("333") ? "" : (stryCov_9fa48("333"), 'apt-001')
        }));
      }
    });
    it(stryMutAct_9fa48("334") ? "" : (stryCov_9fa48("334"), 'should schedule appointment'), () => {
      if (stryMutAct_9fa48("335")) {
        {}
      } else {
        stryCov_9fa48("335");
        const request = stryMutAct_9fa48("336") ? {} : (stryCov_9fa48("336"), {
          patientId: stryMutAct_9fa48("337") ? "" : (stryCov_9fa48("337"), 'pat-001'),
          reason: stryMutAct_9fa48("338") ? "" : (stryCov_9fa48("338"), 'Checkup')
        });
        service.schedule(request as any).subscribe(apt => {
          if (stryMutAct_9fa48("339")) {
            {}
          } else {
            stryCov_9fa48("339");
            expect(apt.id).toBe(stryMutAct_9fa48("340") ? "" : (stryCov_9fa48("340"), 'apt-003'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("341") ? `` : (stryCov_9fa48("341"), `${environment.apiUrl}/appointments/`));
        expect(req.request.method).toBe(stryMutAct_9fa48("342") ? "" : (stryCov_9fa48("342"), 'POST'));
        req.flush(stryMutAct_9fa48("343") ? {} : (stryCov_9fa48("343"), {
          id: stryMutAct_9fa48("344") ? "" : (stryCov_9fa48("344"), 'apt-003'),
          ...request
        }));
      }
    });
    it(stryMutAct_9fa48("345") ? "" : (stryCov_9fa48("345"), 'should cancel appointment'), () => {
      if (stryMutAct_9fa48("346")) {
        {}
      } else {
        stryCov_9fa48("346");
        service.cancel(stryMutAct_9fa48("347") ? "" : (stryCov_9fa48("347"), 'apt-001'), stryMutAct_9fa48("348") ? "" : (stryCov_9fa48("348"), 'Patient no-show')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("349") ? `` : (stryCov_9fa48("349"), `${environment.apiUrl}/appointments/apt-001/cancel`));
        expect(req.request.method).toBe(stryMutAct_9fa48("350") ? "" : (stryCov_9fa48("350"), 'PUT'));
        expect(req.request.body).toEqual(stryMutAct_9fa48("351") ? {} : (stryCov_9fa48("351"), {
          reason: stryMutAct_9fa48("352") ? "" : (stryCov_9fa48("352"), 'Patient no-show')
        }));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("353") ? "" : (stryCov_9fa48("353"), 'should check in appointment'), () => {
      if (stryMutAct_9fa48("354")) {
        {}
      } else {
        stryCov_9fa48("354");
        service.checkIn(stryMutAct_9fa48("355") ? "" : (stryCov_9fa48("355"), 'apt-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("356") ? `` : (stryCov_9fa48("356"), `${environment.apiUrl}/appointments/apt-001/checkin`));
        expect(req.request.method).toBe(stryMutAct_9fa48("357") ? "" : (stryCov_9fa48("357"), 'PUT'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("358") ? "" : (stryCov_9fa48("358"), 'should check out appointment'), () => {
      if (stryMutAct_9fa48("359")) {
        {}
      } else {
        stryCov_9fa48("359");
        service.checkOut(stryMutAct_9fa48("360") ? "" : (stryCov_9fa48("360"), 'apt-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("361") ? `` : (stryCov_9fa48("361"), `${environment.apiUrl}/appointments/apt-001/checkout`));
        expect(req.request.method).toBe(stryMutAct_9fa48("362") ? "" : (stryCov_9fa48("362"), 'PUT'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("363") ? "" : (stryCov_9fa48("363"), 'should handle schedule error'), () => {
      if (stryMutAct_9fa48("364")) {
        {}
      } else {
        stryCov_9fa48("364");
        service.schedule({} as any).subscribe(stryMutAct_9fa48("365") ? {} : (stryCov_9fa48("365"), {
          error: error => {
            if (stryMutAct_9fa48("366")) {
              {}
            } else {
              stryCov_9fa48("366");
              expect(error.status).toBe(400);
            }
          }
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("367") ? `` : (stryCov_9fa48("367"), `${environment.apiUrl}/appointments/`));
        req.flush(stryMutAct_9fa48("368") ? "" : (stryCov_9fa48("368"), 'Bad request'), stryMutAct_9fa48("369") ? {} : (stryCov_9fa48("369"), {
          status: 400,
          statusText: stryMutAct_9fa48("370") ? "" : (stryCov_9fa48("370"), 'Bad Request')
        }));
      }
    });
    it(stryMutAct_9fa48("371") ? "" : (stryCov_9fa48("371"), 'should checkIn handles error'), () => {
      if (stryMutAct_9fa48("372")) {
        {}
      } else {
        stryCov_9fa48("372");
        service.checkIn(stryMutAct_9fa48("373") ? "" : (stryCov_9fa48("373"), 'apt-001')).subscribe(stryMutAct_9fa48("374") ? {} : (stryCov_9fa48("374"), {
          error: stryMutAct_9fa48("375") ? () => undefined : (stryCov_9fa48("375"), error => expect(error).toBeTruthy())
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("376") ? `` : (stryCov_9fa48("376"), `${environment.apiUrl}/appointments/apt-001/checkin`));
        req.flush(stryMutAct_9fa48("377") ? "" : (stryCov_9fa48("377"), 'Error'), stryMutAct_9fa48("378") ? {} : (stryCov_9fa48("378"), {
          status: 500,
          statusText: stryMutAct_9fa48("379") ? "" : (stryCov_9fa48("379"), 'Error')
        }));
      }
    });
    it(stryMutAct_9fa48("380") ? "" : (stryCov_9fa48("380"), 'should cancel with no reason'), () => {
      if (stryMutAct_9fa48("381")) {
        {}
      } else {
        stryCov_9fa48("381");
        service.cancel(stryMutAct_9fa48("382") ? "" : (stryCov_9fa48("382"), 'apt-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("383") ? `` : (stryCov_9fa48("383"), `${environment.apiUrl}/appointments/apt-001/cancel`));
        expect(req.request.body).toEqual(stryMutAct_9fa48("384") ? {} : (stryCov_9fa48("384"), {
          reason: undefined
        }));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("385") ? "" : (stryCov_9fa48("385"), 'should search with pagination'), () => {
      if (stryMutAct_9fa48("386")) {
        {}
      } else {
        stryCov_9fa48("386");
        service.search(stryMutAct_9fa48("387") ? "" : (stryCov_9fa48("387"), 'test'), 2, 50).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("388") ? () => undefined : (stryCov_9fa48("388"), r => stryMutAct_9fa48("391") ? r.params.get('q') === 'test' && r.params.get('page') === '2' || r.params.get('pageSize') === '50' : stryMutAct_9fa48("390") ? false : stryMutAct_9fa48("389") ? true : (stryCov_9fa48("389", "390", "391"), (stryMutAct_9fa48("393") ? r.params.get('q') === 'test' || r.params.get('page') === '2' : stryMutAct_9fa48("392") ? true : (stryCov_9fa48("392", "393"), (stryMutAct_9fa48("395") ? r.params.get('q') !== 'test' : stryMutAct_9fa48("394") ? true : (stryCov_9fa48("394", "395"), r.params.get(stryMutAct_9fa48("396") ? "" : (stryCov_9fa48("396"), 'q')) === (stryMutAct_9fa48("397") ? "" : (stryCov_9fa48("397"), 'test')))) && (stryMutAct_9fa48("399") ? r.params.get('page') !== '2' : stryMutAct_9fa48("398") ? true : (stryCov_9fa48("398", "399"), r.params.get(stryMutAct_9fa48("400") ? "" : (stryCov_9fa48("400"), 'page')) === (stryMutAct_9fa48("401") ? "" : (stryCov_9fa48("401"), '2')))))) && (stryMutAct_9fa48("403") ? r.params.get('pageSize') !== '50' : stryMutAct_9fa48("402") ? true : (stryCov_9fa48("402", "403"), r.params.get(stryMutAct_9fa48("404") ? "" : (stryCov_9fa48("404"), 'pageSize')) === (stryMutAct_9fa48("405") ? "" : (stryCov_9fa48("405"), '50')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("406") ? "" : (stryCov_9fa48("406"), 'GET'));
        req.flush(stryMutAct_9fa48("407") ? {} : (stryCov_9fa48("407"), {
          items: stryMutAct_9fa48("408") ? ["Stryker was here"] : (stryCov_9fa48("408"), []),
          totalCount: 0,
          page: 2,
          pageSize: 50,
          hasNextPage: stryMutAct_9fa48("409") ? true : (stryCov_9fa48("409"), false),
          hasPreviousPage: stryMutAct_9fa48("410") ? true : (stryCov_9fa48("410"), false)
        }));
      }
    });
    it(stryMutAct_9fa48("411") ? "" : (stryCov_9fa48("411"), 'should list with default params'), () => {
      if (stryMutAct_9fa48("412")) {
        {}
      } else {
        stryCov_9fa48("412");
        service.list().subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("413") ? () => undefined : (stryCov_9fa48("413"), r => stryMutAct_9fa48("416") ? r.params.get('page') !== '1' : stryMutAct_9fa48("415") ? false : stryMutAct_9fa48("414") ? true : (stryCov_9fa48("414", "415", "416"), r.params.get(stryMutAct_9fa48("417") ? "" : (stryCov_9fa48("417"), 'page')) === (stryMutAct_9fa48("418") ? "" : (stryCov_9fa48("418"), '1')))));
        expect(req.request.method).toBe(stryMutAct_9fa48("419") ? "" : (stryCov_9fa48("419"), 'GET'));
        req.flush(stryMutAct_9fa48("420") ? {} : (stryCov_9fa48("420"), {
          items: stryMutAct_9fa48("421") ? ["Stryker was here"] : (stryCov_9fa48("421"), []),
          totalCount: 0,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("422") ? true : (stryCov_9fa48("422"), false),
          hasPreviousPage: stryMutAct_9fa48("423") ? true : (stryCov_9fa48("423"), false)
        }));
      }
    });
  }
});