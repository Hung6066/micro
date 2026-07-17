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
import { LabService } from './lab.service';
import { environment } from '@env/environment';
describe(stryMutAct_9fa48("1167") ? "" : (stryCov_9fa48("1167"), 'LabService'), () => {
  if (stryMutAct_9fa48("1168")) {
    {}
  } else {
    stryCov_9fa48("1168");
    let service: LabService;
    let httpMock: HttpTestingController;
    beforeEach(() => {
      if (stryMutAct_9fa48("1169")) {
        {}
      } else {
        stryCov_9fa48("1169");
        TestBed.configureTestingModule(stryMutAct_9fa48("1170") ? {} : (stryCov_9fa48("1170"), {
          imports: stryMutAct_9fa48("1171") ? [] : (stryCov_9fa48("1171"), [HttpClientTestingModule])
        }));
        service = TestBed.inject(LabService);
        httpMock = TestBed.inject(HttpTestingController);
      }
    });
    afterEach(() => {
      if (stryMutAct_9fa48("1172")) {
        {}
      } else {
        stryCov_9fa48("1172");
        httpMock.verify();
      }
    });
    it(stryMutAct_9fa48("1173") ? "" : (stryCov_9fa48("1173"), 'should search lab orders'), () => {
      if (stryMutAct_9fa48("1174")) {
        {}
      } else {
        stryCov_9fa48("1174");
        const mockResult = stryMutAct_9fa48("1175") ? {} : (stryCov_9fa48("1175"), {
          items: stryMutAct_9fa48("1176") ? [] : (stryCov_9fa48("1176"), [stryMutAct_9fa48("1177") ? {} : (stryCov_9fa48("1177"), {
            id: stryMutAct_9fa48("1178") ? "" : (stryCov_9fa48("1178"), 'lab-001'),
            testName: stryMutAct_9fa48("1179") ? "" : (stryCov_9fa48("1179"), 'CBC')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1180") ? true : (stryCov_9fa48("1180"), false),
          hasPreviousPage: stryMutAct_9fa48("1181") ? true : (stryCov_9fa48("1181"), false)
        });
        service.searchLabOrders(stryMutAct_9fa48("1182") ? {} : (stryCov_9fa48("1182"), {
          searchTerm: stryMutAct_9fa48("1183") ? "" : (stryCov_9fa48("1183"), 'CBC')
        })).subscribe(result => {
          if (stryMutAct_9fa48("1184")) {
            {}
          } else {
            stryCov_9fa48("1184");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1185") ? () => undefined : (stryCov_9fa48("1185"), r => stryMutAct_9fa48("1188") ? r.url === `${environment.apiUrl}/lab-orders/search` || r.params.get('q') === 'CBC' : stryMutAct_9fa48("1187") ? false : stryMutAct_9fa48("1186") ? true : (stryCov_9fa48("1186", "1187", "1188"), (stryMutAct_9fa48("1190") ? r.url !== `${environment.apiUrl}/lab-orders/search` : stryMutAct_9fa48("1189") ? true : (stryCov_9fa48("1189", "1190"), r.url === (stryMutAct_9fa48("1191") ? `` : (stryCov_9fa48("1191"), `${environment.apiUrl}/lab-orders/search`)))) && (stryMutAct_9fa48("1193") ? r.params.get('q') !== 'CBC' : stryMutAct_9fa48("1192") ? true : (stryCov_9fa48("1192", "1193"), r.params.get(stryMutAct_9fa48("1194") ? "" : (stryCov_9fa48("1194"), 'q')) === (stryMutAct_9fa48("1195") ? "" : (stryCov_9fa48("1195"), 'CBC')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1196") ? "" : (stryCov_9fa48("1196"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("1197") ? "" : (stryCov_9fa48("1197"), 'should get lab order by id'), () => {
      if (stryMutAct_9fa48("1198")) {
        {}
      } else {
        stryCov_9fa48("1198");
        service.getLabOrder(stryMutAct_9fa48("1199") ? "" : (stryCov_9fa48("1199"), 'lab-001')).subscribe(order => {
          if (stryMutAct_9fa48("1200")) {
            {}
          } else {
            stryCov_9fa48("1200");
            expect(order.id).toBe(stryMutAct_9fa48("1201") ? "" : (stryCov_9fa48("1201"), 'lab-001'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1202") ? `` : (stryCov_9fa48("1202"), `${environment.apiUrl}/lab-orders/lab-001`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1203") ? "" : (stryCov_9fa48("1203"), 'GET'));
        req.flush(stryMutAct_9fa48("1204") ? {} : (stryCov_9fa48("1204"), {
          id: stryMutAct_9fa48("1205") ? "" : (stryCov_9fa48("1205"), 'lab-001')
        }));
      }
    });
    it(stryMutAct_9fa48("1206") ? "" : (stryCov_9fa48("1206"), 'should create lab order'), () => {
      if (stryMutAct_9fa48("1207")) {
        {}
      } else {
        stryCov_9fa48("1207");
        const data = stryMutAct_9fa48("1208") ? {} : (stryCov_9fa48("1208"), {
          patientId: stryMutAct_9fa48("1209") ? "" : (stryCov_9fa48("1209"), 'pat-001'),
          testCode: stryMutAct_9fa48("1210") ? "" : (stryCov_9fa48("1210"), 'CBC')
        });
        service.createLabOrder(data as any).subscribe(order => {
          if (stryMutAct_9fa48("1211")) {
            {}
          } else {
            stryCov_9fa48("1211");
            expect(order.id).toBe(stryMutAct_9fa48("1212") ? "" : (stryCov_9fa48("1212"), 'lab-002'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1213") ? `` : (stryCov_9fa48("1213"), `${environment.apiUrl}/lab-orders/`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1214") ? "" : (stryCov_9fa48("1214"), 'POST'));
        req.flush(stryMutAct_9fa48("1215") ? {} : (stryCov_9fa48("1215"), {
          id: stryMutAct_9fa48("1216") ? "" : (stryCov_9fa48("1216"), 'lab-002'),
          ...data
        }));
      }
    });
    it(stryMutAct_9fa48("1217") ? "" : (stryCov_9fa48("1217"), 'should submit lab order'), () => {
      if (stryMutAct_9fa48("1218")) {
        {}
      } else {
        stryCov_9fa48("1218");
        service.submitLabOrder(stryMutAct_9fa48("1219") ? "" : (stryCov_9fa48("1219"), 'lab-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1220") ? `` : (stryCov_9fa48("1220"), `${environment.apiUrl}/lab-orders/lab-001/submit`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1221") ? "" : (stryCov_9fa48("1221"), 'POST'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("1222") ? "" : (stryCov_9fa48("1222"), 'should collect specimen'), () => {
      if (stryMutAct_9fa48("1223")) {
        {}
      } else {
        stryCov_9fa48("1223");
        service.collectSpecimen(stryMutAct_9fa48("1224") ? "" : (stryCov_9fa48("1224"), 'lab-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1225") ? `` : (stryCov_9fa48("1225"), `${environment.apiUrl}/lab-orders/lab-001/collect`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1226") ? "" : (stryCov_9fa48("1226"), 'POST'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("1227") ? "" : (stryCov_9fa48("1227"), 'should cancel lab order'), () => {
      if (stryMutAct_9fa48("1228")) {
        {}
      } else {
        stryCov_9fa48("1228");
        service.cancelLabOrder(stryMutAct_9fa48("1229") ? "" : (stryCov_9fa48("1229"), 'lab-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1230") ? `` : (stryCov_9fa48("1230"), `${environment.apiUrl}/lab-orders/lab-001/cancel`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1231") ? "" : (stryCov_9fa48("1231"), 'POST'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("1232") ? "" : (stryCov_9fa48("1232"), 'should record lab result'), () => {
      if (stryMutAct_9fa48("1233")) {
        {}
      } else {
        stryCov_9fa48("1233");
        const data = stryMutAct_9fa48("1234") ? {} : (stryCov_9fa48("1234"), {
          value: stryMutAct_9fa48("1235") ? "" : (stryCov_9fa48("1235"), '5.5'),
          unit: stryMutAct_9fa48("1236") ? "" : (stryCov_9fa48("1236"), 'g/dL')
        });
        service.recordResult(stryMutAct_9fa48("1237") ? "" : (stryCov_9fa48("1237"), 'lab-001'), data as any).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1238") ? `` : (stryCov_9fa48("1238"), `${environment.apiUrl}/lab-orders/lab-001/result`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1239") ? "" : (stryCov_9fa48("1239"), 'POST'));
        expect(req.request.body).toEqual(data);
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("1240") ? "" : (stryCov_9fa48("1240"), 'should get patient lab orders'), () => {
      if (stryMutAct_9fa48("1241")) {
        {}
      } else {
        stryCov_9fa48("1241");
        const mockResult = stryMutAct_9fa48("1242") ? {} : (stryCov_9fa48("1242"), {
          items: stryMutAct_9fa48("1243") ? [] : (stryCov_9fa48("1243"), [stryMutAct_9fa48("1244") ? {} : (stryCov_9fa48("1244"), {
            id: stryMutAct_9fa48("1245") ? "" : (stryCov_9fa48("1245"), 'lab-001'),
            testName: stryMutAct_9fa48("1246") ? "" : (stryCov_9fa48("1246"), 'CBC')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1247") ? true : (stryCov_9fa48("1247"), false),
          hasPreviousPage: stryMutAct_9fa48("1248") ? true : (stryCov_9fa48("1248"), false)
        });
        service.getPatientLabOrders(stryMutAct_9fa48("1249") ? "" : (stryCov_9fa48("1249"), 'pat-001')).subscribe(result => {
          if (stryMutAct_9fa48("1250")) {
            {}
          } else {
            stryCov_9fa48("1250");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1251") ? () => undefined : (stryCov_9fa48("1251"), r => stryMutAct_9fa48("1254") ? r.url === `${environment.apiUrl}/lab-orders/search` || r.params.get('patientId') === 'pat-001' : stryMutAct_9fa48("1253") ? false : stryMutAct_9fa48("1252") ? true : (stryCov_9fa48("1252", "1253", "1254"), (stryMutAct_9fa48("1256") ? r.url !== `${environment.apiUrl}/lab-orders/search` : stryMutAct_9fa48("1255") ? true : (stryCov_9fa48("1255", "1256"), r.url === (stryMutAct_9fa48("1257") ? `` : (stryCov_9fa48("1257"), `${environment.apiUrl}/lab-orders/search`)))) && (stryMutAct_9fa48("1259") ? r.params.get('patientId') !== 'pat-001' : stryMutAct_9fa48("1258") ? true : (stryCov_9fa48("1258", "1259"), r.params.get(stryMutAct_9fa48("1260") ? "" : (stryCov_9fa48("1260"), 'patientId')) === (stryMutAct_9fa48("1261") ? "" : (stryCov_9fa48("1261"), 'pat-001')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1262") ? "" : (stryCov_9fa48("1262"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("1263") ? "" : (stryCov_9fa48("1263"), 'should search with filters'), () => {
      if (stryMutAct_9fa48("1264")) {
        {}
      } else {
        stryCov_9fa48("1264");
        service.searchLabOrders(stryMutAct_9fa48("1265") ? {} : (stryCov_9fa48("1265"), {
          statusCode: stryMutAct_9fa48("1266") ? "" : (stryCov_9fa48("1266"), 'PENDING'),
          page: 1,
          pageSize: 10
        })).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1267") ? () => undefined : (stryCov_9fa48("1267"), r => stryMutAct_9fa48("1270") ? r.params.get('statusCode') === 'PENDING' || r.params.get('page') === '1' : stryMutAct_9fa48("1269") ? false : stryMutAct_9fa48("1268") ? true : (stryCov_9fa48("1268", "1269", "1270"), (stryMutAct_9fa48("1272") ? r.params.get('statusCode') !== 'PENDING' : stryMutAct_9fa48("1271") ? true : (stryCov_9fa48("1271", "1272"), r.params.get(stryMutAct_9fa48("1273") ? "" : (stryCov_9fa48("1273"), 'statusCode')) === (stryMutAct_9fa48("1274") ? "" : (stryCov_9fa48("1274"), 'PENDING')))) && (stryMutAct_9fa48("1276") ? r.params.get('page') !== '1' : stryMutAct_9fa48("1275") ? true : (stryCov_9fa48("1275", "1276"), r.params.get(stryMutAct_9fa48("1277") ? "" : (stryCov_9fa48("1277"), 'page')) === (stryMutAct_9fa48("1278") ? "" : (stryCov_9fa48("1278"), '1')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1279") ? "" : (stryCov_9fa48("1279"), 'GET'));
        req.flush(stryMutAct_9fa48("1280") ? {} : (stryCov_9fa48("1280"), {
          items: stryMutAct_9fa48("1281") ? ["Stryker was here"] : (stryCov_9fa48("1281"), []),
          totalCount: 0,
          page: 1,
          pageSize: 10,
          hasNextPage: stryMutAct_9fa48("1282") ? true : (stryCov_9fa48("1282"), false),
          hasPreviousPage: stryMutAct_9fa48("1283") ? true : (stryCov_9fa48("1283"), false)
        }));
      }
    });
    it(stryMutAct_9fa48("1284") ? "" : (stryCov_9fa48("1284"), 'should handle submit error'), () => {
      if (stryMutAct_9fa48("1285")) {
        {}
      } else {
        stryCov_9fa48("1285");
        service.submitLabOrder(stryMutAct_9fa48("1286") ? "" : (stryCov_9fa48("1286"), 'lab-001')).subscribe(stryMutAct_9fa48("1287") ? {} : (stryCov_9fa48("1287"), {
          error: stryMutAct_9fa48("1288") ? () => undefined : (stryCov_9fa48("1288"), error => expect(error).toBeTruthy())
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("1289") ? `` : (stryCov_9fa48("1289"), `${environment.apiUrl}/lab-orders/lab-001/submit`));
        req.flush(stryMutAct_9fa48("1290") ? "" : (stryCov_9fa48("1290"), 'Error'), stryMutAct_9fa48("1291") ? {} : (stryCov_9fa48("1291"), {
          status: 500,
          statusText: stryMutAct_9fa48("1292") ? "" : (stryCov_9fa48("1292"), 'Error')
        }));
      }
    });
    it(stryMutAct_9fa48("1293") ? "" : (stryCov_9fa48("1293"), 'should create lab order handles error'), () => {
      if (stryMutAct_9fa48("1294")) {
        {}
      } else {
        stryCov_9fa48("1294");
        service.createLabOrder({} as any).subscribe(stryMutAct_9fa48("1295") ? {} : (stryCov_9fa48("1295"), {
          error: stryMutAct_9fa48("1296") ? () => undefined : (stryCov_9fa48("1296"), error => expect(error).toBeTruthy())
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("1297") ? `` : (stryCov_9fa48("1297"), `${environment.apiUrl}/lab-orders/`));
        req.flush(stryMutAct_9fa48("1298") ? "" : (stryCov_9fa48("1298"), 'Error'), stryMutAct_9fa48("1299") ? {} : (stryCov_9fa48("1299"), {
          status: 400,
          statusText: stryMutAct_9fa48("1300") ? "" : (stryCov_9fa48("1300"), 'Bad Request')
        }));
      }
    });
    it(stryMutAct_9fa48("1301") ? "" : (stryCov_9fa48("1301"), 'should search with patientId filter'), () => {
      if (stryMutAct_9fa48("1302")) {
        {}
      } else {
        stryCov_9fa48("1302");
        service.searchLabOrders(stryMutAct_9fa48("1303") ? {} : (stryCov_9fa48("1303"), {
          patientId: stryMutAct_9fa48("1304") ? "" : (stryCov_9fa48("1304"), 'pat-001')
        })).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1305") ? () => undefined : (stryCov_9fa48("1305"), r => stryMutAct_9fa48("1308") ? r.params.get('patientId') !== 'pat-001' : stryMutAct_9fa48("1307") ? false : stryMutAct_9fa48("1306") ? true : (stryCov_9fa48("1306", "1307", "1308"), r.params.get(stryMutAct_9fa48("1309") ? "" : (stryCov_9fa48("1309"), 'patientId')) === (stryMutAct_9fa48("1310") ? "" : (stryCov_9fa48("1310"), 'pat-001')))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1311") ? "" : (stryCov_9fa48("1311"), 'GET'));
        req.flush(stryMutAct_9fa48("1312") ? {} : (stryCov_9fa48("1312"), {
          items: stryMutAct_9fa48("1313") ? ["Stryker was here"] : (stryCov_9fa48("1313"), []),
          totalCount: 0,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1314") ? true : (stryCov_9fa48("1314"), false),
          hasPreviousPage: stryMutAct_9fa48("1315") ? true : (stryCov_9fa48("1315"), false)
        }));
      }
    });
    it(stryMutAct_9fa48("1316") ? "" : (stryCov_9fa48("1316"), 'should submit lab order handles error'), () => {
      if (stryMutAct_9fa48("1317")) {
        {}
      } else {
        stryCov_9fa48("1317");
        service.submitLabOrder(stryMutAct_9fa48("1318") ? "" : (stryCov_9fa48("1318"), 'lab-001')).subscribe(stryMutAct_9fa48("1319") ? {} : (stryCov_9fa48("1319"), {
          error: stryMutAct_9fa48("1320") ? () => undefined : (stryCov_9fa48("1320"), e => expect(e).toBeTruthy())
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("1321") ? `` : (stryCov_9fa48("1321"), `${environment.apiUrl}/lab-orders/lab-001/submit`));
        req.flush(stryMutAct_9fa48("1322") ? "" : (stryCov_9fa48("1322"), 'Error'), stryMutAct_9fa48("1323") ? {} : (stryCov_9fa48("1323"), {
          status: 500,
          statusText: stryMutAct_9fa48("1324") ? "" : (stryCov_9fa48("1324"), 'Internal Server Error')
        }));
      }
    });
  }
});