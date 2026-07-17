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
import { PharmacyService } from './pharmacy.service';
import { environment } from '@env/environment';
describe(stryMutAct_9fa48("1627") ? "" : (stryCov_9fa48("1627"), 'PharmacyService'), () => {
  if (stryMutAct_9fa48("1628")) {
    {}
  } else {
    stryCov_9fa48("1628");
    let service: PharmacyService;
    let httpMock: HttpTestingController;
    beforeEach(() => {
      if (stryMutAct_9fa48("1629")) {
        {}
      } else {
        stryCov_9fa48("1629");
        TestBed.configureTestingModule(stryMutAct_9fa48("1630") ? {} : (stryCov_9fa48("1630"), {
          imports: stryMutAct_9fa48("1631") ? [] : (stryCov_9fa48("1631"), [HttpClientTestingModule])
        }));
        service = TestBed.inject(PharmacyService);
        httpMock = TestBed.inject(HttpTestingController);
      }
    });
    afterEach(() => {
      if (stryMutAct_9fa48("1632")) {
        {}
      } else {
        stryCov_9fa48("1632");
        httpMock.verify();
      }
    });
    it(stryMutAct_9fa48("1633") ? "" : (stryCov_9fa48("1633"), 'should search medications'), () => {
      if (stryMutAct_9fa48("1634")) {
        {}
      } else {
        stryCov_9fa48("1634");
        const mockResult = stryMutAct_9fa48("1635") ? {} : (stryCov_9fa48("1635"), {
          items: stryMutAct_9fa48("1636") ? [] : (stryCov_9fa48("1636"), [stryMutAct_9fa48("1637") ? {} : (stryCov_9fa48("1637"), {
            id: stryMutAct_9fa48("1638") ? "" : (stryCov_9fa48("1638"), 'med-001'),
            name: stryMutAct_9fa48("1639") ? "" : (stryCov_9fa48("1639"), 'Amoxicillin')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1640") ? true : (stryCov_9fa48("1640"), false),
          hasPreviousPage: stryMutAct_9fa48("1641") ? true : (stryCov_9fa48("1641"), false)
        });
        service.searchMedications(stryMutAct_9fa48("1642") ? {} : (stryCov_9fa48("1642"), {
          searchTerm: stryMutAct_9fa48("1643") ? "" : (stryCov_9fa48("1643"), 'amox')
        })).subscribe(result => {
          if (stryMutAct_9fa48("1644")) {
            {}
          } else {
            stryCov_9fa48("1644");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1645") ? () => undefined : (stryCov_9fa48("1645"), r => stryMutAct_9fa48("1648") ? r.url === `${environment.apiUrl}/medications/search` || r.params.get('q') === 'amox' : stryMutAct_9fa48("1647") ? false : stryMutAct_9fa48("1646") ? true : (stryCov_9fa48("1646", "1647", "1648"), (stryMutAct_9fa48("1650") ? r.url !== `${environment.apiUrl}/medications/search` : stryMutAct_9fa48("1649") ? true : (stryCov_9fa48("1649", "1650"), r.url === (stryMutAct_9fa48("1651") ? `` : (stryCov_9fa48("1651"), `${environment.apiUrl}/medications/search`)))) && (stryMutAct_9fa48("1653") ? r.params.get('q') !== 'amox' : stryMutAct_9fa48("1652") ? true : (stryCov_9fa48("1652", "1653"), r.params.get(stryMutAct_9fa48("1654") ? "" : (stryCov_9fa48("1654"), 'q')) === (stryMutAct_9fa48("1655") ? "" : (stryCov_9fa48("1655"), 'amox')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1656") ? "" : (stryCov_9fa48("1656"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("1657") ? "" : (stryCov_9fa48("1657"), 'should get medication by id'), () => {
      if (stryMutAct_9fa48("1658")) {
        {}
      } else {
        stryCov_9fa48("1658");
        service.getMedication(stryMutAct_9fa48("1659") ? "" : (stryCov_9fa48("1659"), 'med-001')).subscribe(med => {
          if (stryMutAct_9fa48("1660")) {
            {}
          } else {
            stryCov_9fa48("1660");
            expect(med.id).toBe(stryMutAct_9fa48("1661") ? "" : (stryCov_9fa48("1661"), 'med-001'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1662") ? `` : (stryCov_9fa48("1662"), `${environment.apiUrl}/medications/med-001`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1663") ? "" : (stryCov_9fa48("1663"), 'GET'));
        req.flush(stryMutAct_9fa48("1664") ? {} : (stryCov_9fa48("1664"), {
          id: stryMutAct_9fa48("1665") ? "" : (stryCov_9fa48("1665"), 'med-001')
        }));
      }
    });
    it(stryMutAct_9fa48("1666") ? "" : (stryCov_9fa48("1666"), 'should create medication'), () => {
      if (stryMutAct_9fa48("1667")) {
        {}
      } else {
        stryCov_9fa48("1667");
        const data = stryMutAct_9fa48("1668") ? {} : (stryCov_9fa48("1668"), {
          name: stryMutAct_9fa48("1669") ? "" : (stryCov_9fa48("1669"), 'New Med'),
          genericName: stryMutAct_9fa48("1670") ? "" : (stryCov_9fa48("1670"), 'Generic'),
          strength: stryMutAct_9fa48("1671") ? "" : (stryCov_9fa48("1671"), '500mg')
        });
        service.createMedication(data as any).subscribe(med => {
          if (stryMutAct_9fa48("1672")) {
            {}
          } else {
            stryCov_9fa48("1672");
            expect(med.name).toBe(stryMutAct_9fa48("1673") ? "" : (stryCov_9fa48("1673"), 'New Med'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1674") ? `` : (stryCov_9fa48("1674"), `${environment.apiUrl}/medications/`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1675") ? "" : (stryCov_9fa48("1675"), 'POST'));
        req.flush(stryMutAct_9fa48("1676") ? {} : (stryCov_9fa48("1676"), {
          id: stryMutAct_9fa48("1677") ? "" : (stryCov_9fa48("1677"), 'med-002'),
          ...data
        }));
      }
    });
    it(stryMutAct_9fa48("1678") ? "" : (stryCov_9fa48("1678"), 'should update medication'), () => {
      if (stryMutAct_9fa48("1679")) {
        {}
      } else {
        stryCov_9fa48("1679");
        const data = stryMutAct_9fa48("1680") ? {} : (stryCov_9fa48("1680"), {
          name: stryMutAct_9fa48("1681") ? "" : (stryCov_9fa48("1681"), 'Updated')
        });
        service.updateMedication(stryMutAct_9fa48("1682") ? "" : (stryCov_9fa48("1682"), 'med-001'), data as any).subscribe(med => {
          if (stryMutAct_9fa48("1683")) {
            {}
          } else {
            stryCov_9fa48("1683");
            expect(med.name).toBe(stryMutAct_9fa48("1684") ? "" : (stryCov_9fa48("1684"), 'Updated'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1685") ? `` : (stryCov_9fa48("1685"), `${environment.apiUrl}/medications/med-001`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1686") ? "" : (stryCov_9fa48("1686"), 'PUT'));
        req.flush(stryMutAct_9fa48("1687") ? {} : (stryCov_9fa48("1687"), {
          id: stryMutAct_9fa48("1688") ? "" : (stryCov_9fa48("1688"), 'med-001'),
          ...data
        }));
      }
    });
    it(stryMutAct_9fa48("1689") ? "" : (stryCov_9fa48("1689"), 'should deactivate medication'), () => {
      if (stryMutAct_9fa48("1690")) {
        {}
      } else {
        stryCov_9fa48("1690");
        service.deactivateMedication(stryMutAct_9fa48("1691") ? "" : (stryCov_9fa48("1691"), 'med-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1692") ? `` : (stryCov_9fa48("1692"), `${environment.apiUrl}/medications/med-001/deactivate`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1693") ? "" : (stryCov_9fa48("1693"), 'PATCH'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("1694") ? "" : (stryCov_9fa48("1694"), 'should fill prescription'), () => {
      if (stryMutAct_9fa48("1695")) {
        {}
      } else {
        stryCov_9fa48("1695");
        service.fillPrescription(stryMutAct_9fa48("1696") ? "" : (stryCov_9fa48("1696"), 'rx-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1697") ? `` : (stryCov_9fa48("1697"), `${environment.apiUrl}/prescriptions/rx-001/fill`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1698") ? "" : (stryCov_9fa48("1698"), 'POST'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("1699") ? "" : (stryCov_9fa48("1699"), 'should cancel prescription'), () => {
      if (stryMutAct_9fa48("1700")) {
        {}
      } else {
        stryCov_9fa48("1700");
        service.cancelPrescription(stryMutAct_9fa48("1701") ? "" : (stryCov_9fa48("1701"), 'rx-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1702") ? `` : (stryCov_9fa48("1702"), `${environment.apiUrl}/prescriptions/rx-001/cancel`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1703") ? "" : (stryCov_9fa48("1703"), 'POST'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("1704") ? "" : (stryCov_9fa48("1704"), 'should create prescription'), () => {
      if (stryMutAct_9fa48("1705")) {
        {}
      } else {
        stryCov_9fa48("1705");
        const data = stryMutAct_9fa48("1706") ? {} : (stryCov_9fa48("1706"), {
          patientId: stryMutAct_9fa48("1707") ? "" : (stryCov_9fa48("1707"), 'pat-001'),
          medicationId: stryMutAct_9fa48("1708") ? "" : (stryCov_9fa48("1708"), 'med-001'),
          dosageInstructions: stryMutAct_9fa48("1709") ? "" : (stryCov_9fa48("1709"), 'Take 1 daily')
        });
        service.createPrescription(data as any).subscribe(rx => {
          if (stryMutAct_9fa48("1710")) {
            {}
          } else {
            stryCov_9fa48("1710");
            expect(rx.id).toBe(stryMutAct_9fa48("1711") ? "" : (stryCov_9fa48("1711"), 'rx-002'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1712") ? `` : (stryCov_9fa48("1712"), `${environment.apiUrl}/prescriptions/`));
        expect(req.request.method).toBe(stryMutAct_9fa48("1713") ? "" : (stryCov_9fa48("1713"), 'POST'));
        req.flush(stryMutAct_9fa48("1714") ? {} : (stryCov_9fa48("1714"), {
          id: stryMutAct_9fa48("1715") ? "" : (stryCov_9fa48("1715"), 'rx-002'),
          ...data
        }));
      }
    });
    it(stryMutAct_9fa48("1716") ? "" : (stryCov_9fa48("1716"), 'should search prescriptions'), () => {
      if (stryMutAct_9fa48("1717")) {
        {}
      } else {
        stryCov_9fa48("1717");
        const mockResult = stryMutAct_9fa48("1718") ? {} : (stryCov_9fa48("1718"), {
          items: stryMutAct_9fa48("1719") ? [] : (stryCov_9fa48("1719"), [stryMutAct_9fa48("1720") ? {} : (stryCov_9fa48("1720"), {
            id: stryMutAct_9fa48("1721") ? "" : (stryCov_9fa48("1721"), 'rx-001'),
            medicationName: stryMutAct_9fa48("1722") ? "" : (stryCov_9fa48("1722"), 'Amoxicillin')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1723") ? true : (stryCov_9fa48("1723"), false),
          hasPreviousPage: stryMutAct_9fa48("1724") ? true : (stryCov_9fa48("1724"), false)
        });
        service.searchPrescriptions(stryMutAct_9fa48("1725") ? {} : (stryCov_9fa48("1725"), {
          searchTerm: stryMutAct_9fa48("1726") ? "" : (stryCov_9fa48("1726"), 'amox')
        })).subscribe(result => {
          if (stryMutAct_9fa48("1727")) {
            {}
          } else {
            stryCov_9fa48("1727");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1728") ? () => undefined : (stryCov_9fa48("1728"), r => stryMutAct_9fa48("1731") ? r.url === `${environment.apiUrl}/prescriptions/search` || r.params.get('q') === 'amox' : stryMutAct_9fa48("1730") ? false : stryMutAct_9fa48("1729") ? true : (stryCov_9fa48("1729", "1730", "1731"), (stryMutAct_9fa48("1733") ? r.url !== `${environment.apiUrl}/prescriptions/search` : stryMutAct_9fa48("1732") ? true : (stryCov_9fa48("1732", "1733"), r.url === (stryMutAct_9fa48("1734") ? `` : (stryCov_9fa48("1734"), `${environment.apiUrl}/prescriptions/search`)))) && (stryMutAct_9fa48("1736") ? r.params.get('q') !== 'amox' : stryMutAct_9fa48("1735") ? true : (stryCov_9fa48("1735", "1736"), r.params.get(stryMutAct_9fa48("1737") ? "" : (stryCov_9fa48("1737"), 'q')) === (stryMutAct_9fa48("1738") ? "" : (stryCov_9fa48("1738"), 'amox')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1739") ? "" : (stryCov_9fa48("1739"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("1740") ? "" : (stryCov_9fa48("1740"), 'should get patient prescriptions'), () => {
      if (stryMutAct_9fa48("1741")) {
        {}
      } else {
        stryCov_9fa48("1741");
        const mockResult = stryMutAct_9fa48("1742") ? {} : (stryCov_9fa48("1742"), {
          items: stryMutAct_9fa48("1743") ? [] : (stryCov_9fa48("1743"), [stryMutAct_9fa48("1744") ? {} : (stryCov_9fa48("1744"), {
            id: stryMutAct_9fa48("1745") ? "" : (stryCov_9fa48("1745"), 'rx-001'),
            medicationName: stryMutAct_9fa48("1746") ? "" : (stryCov_9fa48("1746"), 'Amoxicillin')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("1747") ? true : (stryCov_9fa48("1747"), false),
          hasPreviousPage: stryMutAct_9fa48("1748") ? true : (stryCov_9fa48("1748"), false)
        });
        service.getPatientPrescriptions(stryMutAct_9fa48("1749") ? "" : (stryCov_9fa48("1749"), 'pat-001')).subscribe(result => {
          if (stryMutAct_9fa48("1750")) {
            {}
          } else {
            stryCov_9fa48("1750");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("1751") ? () => undefined : (stryCov_9fa48("1751"), r => stryMutAct_9fa48("1754") ? r.url === `${environment.apiUrl}/prescriptions/search` || r.params.get('patientId') === 'pat-001' : stryMutAct_9fa48("1753") ? false : stryMutAct_9fa48("1752") ? true : (stryCov_9fa48("1752", "1753", "1754"), (stryMutAct_9fa48("1756") ? r.url !== `${environment.apiUrl}/prescriptions/search` : stryMutAct_9fa48("1755") ? true : (stryCov_9fa48("1755", "1756"), r.url === (stryMutAct_9fa48("1757") ? `` : (stryCov_9fa48("1757"), `${environment.apiUrl}/prescriptions/search`)))) && (stryMutAct_9fa48("1759") ? r.params.get('patientId') !== 'pat-001' : stryMutAct_9fa48("1758") ? true : (stryCov_9fa48("1758", "1759"), r.params.get(stryMutAct_9fa48("1760") ? "" : (stryCov_9fa48("1760"), 'patientId')) === (stryMutAct_9fa48("1761") ? "" : (stryCov_9fa48("1761"), 'pat-001')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1762") ? "" : (stryCov_9fa48("1762"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("1763") ? "" : (stryCov_9fa48("1763"), 'should fill prescription handles error'), () => {
      if (stryMutAct_9fa48("1764")) {
        {}
      } else {
        stryCov_9fa48("1764");
        service.fillPrescription(stryMutAct_9fa48("1765") ? "" : (stryCov_9fa48("1765"), 'rx-001')).subscribe(stryMutAct_9fa48("1766") ? {} : (stryCov_9fa48("1766"), {
          error: stryMutAct_9fa48("1767") ? () => undefined : (stryCov_9fa48("1767"), error => expect(error).toBeTruthy())
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("1768") ? `` : (stryCov_9fa48("1768"), `${environment.apiUrl}/prescriptions/rx-001/fill`));
        req.flush(stryMutAct_9fa48("1769") ? "" : (stryCov_9fa48("1769"), 'Error'), stryMutAct_9fa48("1770") ? {} : (stryCov_9fa48("1770"), {
          status: 500,
          statusText: stryMutAct_9fa48("1771") ? "" : (stryCov_9fa48("1771"), 'Error')
        }));
      }
    });
    it(stryMutAct_9fa48("1772") ? "" : (stryCov_9fa48("1772"), 'should search medications with page params'), () => {
      if (stryMutAct_9fa48("1773")) {
        {}
      } else {
        stryCov_9fa48("1773");
        service.searchMedications(stryMutAct_9fa48("1774") ? {} : (stryCov_9fa48("1774"), {
          searchTerm: stryMutAct_9fa48("1775") ? "" : (stryCov_9fa48("1775"), 'test'),
          page: 2,
          pageSize: 50
        })).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("1776") ? () => undefined : (stryCov_9fa48("1776"), r => stryMutAct_9fa48("1779") ? r.params.get('q') === 'test' && r.params.get('page') === '2' || r.params.get('pageSize') === '50' : stryMutAct_9fa48("1778") ? false : stryMutAct_9fa48("1777") ? true : (stryCov_9fa48("1777", "1778", "1779"), (stryMutAct_9fa48("1781") ? r.params.get('q') === 'test' || r.params.get('page') === '2' : stryMutAct_9fa48("1780") ? true : (stryCov_9fa48("1780", "1781"), (stryMutAct_9fa48("1783") ? r.params.get('q') !== 'test' : stryMutAct_9fa48("1782") ? true : (stryCov_9fa48("1782", "1783"), r.params.get(stryMutAct_9fa48("1784") ? "" : (stryCov_9fa48("1784"), 'q')) === (stryMutAct_9fa48("1785") ? "" : (stryCov_9fa48("1785"), 'test')))) && (stryMutAct_9fa48("1787") ? r.params.get('page') !== '2' : stryMutAct_9fa48("1786") ? true : (stryCov_9fa48("1786", "1787"), r.params.get(stryMutAct_9fa48("1788") ? "" : (stryCov_9fa48("1788"), 'page')) === (stryMutAct_9fa48("1789") ? "" : (stryCov_9fa48("1789"), '2')))))) && (stryMutAct_9fa48("1791") ? r.params.get('pageSize') !== '50' : stryMutAct_9fa48("1790") ? true : (stryCov_9fa48("1790", "1791"), r.params.get(stryMutAct_9fa48("1792") ? "" : (stryCov_9fa48("1792"), 'pageSize')) === (stryMutAct_9fa48("1793") ? "" : (stryCov_9fa48("1793"), '50')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("1794") ? "" : (stryCov_9fa48("1794"), 'GET'));
        req.flush(stryMutAct_9fa48("1795") ? {} : (stryCov_9fa48("1795"), {
          items: stryMutAct_9fa48("1796") ? ["Stryker was here"] : (stryCov_9fa48("1796"), []),
          totalCount: 0,
          page: 2,
          pageSize: 50,
          hasNextPage: stryMutAct_9fa48("1797") ? true : (stryCov_9fa48("1797"), false),
          hasPreviousPage: stryMutAct_9fa48("1798") ? true : (stryCov_9fa48("1798"), false)
        }));
      }
    });
  }
});