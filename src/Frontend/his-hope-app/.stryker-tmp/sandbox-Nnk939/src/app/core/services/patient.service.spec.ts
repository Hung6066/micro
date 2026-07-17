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
import { PatientService } from './patient.service';
import { environment } from '@env/environment';
describe(stryMutAct_9fa48("1370") ? "" : (stryCov_9fa48("1370"), 'PatientService'), () => {
  if (stryMutAct_9fa48("1371")) {
    {}
  } else {
    stryCov_9fa48("1371");
    let service: PatientService;
    let httpMock: HttpTestingController;
    beforeEach(() => {
      if (stryMutAct_9fa48("1372")) {
        {}
      } else {
        stryCov_9fa48("1372");
        TestBed.configureTestingModule(stryMutAct_9fa48("1373") ? {} : (stryCov_9fa48("1373"), {
          imports: stryMutAct_9fa48("1374") ? [] : (stryCov_9fa48("1374"), [HttpClientTestingModule])
        }));
        service = TestBed.inject(PatientService);
        httpMock = TestBed.inject(HttpTestingController);
      }
    });
    afterEach(() => {
      if (stryMutAct_9fa48("1375")) {
        {}
      } else {
        stryCov_9fa48("1375");
        httpMock.verify();
      }
    });
    it(stryMutAct_9fa48("1376") ? "" : (stryCov_9fa48("1376"), 'should search patients with paged results'), () => {
      if (stryMutAct_9fa48("1377")) {
        {}
      } else {
        stryCov_9fa48("1377");
        const mockResult = stryMutAct_9fa48("1378") ? {} : (stryCov_9fa48("1378"), {
          items: stryMutAct_9fa48("1379") ? [] : (stryCov_9fa48("1379"), [stryMutAct_9fa48("1380") ? {} : (stryCov_9fa48("1380"), {
            id: stryMutAct_9fa48("1381") ? "" : (stryCov_9fa48("1381"), 'pat-001'),
            fullName: stryMutAct_9fa48("1382") ? "" : (stryCov_9fa48("1382"), 'Test Patient')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1383") ? true : (stryCov_9fa48("1383"), false),
          hasPreviousPage: stryMutAct_9fa48("1384") ? true : (stryCov_9fa48("1384"), false)
        });
        service.search(stryMutAct_9fa48("1385") ? "" : (stryCov_9fa48("1385"), 'test'), 1, 20).subscribe(result => {
          if (stryMutAct_9fa48("1386")) {
            {}
          } else {
            stryCov_9fa48("1386");
            expect(result.items.length).toBe(1);
            expect(result.totalCount).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1387") ? () => undefined : (stryCov_9fa48("1387"), r => stryMutAct_9fa48("1390") ? r.url === `${environment.apiUrl}/patients/search` || r.params.get('q') === 'test' : stryMutAct_9fa48("1389") ? false : stryMutAct_9fa48("1388") ? true : (stryCov_9fa48("1388", "1389", "1390"), (stryMutAct_9fa48("1392") ? r.url !== `${environment.apiUrl}/patients/search` : stryMutAct_9fa48("1391") ? true : (stryCov_9fa48("1391", "1392"), r.url === (stryMutAct_9fa48("1393") ? `` : (stryCov_9fa48("1393"), `${environment.apiUrl}/patients/search`)))) && (stryMutAct_9fa48("1395") ? r.params.get('q') !== 'test' : stryMutAct_9fa48("1394") ? true : (stryCov_9fa48("1394", "1395"), r.params.get(stryMutAct_9fa48("1396") ? "" : (stryCov_9fa48("1396"), 'q')) === (stryMutAct_9fa48("1397") ? "" : (stryCov_9fa48("1397"), 'test')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1398") ? "" : (stryCov_9fa48("1398"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("1399") ? "" : (stryCov_9fa48("1399"), 'should get patient by id'), () => {
      if (stryMutAct_9fa48("1400")) {
        {}
      } else {
        stryCov_9fa48("1400");
        const mockPatient = stryMutAct_9fa48("1401") ? {} : (stryCov_9fa48("1401"), {
          id: stryMutAct_9fa48("1402") ? "" : (stryCov_9fa48("1402"), 'pat-001'),
          fullName: stryMutAct_9fa48("1403") ? "" : (stryCov_9fa48("1403"), 'Test Patient')
        });
        service.getById(stryMutAct_9fa48("1404") ? "" : (stryCov_9fa48("1404"), 'pat-001')).subscribe(patient => {
          if (stryMutAct_9fa48("1405")) {
            {}
          } else {
            stryCov_9fa48("1405");
            expect(patient.id).toBe(stryMutAct_9fa48("1406") ? "" : (stryCov_9fa48("1406"), 'pat-001'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1407") ? `` : (stryCov_9fa48("1407"), `${environment.apiUrl}/patients/pat-001`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1408") ? "" : (stryCov_9fa48("1408"), 'GET'));
        req.flush(mockPatient);
      }
    });
    it(stryMutAct_9fa48("1409") ? "" : (stryCov_9fa48("1409"), 'should create patient'), () => {
      if (stryMutAct_9fa48("1410")) {
        {}
      } else {
        stryCov_9fa48("1410");
        const request = stryMutAct_9fa48("1411") ? {} : (stryCov_9fa48("1411"), {
          fullName: stryMutAct_9fa48("1412") ? "" : (stryCov_9fa48("1412"), 'New Patient')
        });
        const mockPatient = stryMutAct_9fa48("1413") ? {} : (stryCov_9fa48("1413"), {
          id: stryMutAct_9fa48("1414") ? "" : (stryCov_9fa48("1414"), 'pat-002'),
          ...request
        });
        service.create(request as any).subscribe(patient => {
          if (stryMutAct_9fa48("1415")) {
            {}
          } else {
            stryCov_9fa48("1415");
            expect(patient.id).toBe(stryMutAct_9fa48("1416") ? "" : (stryCov_9fa48("1416"), 'pat-002'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1417") ? `` : (stryCov_9fa48("1417"), `${environment.apiUrl}/patients/`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1418") ? "" : (stryCov_9fa48("1418"), 'POST'));
        req.flush(mockPatient);
      }
    });
    it(stryMutAct_9fa48("1419") ? "" : (stryCov_9fa48("1419"), 'should update patient'), () => {
      if (stryMutAct_9fa48("1420")) {
        {}
      } else {
        stryCov_9fa48("1420");
        const request = stryMutAct_9fa48("1421") ? {} : (stryCov_9fa48("1421"), {
          fullName: stryMutAct_9fa48("1422") ? "" : (stryCov_9fa48("1422"), 'Updated')
        });
        const mockPatient = stryMutAct_9fa48("1423") ? {} : (stryCov_9fa48("1423"), {
          id: stryMutAct_9fa48("1424") ? "" : (stryCov_9fa48("1424"), 'pat-001'),
          ...request
        });
        service.update(stryMutAct_9fa48("1425") ? "" : (stryCov_9fa48("1425"), 'pat-001'), request as any).subscribe(patient => {
          if (stryMutAct_9fa48("1426")) {
            {}
          } else {
            stryCov_9fa48("1426");
            expect(patient.fullName).toBe(stryMutAct_9fa48("1427") ? "" : (stryCov_9fa48("1427"), 'Updated'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1428") ? `` : (stryCov_9fa48("1428"), `${environment.apiUrl}/patients/pat-001`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1429") ? "" : (stryCov_9fa48("1429"), 'PUT'));
        req.flush(mockPatient);
      }
    });
    it(stryMutAct_9fa48("1430") ? "" : (stryCov_9fa48("1430"), 'should deactivate patient'), () => {
      if (stryMutAct_9fa48("1431")) {
        {}
      } else {
        stryCov_9fa48("1431");
        service.deactivate(stryMutAct_9fa48("1432") ? "" : (stryCov_9fa48("1432"), 'pat-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1433") ? `` : (stryCov_9fa48("1433"), `${environment.apiUrl}/patients/pat-001/deactivate`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1434") ? "" : (stryCov_9fa48("1434"), 'PATCH'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("1435") ? "" : (stryCov_9fa48("1435"), 'should get patient encounters'), () => {
      if (stryMutAct_9fa48("1436")) {
        {}
      } else {
        stryCov_9fa48("1436");
        const mockResult = stryMutAct_9fa48("1437") ? {} : (stryCov_9fa48("1437"), {
          items: stryMutAct_9fa48("1438") ? [] : (stryCov_9fa48("1438"), [stryMutAct_9fa48("1439") ? {} : (stryCov_9fa48("1439"), {
            id: stryMutAct_9fa48("1440") ? "" : (stryCov_9fa48("1440"), 'enc-001'),
            patientId: stryMutAct_9fa48("1441") ? "" : (stryCov_9fa48("1441"), 'pat-001')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1442") ? true : (stryCov_9fa48("1442"), false),
          hasPreviousPage: stryMutAct_9fa48("1443") ? true : (stryCov_9fa48("1443"), false)
        });
        service.getEncounters(stryMutAct_9fa48("1444") ? "" : (stryCov_9fa48("1444"), 'pat-001')).subscribe(result => {
          if (stryMutAct_9fa48("1445")) {
            {}
          } else {
            stryCov_9fa48("1445");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1446") ? () => undefined : (stryCov_9fa48("1446"), r => stryMutAct_9fa48("1449") ? r.url !== `${environment.apiUrl}/patients/pat-001/encounters` : stryMutAct_9fa48("1448") ? false : stryMutAct_9fa48("1447") ? true : (stryCov_9fa48("1447", "1448", "1449"), r.url === (stryMutAct_9fa48("1450") ? `` : (stryCov_9fa48("1450"), `${environment.apiUrl}/patients/pat-001/encounters`)))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1451") ? "" : (stryCov_9fa48("1451"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("1452") ? "" : (stryCov_9fa48("1452"), 'should handle create with validation error'), () => {
      if (stryMutAct_9fa48("1453")) {
        {}
      } else {
        stryCov_9fa48("1453");
        service.create({
          fullName: ''
        } as any).subscribe(stryMutAct_9fa48("1454") ? {} : (stryCov_9fa48("1454"), {
          error: error => {
            if (stryMutAct_9fa48("1455")) {
              {}
            } else {
              stryCov_9fa48("1455");
              expect(error.status).toBe(400);
            }
          }
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("1456") ? `` : (stryCov_9fa48("1456"), `${environment.apiUrl}/patients/`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1457") ? "" : (stryCov_9fa48("1457"), 'POST'));
        req.flush(stryMutAct_9fa48("1458") ? "" : (stryCov_9fa48("1458"), 'Validation error'), stryMutAct_9fa48("1459") ? {} : (stryCov_9fa48("1459"), {
          status: 400,
          statusText: stryMutAct_9fa48("1460") ? "" : (stryCov_9fa48("1460"), 'Bad Request')
        }));
      }
    });
    it(stryMutAct_9fa48("1461") ? "" : (stryCov_9fa48("1461"), 'should handle update not found'), () => {
      if (stryMutAct_9fa48("1462")) {
        {}
      } else {
        stryCov_9fa48("1462");
        service.update(stryMutAct_9fa48("1463") ? "" : (stryCov_9fa48("1463"), 'non-existent'), {
          fullName: 'Test'
        } as any).subscribe(stryMutAct_9fa48("1464") ? {} : (stryCov_9fa48("1464"), {
          error: error => {
            if (stryMutAct_9fa48("1465")) {
              {}
            } else {
              stryCov_9fa48("1465");
              expect(error.status).toBe(404);
            }
          }
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("1466") ? `` : (stryCov_9fa48("1466"), `${environment.apiUrl}/patients/non-existent`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1467") ? "" : (stryCov_9fa48("1467"), 'PUT'));
        req.flush(stryMutAct_9fa48("1468") ? "" : (stryCov_9fa48("1468"), 'Not found'), stryMutAct_9fa48("1469") ? {} : (stryCov_9fa48("1469"), {
          status: 404,
          statusText: stryMutAct_9fa48("1470") ? "" : (stryCov_9fa48("1470"), 'Not Found')
        }));
      }
    });
    it(stryMutAct_9fa48("1471") ? "" : (stryCov_9fa48("1471"), 'should handle deactivate error'), () => {
      if (stryMutAct_9fa48("1472")) {
        {}
      } else {
        stryCov_9fa48("1472");
        service.deactivate(stryMutAct_9fa48("1473") ? "" : (stryCov_9fa48("1473"), 'pat-001')).subscribe(stryMutAct_9fa48("1474") ? {} : (stryCov_9fa48("1474"), {
          error: error => {
            if (stryMutAct_9fa48("1475")) {
              {}
            } else {
              stryCov_9fa48("1475");
              expect(error.status).toBe(500);
            }
          }
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("1476") ? `` : (stryCov_9fa48("1476"), `${environment.apiUrl}/patients/pat-001/deactivate`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1477") ? "" : (stryCov_9fa48("1477"), 'PATCH'));
        req.flush(stryMutAct_9fa48("1478") ? "" : (stryCov_9fa48("1478"), 'Server error'), stryMutAct_9fa48("1479") ? {} : (stryCov_9fa48("1479"), {
          status: 500,
          statusText: stryMutAct_9fa48("1480") ? "" : (stryCov_9fa48("1480"), 'Internal Server Error')
        }));
      }
    });
    it(stryMutAct_9fa48("1481") ? "" : (stryCov_9fa48("1481"), 'should get patient appointments'), () => {
      if (stryMutAct_9fa48("1482")) {
        {}
      } else {
        stryCov_9fa48("1482");
        const mockResult = stryMutAct_9fa48("1483") ? {} : (stryCov_9fa48("1483"), {
          items: stryMutAct_9fa48("1484") ? [] : (stryCov_9fa48("1484"), [stryMutAct_9fa48("1485") ? {} : (stryCov_9fa48("1485"), {
            id: stryMutAct_9fa48("1486") ? "" : (stryCov_9fa48("1486"), 'apt-001'),
            patientId: stryMutAct_9fa48("1487") ? "" : (stryCov_9fa48("1487"), 'pat-001'),
            reason: stryMutAct_9fa48("1488") ? "" : (stryCov_9fa48("1488"), 'Checkup')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1489") ? true : (stryCov_9fa48("1489"), false),
          hasPreviousPage: stryMutAct_9fa48("1490") ? true : (stryCov_9fa48("1490"), false)
        });
        service.getAppointments(stryMutAct_9fa48("1491") ? "" : (stryCov_9fa48("1491"), 'pat-001')).subscribe(result => {
          if (stryMutAct_9fa48("1492")) {
            {}
          } else {
            stryCov_9fa48("1492");
            expect(result.items.length).toBe(1);
            expect(result.items[0].reason).toBe(stryMutAct_9fa48("1493") ? "" : (stryCov_9fa48("1493"), 'Checkup'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1494") ? () => undefined : (stryCov_9fa48("1494"), r => stryMutAct_9fa48("1497") ? r.url !== `${environment.apiUrl}/patients/pat-001/appointments` : stryMutAct_9fa48("1496") ? false : stryMutAct_9fa48("1495") ? true : (stryCov_9fa48("1495", "1496", "1497"), r.url === (stryMutAct_9fa48("1498") ? `` : (stryCov_9fa48("1498"), `${environment.apiUrl}/patients/pat-001/appointments`)))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1499") ? "" : (stryCov_9fa48("1499"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("1500") ? "" : (stryCov_9fa48("1500"), 'should get prescriptions'), () => {
      if (stryMutAct_9fa48("1501")) {
        {}
      } else {
        stryCov_9fa48("1501");
        const mockResult = stryMutAct_9fa48("1502") ? {} : (stryCov_9fa48("1502"), {
          items: stryMutAct_9fa48("1503") ? [] : (stryCov_9fa48("1503"), [stryMutAct_9fa48("1504") ? {} : (stryCov_9fa48("1504"), {
            id: stryMutAct_9fa48("1505") ? "" : (stryCov_9fa48("1505"), 'rx-001')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1506") ? true : (stryCov_9fa48("1506"), false),
          hasPreviousPage: stryMutAct_9fa48("1507") ? true : (stryCov_9fa48("1507"), false)
        });
        service.getPrescriptions(stryMutAct_9fa48("1508") ? "" : (stryCov_9fa48("1508"), 'pat-001')).subscribe(result => {
          if (stryMutAct_9fa48("1509")) {
            {}
          } else {
            stryCov_9fa48("1509");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1510") ? () => undefined : (stryCov_9fa48("1510"), r => stryMutAct_9fa48("1513") ? r.url !== `${environment.apiUrl}/patients/pat-001/prescriptions` : stryMutAct_9fa48("1512") ? false : stryMutAct_9fa48("1511") ? true : (stryCov_9fa48("1511", "1512", "1513"), r.url === (stryMutAct_9fa48("1514") ? `` : (stryCov_9fa48("1514"), `${environment.apiUrl}/patients/pat-001/prescriptions`)))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1515") ? "" : (stryCov_9fa48("1515"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("1516") ? "" : (stryCov_9fa48("1516"), 'should get lab orders'), () => {
      if (stryMutAct_9fa48("1517")) {
        {}
      } else {
        stryCov_9fa48("1517");
        const mockResult = stryMutAct_9fa48("1518") ? {} : (stryCov_9fa48("1518"), {
          items: stryMutAct_9fa48("1519") ? [] : (stryCov_9fa48("1519"), [stryMutAct_9fa48("1520") ? {} : (stryCov_9fa48("1520"), {
            id: stryMutAct_9fa48("1521") ? "" : (stryCov_9fa48("1521"), 'lab-001')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1522") ? true : (stryCov_9fa48("1522"), false),
          hasPreviousPage: stryMutAct_9fa48("1523") ? true : (stryCov_9fa48("1523"), false)
        });
        service.getLabOrders(stryMutAct_9fa48("1524") ? "" : (stryCov_9fa48("1524"), 'pat-001')).subscribe(result => {
          if (stryMutAct_9fa48("1525")) {
            {}
          } else {
            stryCov_9fa48("1525");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1526") ? () => undefined : (stryCov_9fa48("1526"), r => stryMutAct_9fa48("1529") ? r.url !== `${environment.apiUrl}/patients/pat-001/lab-orders` : stryMutAct_9fa48("1528") ? false : stryMutAct_9fa48("1527") ? true : (stryCov_9fa48("1527", "1528", "1529"), r.url === (stryMutAct_9fa48("1530") ? `` : (stryCov_9fa48("1530"), `${environment.apiUrl}/patients/pat-001/lab-orders`)))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1531") ? "" : (stryCov_9fa48("1531"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("1532") ? "" : (stryCov_9fa48("1532"), 'should get invoices'), () => {
      if (stryMutAct_9fa48("1533")) {
        {}
      } else {
        stryCov_9fa48("1533");
        const mockResult = stryMutAct_9fa48("1534") ? {} : (stryCov_9fa48("1534"), {
          items: stryMutAct_9fa48("1535") ? [] : (stryCov_9fa48("1535"), [stryMutAct_9fa48("1536") ? {} : (stryCov_9fa48("1536"), {
            id: stryMutAct_9fa48("1537") ? "" : (stryCov_9fa48("1537"), 'inv-001')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1538") ? true : (stryCov_9fa48("1538"), false),
          hasPreviousPage: stryMutAct_9fa48("1539") ? true : (stryCov_9fa48("1539"), false)
        });
        service.getInvoices(stryMutAct_9fa48("1540") ? "" : (stryCov_9fa48("1540"), 'pat-001')).subscribe(result => {
          if (stryMutAct_9fa48("1541")) {
            {}
          } else {
            stryCov_9fa48("1541");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1542") ? () => undefined : (stryCov_9fa48("1542"), r => stryMutAct_9fa48("1545") ? r.url !== `${environment.apiUrl}/patients/pat-001/invoices` : stryMutAct_9fa48("1544") ? false : stryMutAct_9fa48("1543") ? true : (stryCov_9fa48("1543", "1544", "1545"), r.url === (stryMutAct_9fa48("1546") ? `` : (stryCov_9fa48("1546"), `${environment.apiUrl}/patients/pat-001/invoices`)))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1547") ? "" : (stryCov_9fa48("1547"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("1548") ? "" : (stryCov_9fa48("1548"), 'should reactivate patient'), () => {
      if (stryMutAct_9fa48("1549")) {
        {}
      } else {
        stryCov_9fa48("1549");
        service.reactivate(stryMutAct_9fa48("1550") ? "" : (stryCov_9fa48("1550"), 'pat-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1551") ? `` : (stryCov_9fa48("1551"), `${environment.apiUrl}/patients/pat-001/reactivate`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1552") ? "" : (stryCov_9fa48("1552"), 'PATCH'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("1553") ? "" : (stryCov_9fa48("1553"), 'should getEncounters with empty result'), () => {
      if (stryMutAct_9fa48("1554")) {
        {}
      } else {
        stryCov_9fa48("1554");
        service.getEncounters(stryMutAct_9fa48("1555") ? "" : (stryCov_9fa48("1555"), 'pat-001')).subscribe(result => {
          if (stryMutAct_9fa48("1556")) {
            {}
          } else {
            stryCov_9fa48("1556");
            expect(result.items.length).toBe(0);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1557") ? () => undefined : (stryCov_9fa48("1557"), r => stryMutAct_9fa48("1560") ? r.url !== `${environment.apiUrl}/patients/pat-001/encounters` : stryMutAct_9fa48("1559") ? false : stryMutAct_9fa48("1558") ? true : (stryCov_9fa48("1558", "1559", "1560"), r.url === (stryMutAct_9fa48("1561") ? `` : (stryCov_9fa48("1561"), `${environment.apiUrl}/patients/pat-001/encounters`)))));
        req.flush(stryMutAct_9fa48("1562") ? {} : (stryCov_9fa48("1562"), {
          items: stryMutAct_9fa48("1563") ? ["Stryker was here"] : (stryCov_9fa48("1563"), []),
          totalCount: 0,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1564") ? true : (stryCov_9fa48("1564"), false),
          hasPreviousPage: stryMutAct_9fa48("1565") ? true : (stryCov_9fa48("1565"), false)
        }));
      }
    });
    it(stryMutAct_9fa48("1566") ? "" : (stryCov_9fa48("1566"), 'should search with default params'), () => {
      if (stryMutAct_9fa48("1567")) {
        {}
      } else {
        stryCov_9fa48("1567");
        service.search(stryMutAct_9fa48("1568") ? "Stryker was here!" : (stryCov_9fa48("1568"), '')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1569") ? () => undefined : (stryCov_9fa48("1569"), r => stryMutAct_9fa48("1572") ? r.url !== `${environment.apiUrl}/patients/search` : stryMutAct_9fa48("1571") ? false : stryMutAct_9fa48("1570") ? true : (stryCov_9fa48("1570", "1571", "1572"), r.url === (stryMutAct_9fa48("1573") ? `` : (stryCov_9fa48("1573"), `${environment.apiUrl}/patients/search`)))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1574") ? "" : (stryCov_9fa48("1574"), 'GET'));
        req.flush(stryMutAct_9fa48("1575") ? {} : (stryCov_9fa48("1575"), {
          items: stryMutAct_9fa48("1576") ? ["Stryker was here"] : (stryCov_9fa48("1576"), []),
          totalCount: 0,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1577") ? true : (stryCov_9fa48("1577"), false),
          hasPreviousPage: stryMutAct_9fa48("1578") ? true : (stryCov_9fa48("1578"), false)
        }));
      }
    });
  }
});