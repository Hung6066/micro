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
import { BillingService } from './billing.service';
import { environment } from '@env/environment';
describe(stryMutAct_9fa48("717") ? "" : (stryCov_9fa48("717"), 'BillingService'), () => {
  if (stryMutAct_9fa48("718")) {
    {}
  } else {
    stryCov_9fa48("718");
    let service: BillingService;
    let httpMock: HttpTestingController;
    beforeEach(() => {
      if (stryMutAct_9fa48("719")) {
        {}
      } else {
        stryCov_9fa48("719");
        TestBed.configureTestingModule(stryMutAct_9fa48("720") ? {} : (stryCov_9fa48("720"), {
          imports: stryMutAct_9fa48("721") ? [] : (stryCov_9fa48("721"), [HttpClientTestingModule])
        }));
        service = TestBed.inject(BillingService);
        httpMock = TestBed.inject(HttpTestingController);
      }
    });
    afterEach(() => {
      if (stryMutAct_9fa48("722")) {
        {}
      } else {
        stryCov_9fa48("722");
        httpMock.verify();
      }
    });
    it(stryMutAct_9fa48("723") ? "" : (stryCov_9fa48("723"), 'should search invoices'), () => {
      if (stryMutAct_9fa48("724")) {
        {}
      } else {
        stryCov_9fa48("724");
        const mockResult = stryMutAct_9fa48("725") ? {} : (stryCov_9fa48("725"), {
          items: stryMutAct_9fa48("726") ? [] : (stryCov_9fa48("726"), [stryMutAct_9fa48("727") ? {} : (stryCov_9fa48("727"), {
            id: stryMutAct_9fa48("728") ? "" : (stryCov_9fa48("728"), 'inv-001'),
            invoiceNumber: stryMutAct_9fa48("729") ? "" : (stryCov_9fa48("729"), 'INV-001')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("730") ? true : (stryCov_9fa48("730"), false),
          hasPreviousPage: stryMutAct_9fa48("731") ? true : (stryCov_9fa48("731"), false)
        });
        service.searchInvoices(stryMutAct_9fa48("732") ? {} : (stryCov_9fa48("732"), {
          searchTerm: stryMutAct_9fa48("733") ? "" : (stryCov_9fa48("733"), 'INV')
        })).subscribe(result => {
          if (stryMutAct_9fa48("734")) {
            {}
          } else {
            stryCov_9fa48("734");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("735") ? () => undefined : (stryCov_9fa48("735"), r => stryMutAct_9fa48("738") ? r.url === `${environment.apiUrl}/invoices/search` || r.params.get('q') === 'INV' : stryMutAct_9fa48("737") ? false : stryMutAct_9fa48("736") ? true : (stryCov_9fa48("736", "737", "738"), (stryMutAct_9fa48("740") ? r.url !== `${environment.apiUrl}/invoices/search` : stryMutAct_9fa48("739") ? true : (stryCov_9fa48("739", "740"), r.url === (stryMutAct_9fa48("741") ? `` : (stryCov_9fa48("741"), `${environment.apiUrl}/invoices/search`)))) && (stryMutAct_9fa48("743") ? r.params.get('q') !== 'INV' : stryMutAct_9fa48("742") ? true : (stryCov_9fa48("742", "743"), r.params.get(stryMutAct_9fa48("744") ? "" : (stryCov_9fa48("744"), 'q')) === (stryMutAct_9fa48("745") ? "" : (stryCov_9fa48("745"), 'INV')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("746") ? "" : (stryCov_9fa48("746"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("747") ? "" : (stryCov_9fa48("747"), 'should get invoice by id'), () => {
      if (stryMutAct_9fa48("748")) {
        {}
      } else {
        stryCov_9fa48("748");
        service.getInvoice(stryMutAct_9fa48("749") ? "" : (stryCov_9fa48("749"), 'inv-001')).subscribe(inv => {
          if (stryMutAct_9fa48("750")) {
            {}
          } else {
            stryCov_9fa48("750");
            expect(inv.id).toBe(stryMutAct_9fa48("751") ? "" : (stryCov_9fa48("751"), 'inv-001'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("752") ? `` : (stryCov_9fa48("752"), `${environment.apiUrl}/invoices/inv-001`));
        expect(req.request.method).toBe(stryMutAct_9fa48("753") ? "" : (stryCov_9fa48("753"), 'GET'));
        req.flush(stryMutAct_9fa48("754") ? {} : (stryCov_9fa48("754"), {
          id: stryMutAct_9fa48("755") ? "" : (stryCov_9fa48("755"), 'inv-001')
        }));
      }
    });
    it(stryMutAct_9fa48("756") ? "" : (stryCov_9fa48("756"), 'should create invoice'), () => {
      if (stryMutAct_9fa48("757")) {
        {}
      } else {
        stryCov_9fa48("757");
        const data = stryMutAct_9fa48("758") ? {} : (stryCov_9fa48("758"), {
          patientId: stryMutAct_9fa48("759") ? "" : (stryCov_9fa48("759"), 'pat-001'),
          lineItems: stryMutAct_9fa48("760") ? ["Stryker was here"] : (stryCov_9fa48("760"), [])
        });
        service.createInvoice(data as any).subscribe(inv => {
          if (stryMutAct_9fa48("761")) {
            {}
          } else {
            stryCov_9fa48("761");
            expect(inv.id).toBe(stryMutAct_9fa48("762") ? "" : (stryCov_9fa48("762"), 'inv-002'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("763") ? `` : (stryCov_9fa48("763"), `${environment.apiUrl}/invoices/`));
        expect(req.request.method).toBe(stryMutAct_9fa48("764") ? "" : (stryCov_9fa48("764"), 'POST'));
        req.flush(stryMutAct_9fa48("765") ? {} : (stryCov_9fa48("765"), {
          id: stryMutAct_9fa48("766") ? "" : (stryCov_9fa48("766"), 'inv-002'),
          ...data
        }));
      }
    });
    it(stryMutAct_9fa48("767") ? "" : (stryCov_9fa48("767"), 'should record payment'), () => {
      if (stryMutAct_9fa48("768")) {
        {}
      } else {
        stryCov_9fa48("768");
        const data = stryMutAct_9fa48("769") ? {} : (stryCov_9fa48("769"), {
          amount: 50000,
          paymentMethod: stryMutAct_9fa48("770") ? "" : (stryCov_9fa48("770"), 'cash')
        });
        service.recordPayment(stryMutAct_9fa48("771") ? "" : (stryCov_9fa48("771"), 'inv-001'), data as any).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("772") ? `` : (stryCov_9fa48("772"), `${environment.apiUrl}/invoices/inv-001/payments`));
        expect(req.request.method).toBe(stryMutAct_9fa48("773") ? "" : (stryCov_9fa48("773"), 'POST'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("774") ? "" : (stryCov_9fa48("774"), 'should void invoice'), () => {
      if (stryMutAct_9fa48("775")) {
        {}
      } else {
        stryCov_9fa48("775");
        service.voidInvoice(stryMutAct_9fa48("776") ? "" : (stryCov_9fa48("776"), 'inv-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("777") ? `` : (stryCov_9fa48("777"), `${environment.apiUrl}/invoices/inv-001/void`));
        expect(req.request.method).toBe(stryMutAct_9fa48("778") ? "" : (stryCov_9fa48("778"), 'POST'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("779") ? "" : (stryCov_9fa48("779"), 'should get invoice by number'), () => {
      if (stryMutAct_9fa48("780")) {
        {}
      } else {
        stryCov_9fa48("780");
        service.getInvoiceByNumber(stryMutAct_9fa48("781") ? "" : (stryCov_9fa48("781"), 'INV-001')).subscribe(inv => {
          if (stryMutAct_9fa48("782")) {
            {}
          } else {
            stryCov_9fa48("782");
            expect(inv.id).toBe(stryMutAct_9fa48("783") ? "" : (stryCov_9fa48("783"), 'inv-001'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("784") ? `` : (stryCov_9fa48("784"), `${environment.apiUrl}/invoices/number/INV-001`));
        expect(req.request.method).toBe(stryMutAct_9fa48("785") ? "" : (stryCov_9fa48("785"), 'GET'));
        req.flush(stryMutAct_9fa48("786") ? {} : (stryCov_9fa48("786"), {
          id: stryMutAct_9fa48("787") ? "" : (stryCov_9fa48("787"), 'inv-001'),
          invoiceNumber: stryMutAct_9fa48("788") ? "" : (stryCov_9fa48("788"), 'INV-001')
        }));
      }
    });
    it(stryMutAct_9fa48("789") ? "" : (stryCov_9fa48("789"), 'should get patient invoices'), () => {
      if (stryMutAct_9fa48("790")) {
        {}
      } else {
        stryCov_9fa48("790");
        const mockResult = stryMutAct_9fa48("791") ? {} : (stryCov_9fa48("791"), {
          items: stryMutAct_9fa48("792") ? [] : (stryCov_9fa48("792"), [stryMutAct_9fa48("793") ? {} : (stryCov_9fa48("793"), {
            id: stryMutAct_9fa48("794") ? "" : (stryCov_9fa48("794"), 'inv-001'),
            invoiceNumber: stryMutAct_9fa48("795") ? "" : (stryCov_9fa48("795"), 'INV-001')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("796") ? true : (stryCov_9fa48("796"), false),
          hasPreviousPage: stryMutAct_9fa48("797") ? true : (stryCov_9fa48("797"), false)
        });
        service.getPatientInvoices(stryMutAct_9fa48("798") ? "" : (stryCov_9fa48("798"), 'pat-001')).subscribe(result => {
          if (stryMutAct_9fa48("799")) {
            {}
          } else {
            stryCov_9fa48("799");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("800") ? () => undefined : (stryCov_9fa48("800"), r => stryMutAct_9fa48("803") ? r.url === `${environment.apiUrl}/invoices/search` || r.params.get('patientId') === 'pat-001' : stryMutAct_9fa48("802") ? false : stryMutAct_9fa48("801") ? true : (stryCov_9fa48("801", "802", "803"), (stryMutAct_9fa48("805") ? r.url !== `${environment.apiUrl}/invoices/search` : stryMutAct_9fa48("804") ? true : (stryCov_9fa48("804", "805"), r.url === (stryMutAct_9fa48("806") ? `` : (stryCov_9fa48("806"), `${environment.apiUrl}/invoices/search`)))) && (stryMutAct_9fa48("808") ? r.params.get('patientId') !== 'pat-001' : stryMutAct_9fa48("807") ? true : (stryCov_9fa48("807", "808"), r.params.get(stryMutAct_9fa48("809") ? "" : (stryCov_9fa48("809"), 'patientId')) === (stryMutAct_9fa48("810") ? "" : (stryCov_9fa48("810"), 'pat-001')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("811") ? "" : (stryCov_9fa48("811"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("812") ? "" : (stryCov_9fa48("812"), 'should void invoice handles error'), () => {
      if (stryMutAct_9fa48("813")) {
        {}
      } else {
        stryCov_9fa48("813");
        service.voidInvoice(stryMutAct_9fa48("814") ? "" : (stryCov_9fa48("814"), 'inv-001')).subscribe(stryMutAct_9fa48("815") ? {} : (stryCov_9fa48("815"), {
          error: stryMutAct_9fa48("816") ? () => undefined : (stryCov_9fa48("816"), error => expect(error).toBeTruthy())
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("817") ? `` : (stryCov_9fa48("817"), `${environment.apiUrl}/invoices/inv-001/void`));
        req.flush(stryMutAct_9fa48("818") ? "" : (stryCov_9fa48("818"), 'Error'), stryMutAct_9fa48("819") ? {} : (stryCov_9fa48("819"), {
          status: 500,
          statusText: stryMutAct_9fa48("820") ? "" : (stryCov_9fa48("820"), 'Error')
        }));
      }
    });
    it(stryMutAct_9fa48("821") ? "" : (stryCov_9fa48("821"), 'should search invoices with filters'), () => {
      if (stryMutAct_9fa48("822")) {
        {}
      } else {
        stryCov_9fa48("822");
        service.searchInvoices(stryMutAct_9fa48("823") ? {} : (stryCov_9fa48("823"), {
          searchTerm: stryMutAct_9fa48("824") ? "" : (stryCov_9fa48("824"), 'test'),
          statusCode: stryMutAct_9fa48("825") ? "" : (stryCov_9fa48("825"), 'PAID'),
          page: 1,
          pageSize: 10
        })).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("826") ? () => undefined : (stryCov_9fa48("826"), r => stryMutAct_9fa48("829") ? r.params.get('q') === 'test' || r.params.get('statusCode') === 'PAID' : stryMutAct_9fa48("828") ? false : stryMutAct_9fa48("827") ? true : (stryCov_9fa48("827", "828", "829"), (stryMutAct_9fa48("831") ? r.params.get('q') !== 'test' : stryMutAct_9fa48("830") ? true : (stryCov_9fa48("830", "831"), r.params.get(stryMutAct_9fa48("832") ? "" : (stryCov_9fa48("832"), 'q')) === (stryMutAct_9fa48("833") ? "" : (stryCov_9fa48("833"), 'test')))) && (stryMutAct_9fa48("835") ? r.params.get('statusCode') !== 'PAID' : stryMutAct_9fa48("834") ? true : (stryCov_9fa48("834", "835"), r.params.get(stryMutAct_9fa48("836") ? "" : (stryCov_9fa48("836"), 'statusCode')) === (stryMutAct_9fa48("837") ? "" : (stryCov_9fa48("837"), 'PAID')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("838") ? "" : (stryCov_9fa48("838"), 'GET'));
        req.flush(stryMutAct_9fa48("839") ? {} : (stryCov_9fa48("839"), {
          items: stryMutAct_9fa48("840") ? ["Stryker was here"] : (stryCov_9fa48("840"), []),
          totalCount: 0,
          page: 1,
          pageSize: 10,
          hasNextPage: stryMutAct_9fa48("841") ? true : (stryCov_9fa48("841"), false),
          hasPreviousPage: stryMutAct_9fa48("842") ? true : (stryCov_9fa48("842"), false)
        }));
      }
    });
  }
});