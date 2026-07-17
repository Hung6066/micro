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
import { AdminService } from './admin.service';
import { environment } from '@env/environment';
describe(stryMutAct_9fa48("0") ? "" : (stryCov_9fa48("0"), 'AdminService'), () => {
  if (stryMutAct_9fa48("1")) {
    {}
  } else {
    stryCov_9fa48("1");
    let service: AdminService;
    let httpMock: HttpTestingController;
    beforeEach(() => {
      if (stryMutAct_9fa48("2")) {
        {}
      } else {
        stryCov_9fa48("2");
        TestBed.configureTestingModule(stryMutAct_9fa48("3") ? {} : (stryCov_9fa48("3"), {
          imports: stryMutAct_9fa48("4") ? [] : (stryCov_9fa48("4"), [HttpClientTestingModule])
        }));
        service = TestBed.inject(AdminService);
        httpMock = TestBed.inject(HttpTestingController);
      }
    });
    afterEach(() => {
      if (stryMutAct_9fa48("5")) {
        {}
      } else {
        stryCov_9fa48("5");
        httpMock.verify();
      }
    });
    it(stryMutAct_9fa48("6") ? "" : (stryCov_9fa48("6"), 'should get users with pagination'), () => {
      if (stryMutAct_9fa48("7")) {
        {}
      } else {
        stryCov_9fa48("7");
        const mockResult = stryMutAct_9fa48("8") ? {} : (stryCov_9fa48("8"), {
          items: stryMutAct_9fa48("9") ? [] : (stryCov_9fa48("9"), [stryMutAct_9fa48("10") ? {} : (stryCov_9fa48("10"), {
            id: stryMutAct_9fa48("11") ? "" : (stryCov_9fa48("11"), 'usr-001'),
            username: stryMutAct_9fa48("12") ? "" : (stryCov_9fa48("12"), 'admin')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("13") ? true : (stryCov_9fa48("13"), false),
          hasPreviousPage: stryMutAct_9fa48("14") ? true : (stryCov_9fa48("14"), false)
        });
        service.getUsers(stryMutAct_9fa48("15") ? {} : (stryCov_9fa48("15"), {
          page: 1,
          pageSize: 20
        })).subscribe(result => {
          if (stryMutAct_9fa48("16")) {
            {}
          } else {
            stryCov_9fa48("16");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("17") ? () => undefined : (stryCov_9fa48("17"), r => stryMutAct_9fa48("20") ? r.url !== `${environment.apiUrl}/admin/users` : stryMutAct_9fa48("19") ? false : stryMutAct_9fa48("18") ? true : (stryCov_9fa48("18", "19", "20"), r.url === (stryMutAct_9fa48("21") ? `` : (stryCov_9fa48("21"), `${environment.apiUrl}/admin/users`)))));
        expect(req.request.method).toBe(stryMutAct_9fa48("22") ? "" : (stryCov_9fa48("22"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("23") ? "" : (stryCov_9fa48("23"), 'should get user by id'), () => {
      if (stryMutAct_9fa48("24")) {
        {}
      } else {
        stryCov_9fa48("24");
        service.getUser(stryMutAct_9fa48("25") ? "" : (stryCov_9fa48("25"), 'usr-001')).subscribe(user => {
          if (stryMutAct_9fa48("26")) {
            {}
          } else {
            stryCov_9fa48("26");
            expect(user.id).toBe(stryMutAct_9fa48("27") ? "" : (stryCov_9fa48("27"), 'usr-001'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("28") ? `` : (stryCov_9fa48("28"), `${environment.apiUrl}/admin/users/usr-001`));
        expect(req.request.method).toBe(stryMutAct_9fa48("29") ? "" : (stryCov_9fa48("29"), 'GET'));
        req.flush(stryMutAct_9fa48("30") ? {} : (stryCov_9fa48("30"), {
          id: stryMutAct_9fa48("31") ? "" : (stryCov_9fa48("31"), 'usr-001')
        }));
      }
    });
    it(stryMutAct_9fa48("32") ? "" : (stryCov_9fa48("32"), 'should create user'), () => {
      if (stryMutAct_9fa48("33")) {
        {}
      } else {
        stryCov_9fa48("33");
        const data = stryMutAct_9fa48("34") ? {} : (stryCov_9fa48("34"), {
          username: stryMutAct_9fa48("35") ? "" : (stryCov_9fa48("35"), 'newuser'),
          password: stryMutAct_9fa48("36") ? "" : (stryCov_9fa48("36"), 'secret')
        });
        service.createUser(data as any).subscribe(user => {
          if (stryMutAct_9fa48("37")) {
            {}
          } else {
            stryCov_9fa48("37");
            expect(user.username).toBe(stryMutAct_9fa48("38") ? "" : (stryCov_9fa48("38"), 'newuser'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("39") ? `` : (stryCov_9fa48("39"), `${environment.apiUrl}/admin/users`));
        expect(req.request.method).toBe(stryMutAct_9fa48("40") ? "" : (stryCov_9fa48("40"), 'POST'));
        req.flush(stryMutAct_9fa48("41") ? {} : (stryCov_9fa48("41"), {
          id: stryMutAct_9fa48("42") ? "" : (stryCov_9fa48("42"), 'usr-002'),
          ...data
        }));
      }
    });
    it(stryMutAct_9fa48("43") ? "" : (stryCov_9fa48("43"), 'should get roles'), () => {
      if (stryMutAct_9fa48("44")) {
        {}
      } else {
        stryCov_9fa48("44");
        service.getRoles().subscribe(roles => {
          if (stryMutAct_9fa48("45")) {
            {}
          } else {
            stryCov_9fa48("45");
            expect(roles.length).toBe(2);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("46") ? `` : (stryCov_9fa48("46"), `${environment.apiUrl}/admin/roles`));
        expect(req.request.method).toBe(stryMutAct_9fa48("47") ? "" : (stryCov_9fa48("47"), 'GET'));
        req.flush(stryMutAct_9fa48("48") ? [] : (stryCov_9fa48("48"), [stryMutAct_9fa48("49") ? {} : (stryCov_9fa48("49"), {
          id: stryMutAct_9fa48("50") ? "" : (stryCov_9fa48("50"), 'role-001'),
          name: stryMutAct_9fa48("51") ? "" : (stryCov_9fa48("51"), 'Admin')
        }), stryMutAct_9fa48("52") ? {} : (stryCov_9fa48("52"), {
          id: stryMutAct_9fa48("53") ? "" : (stryCov_9fa48("53"), 'role-002'),
          name: stryMutAct_9fa48("54") ? "" : (stryCov_9fa48("54"), 'Doctor')
        })]));
      }
    });
    it(stryMutAct_9fa48("55") ? "" : (stryCov_9fa48("55"), 'should get permissions'), () => {
      if (stryMutAct_9fa48("56")) {
        {}
      } else {
        stryCov_9fa48("56");
        service.getPermissions().subscribe(groups => {
          if (stryMutAct_9fa48("57")) {
            {}
          } else {
            stryCov_9fa48("57");
            expect(groups.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("58") ? `` : (stryCov_9fa48("58"), `${environment.apiUrl}/admin/permissions`));
        expect(req.request.method).toBe(stryMutAct_9fa48("59") ? "" : (stryCov_9fa48("59"), 'GET'));
        req.flush(stryMutAct_9fa48("60") ? [] : (stryCov_9fa48("60"), [stryMutAct_9fa48("61") ? {} : (stryCov_9fa48("61"), {
          group: stryMutAct_9fa48("62") ? "" : (stryCov_9fa48("62"), 'patients'),
          groupName: stryMutAct_9fa48("63") ? "" : (stryCov_9fa48("63"), 'Patients'),
          permissions: stryMutAct_9fa48("64") ? ["Stryker was here"] : (stryCov_9fa48("64"), [])
        })]));
      }
    });
    it(stryMutAct_9fa48("65") ? "" : (stryCov_9fa48("65"), 'should deactivate user'), () => {
      if (stryMutAct_9fa48("66")) {
        {}
      } else {
        stryCov_9fa48("66");
        service.deactivateUser(stryMutAct_9fa48("67") ? "" : (stryCov_9fa48("67"), 'usr-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("68") ? `` : (stryCov_9fa48("68"), `${environment.apiUrl}/admin/users/usr-001/deactivate`));
        expect(req.request.method).toBe(stryMutAct_9fa48("69") ? "" : (stryCov_9fa48("69"), 'POST'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("70") ? "" : (stryCov_9fa48("70"), 'should activate user'), () => {
      if (stryMutAct_9fa48("71")) {
        {}
      } else {
        stryCov_9fa48("71");
        service.activateUser(stryMutAct_9fa48("72") ? "" : (stryCov_9fa48("72"), 'usr-001')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("73") ? `` : (stryCov_9fa48("73"), `${environment.apiUrl}/admin/users/usr-001/activate`));
        expect(req.request.method).toBe(stryMutAct_9fa48("74") ? "" : (stryCov_9fa48("74"), 'POST'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("75") ? "" : (stryCov_9fa48("75"), 'should assign roles'), () => {
      if (stryMutAct_9fa48("76")) {
        {}
      } else {
        stryCov_9fa48("76");
        service.assignRoles(stryMutAct_9fa48("77") ? "" : (stryCov_9fa48("77"), 'usr-001'), stryMutAct_9fa48("78") ? [] : (stryCov_9fa48("78"), [stryMutAct_9fa48("79") ? "" : (stryCov_9fa48("79"), 'role-1'), stryMutAct_9fa48("80") ? "" : (stryCov_9fa48("80"), 'role-2')])).subscribe(user => {
          if (stryMutAct_9fa48("81")) {
            {}
          } else {
            stryCov_9fa48("81");
            expect(user.roles).toContain(stryMutAct_9fa48("82") ? "" : (stryCov_9fa48("82"), 'admin'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("83") ? `` : (stryCov_9fa48("83"), `${environment.apiUrl}/admin/users/usr-001/roles`));
        expect(req.request.method).toBe(stryMutAct_9fa48("84") ? "" : (stryCov_9fa48("84"), 'POST'));
        expect(req.request.body).toEqual(stryMutAct_9fa48("85") ? {} : (stryCov_9fa48("85"), {
          roleIds: stryMutAct_9fa48("86") ? [] : (stryCov_9fa48("86"), [stryMutAct_9fa48("87") ? "" : (stryCov_9fa48("87"), 'role-1'), stryMutAct_9fa48("88") ? "" : (stryCov_9fa48("88"), 'role-2')])
        }));
        req.flush(stryMutAct_9fa48("89") ? {} : (stryCov_9fa48("89"), {
          id: stryMutAct_9fa48("90") ? "" : (stryCov_9fa48("90"), 'usr-001'),
          username: stryMutAct_9fa48("91") ? "" : (stryCov_9fa48("91"), 'admin'),
          roles: stryMutAct_9fa48("92") ? [] : (stryCov_9fa48("92"), [stryMutAct_9fa48("93") ? "" : (stryCov_9fa48("93"), 'admin')])
        }));
      }
    });
    it(stryMutAct_9fa48("94") ? "" : (stryCov_9fa48("94"), 'should get settings'), () => {
      if (stryMutAct_9fa48("95")) {
        {}
      } else {
        stryCov_9fa48("95");
        const mockSettings = stryMutAct_9fa48("96") ? [] : (stryCov_9fa48("96"), [stryMutAct_9fa48("97") ? {} : (stryCov_9fa48("97"), {
          key: stryMutAct_9fa48("98") ? "" : (stryCov_9fa48("98"), 'hospital_name'),
          value: stryMutAct_9fa48("99") ? "" : (stryCov_9fa48("99"), 'His.Hope'),
          type: stryMutAct_9fa48("100") ? "" : (stryCov_9fa48("100"), 'text'),
          label: stryMutAct_9fa48("101") ? "" : (stryCov_9fa48("101"), 'Hospital Name'),
          category: stryMutAct_9fa48("102") ? "" : (stryCov_9fa48("102"), 'hospital')
        })]);
        service.getSettings().subscribe(settings => {
          if (stryMutAct_9fa48("103")) {
            {}
          } else {
            stryCov_9fa48("103");
            expect(settings.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("104") ? `` : (stryCov_9fa48("104"), `${environment.apiUrl}/admin/settings`));
        expect(req.request.method).toBe(stryMutAct_9fa48("105") ? "" : (stryCov_9fa48("105"), 'GET'));
        req.flush(mockSettings);
      }
    });
    it(stryMutAct_9fa48("106") ? "" : (stryCov_9fa48("106"), 'should get audit logs with params'), () => {
      if (stryMutAct_9fa48("107")) {
        {}
      } else {
        stryCov_9fa48("107");
        const mockResult = stryMutAct_9fa48("108") ? {} : (stryCov_9fa48("108"), {
          items: stryMutAct_9fa48("109") ? [] : (stryCov_9fa48("109"), [stryMutAct_9fa48("110") ? {} : (stryCov_9fa48("110"), {
            id: stryMutAct_9fa48("111") ? "" : (stryCov_9fa48("111"), 'log-001')
          })]),
          totalCount: 1,
          page: 1,
          pageSize: 20,
          hasNextPage: stryMutAct_9fa48("112") ? true : (stryCov_9fa48("112"), false),
          hasPreviousPage: stryMutAct_9fa48("113") ? true : (stryCov_9fa48("113"), false)
        });
        service.getAuditLogs(stryMutAct_9fa48("114") ? {} : (stryCov_9fa48("114"), {
          page: 1,
          pageSize: 20
        })).subscribe(result => {
          if (stryMutAct_9fa48("115")) {
            {}
          } else {
            stryCov_9fa48("115");
            expect(result.items.length).toBe(1);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("116") ? () => undefined : (stryCov_9fa48("116"), r => stryMutAct_9fa48("119") ? r.url.includes('/admin/audit-logs') || r.params.get('page') === '1' : stryMutAct_9fa48("118") ? false : stryMutAct_9fa48("117") ? true : (stryCov_9fa48("117", "118", "119"), r.url.includes(stryMutAct_9fa48("120") ? "" : (stryCov_9fa48("120"), '/admin/audit-logs')) && (stryMutAct_9fa48("122") ? r.params.get('page') !== '1' : stryMutAct_9fa48("121") ? true : (stryCov_9fa48("121", "122"), r.params.get(stryMutAct_9fa48("123") ? "" : (stryCov_9fa48("123"), 'page')) === (stryMutAct_9fa48("124") ? "" : (stryCov_9fa48("124"), '1')))))));
        expect(req.request.method).toBe(stryMutAct_9fa48("125") ? "" : (stryCov_9fa48("125"), 'GET'));
        req.flush(mockResult);
      }
    });
    it(stryMutAct_9fa48("126") ? "" : (stryCov_9fa48("126"), 'should delete role'), () => {
      if (stryMutAct_9fa48("127")) {
        {}
      } else {
        stryCov_9fa48("127");
        service.deleteRole(stryMutAct_9fa48("128") ? "" : (stryCov_9fa48("128"), 'role-1')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("129") ? `` : (stryCov_9fa48("129"), `${environment.apiUrl}/admin/roles/role-1`));
        expect(req.request.method).toBe(stryMutAct_9fa48("130") ? "" : (stryCov_9fa48("130"), 'DELETE'));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("131") ? "" : (stryCov_9fa48("131"), 'should get role by id'), () => {
      if (stryMutAct_9fa48("132")) {
        {}
      } else {
        stryCov_9fa48("132");
        service.getRole(stryMutAct_9fa48("133") ? "" : (stryCov_9fa48("133"), 'role-1')).subscribe(role => {
          if (stryMutAct_9fa48("134")) {
            {}
          } else {
            stryCov_9fa48("134");
            expect(role.name).toBe(stryMutAct_9fa48("135") ? "" : (stryCov_9fa48("135"), 'Admin'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("136") ? `` : (stryCov_9fa48("136"), `${environment.apiUrl}/admin/roles/role-1`));
        expect(req.request.method).toBe(stryMutAct_9fa48("137") ? "" : (stryCov_9fa48("137"), 'GET'));
        req.flush(stryMutAct_9fa48("138") ? {} : (stryCov_9fa48("138"), {
          id: stryMutAct_9fa48("139") ? "" : (stryCov_9fa48("139"), 'role-1'),
          name: stryMutAct_9fa48("140") ? "" : (stryCov_9fa48("140"), 'Admin')
        }));
      }
    });
    it(stryMutAct_9fa48("141") ? "" : (stryCov_9fa48("141"), 'should create role'), () => {
      if (stryMutAct_9fa48("142")) {
        {}
      } else {
        stryCov_9fa48("142");
        const data = stryMutAct_9fa48("143") ? {} : (stryCov_9fa48("143"), {
          name: stryMutAct_9fa48("144") ? "" : (stryCov_9fa48("144"), 'NewRole'),
          description: stryMutAct_9fa48("145") ? "" : (stryCov_9fa48("145"), 'New role'),
          permissions: stryMutAct_9fa48("146") ? [] : (stryCov_9fa48("146"), [stryMutAct_9fa48("147") ? "" : (stryCov_9fa48("147"), 'read')])
        });
        service.createRole(data).subscribe(role => {
          if (stryMutAct_9fa48("148")) {
            {}
          } else {
            stryCov_9fa48("148");
            expect(role.name).toBe(stryMutAct_9fa48("149") ? "" : (stryCov_9fa48("149"), 'NewRole'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("150") ? `` : (stryCov_9fa48("150"), `${environment.apiUrl}/admin/roles`));
        expect(req.request.method).toBe(stryMutAct_9fa48("151") ? "" : (stryCov_9fa48("151"), 'POST'));
        req.flush(stryMutAct_9fa48("152") ? {} : (stryCov_9fa48("152"), {
          id: stryMutAct_9fa48("153") ? "" : (stryCov_9fa48("153"), 'role-3'),
          ...data
        }));
      }
    });
    it(stryMutAct_9fa48("154") ? "" : (stryCov_9fa48("154"), 'should update role'), () => {
      if (stryMutAct_9fa48("155")) {
        {}
      } else {
        stryCov_9fa48("155");
        const data = stryMutAct_9fa48("156") ? {} : (stryCov_9fa48("156"), {
          name: stryMutAct_9fa48("157") ? "" : (stryCov_9fa48("157"), 'Updated'),
          description: stryMutAct_9fa48("158") ? "" : (stryCov_9fa48("158"), 'Updated desc'),
          permissions: stryMutAct_9fa48("159") ? [] : (stryCov_9fa48("159"), [stryMutAct_9fa48("160") ? "" : (stryCov_9fa48("160"), 'read'), stryMutAct_9fa48("161") ? "" : (stryCov_9fa48("161"), 'write')])
        });
        service.updateRole(stryMutAct_9fa48("162") ? "" : (stryCov_9fa48("162"), 'role-1'), data).subscribe(role => {
          if (stryMutAct_9fa48("163")) {
            {}
          } else {
            stryCov_9fa48("163");
            expect(role.name).toBe(stryMutAct_9fa48("164") ? "" : (stryCov_9fa48("164"), 'Updated'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("165") ? `` : (stryCov_9fa48("165"), `${environment.apiUrl}/admin/roles/role-1`));
        expect(req.request.method).toBe(stryMutAct_9fa48("166") ? "" : (stryCov_9fa48("166"), 'PUT'));
        req.flush(stryMutAct_9fa48("167") ? {} : (stryCov_9fa48("167"), {
          id: stryMutAct_9fa48("168") ? "" : (stryCov_9fa48("168"), 'role-1'),
          ...data
        }));
      }
    });
    it(stryMutAct_9fa48("169") ? "" : (stryCov_9fa48("169"), 'should update user'), () => {
      if (stryMutAct_9fa48("170")) {
        {}
      } else {
        stryCov_9fa48("170");
        service.updateUser(stryMutAct_9fa48("171") ? "" : (stryCov_9fa48("171"), 'usr-001'), stryMutAct_9fa48("172") ? {} : (stryCov_9fa48("172"), {
          fullName: stryMutAct_9fa48("173") ? "" : (stryCov_9fa48("173"), 'Updated')
        })).subscribe(user => {
          if (stryMutAct_9fa48("174")) {
            {}
          } else {
            stryCov_9fa48("174");
            expect(user.fullName).toBe(stryMutAct_9fa48("175") ? "" : (stryCov_9fa48("175"), 'Updated'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("176") ? `` : (stryCov_9fa48("176"), `${environment.apiUrl}/admin/users/usr-001`));
        expect(req.request.method).toBe(stryMutAct_9fa48("177") ? "" : (stryCov_9fa48("177"), 'PUT'));
        req.flush(stryMutAct_9fa48("178") ? {} : (stryCov_9fa48("178"), {
          id: stryMutAct_9fa48("179") ? "" : (stryCov_9fa48("179"), 'usr-001'),
          fullName: stryMutAct_9fa48("180") ? "" : (stryCov_9fa48("180"), 'Updated')
        }));
      }
    });
    it(stryMutAct_9fa48("181") ? "" : (stryCov_9fa48("181"), 'should get dashboard stats'), () => {
      if (stryMutAct_9fa48("182")) {
        {}
      } else {
        stryCov_9fa48("182");
        service.getDashboardStats().subscribe(stats => {
          if (stryMutAct_9fa48("183")) {
            {}
          } else {
            stryCov_9fa48("183");
            expect(stats.totalUsers).toBe(10);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("184") ? `` : (stryCov_9fa48("184"), `${environment.apiUrl}/admin/dashboard`));
        expect(req.request.method).toBe(stryMutAct_9fa48("185") ? "" : (stryCov_9fa48("185"), 'GET'));
        req.flush(stryMutAct_9fa48("186") ? {} : (stryCov_9fa48("186"), {
          totalUsers: 10,
          activeRoles: 5,
          lastAuditEntry: new Date().toISOString(),
          systemHealth: stryMutAct_9fa48("187") ? "" : (stryCov_9fa48("187"), 'healthy')
        }));
      }
    });
    it(stryMutAct_9fa48("188") ? "" : (stryCov_9fa48("188"), 'should get setting by key'), () => {
      if (stryMutAct_9fa48("189")) {
        {}
      } else {
        stryCov_9fa48("189");
        service.getSetting(stryMutAct_9fa48("190") ? "" : (stryCov_9fa48("190"), 'hospital_name')).subscribe(setting => {
          if (stryMutAct_9fa48("191")) {
            {}
          } else {
            stryCov_9fa48("191");
            expect(setting.value).toBe(stryMutAct_9fa48("192") ? "" : (stryCov_9fa48("192"), 'His.Hope'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("193") ? `` : (stryCov_9fa48("193"), `${environment.apiUrl}/admin/settings/hospital_name`));
        expect(req.request.method).toBe(stryMutAct_9fa48("194") ? "" : (stryCov_9fa48("194"), 'GET'));
        req.flush(stryMutAct_9fa48("195") ? {} : (stryCov_9fa48("195"), {
          key: stryMutAct_9fa48("196") ? "" : (stryCov_9fa48("196"), 'hospital_name'),
          value: stryMutAct_9fa48("197") ? "" : (stryCov_9fa48("197"), 'His.Hope')
        }));
      }
    });
    it(stryMutAct_9fa48("198") ? "" : (stryCov_9fa48("198"), 'should update setting'), () => {
      if (stryMutAct_9fa48("199")) {
        {}
      } else {
        stryCov_9fa48("199");
        service.updateSetting(stryMutAct_9fa48("200") ? "" : (stryCov_9fa48("200"), 'hospital_name'), stryMutAct_9fa48("201") ? "" : (stryCov_9fa48("201"), 'New Name')).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("202") ? `` : (stryCov_9fa48("202"), `${environment.apiUrl}/admin/settings/hospital_name`));
        expect(req.request.method).toBe(stryMutAct_9fa48("203") ? "" : (stryCov_9fa48("203"), 'PUT'));
        expect(req.request.body).toEqual(stryMutAct_9fa48("204") ? {} : (stryCov_9fa48("204"), {
          value: stryMutAct_9fa48("205") ? "" : (stryCov_9fa48("205"), 'New Name')
        }));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("206") ? "" : (stryCov_9fa48("206"), 'should bulk update settings'), () => {
      if (stryMutAct_9fa48("207")) {
        {}
      } else {
        stryCov_9fa48("207");
        const data = stryMutAct_9fa48("208") ? [] : (stryCov_9fa48("208"), [stryMutAct_9fa48("209") ? {} : (stryCov_9fa48("209"), {
          key: stryMutAct_9fa48("210") ? "" : (stryCov_9fa48("210"), 'k1'),
          value: stryMutAct_9fa48("211") ? "" : (stryCov_9fa48("211"), 'v1')
        })]);
        service.bulkUpdateSettings(data).subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("212") ? `` : (stryCov_9fa48("212"), `${environment.apiUrl}/admin/settings/bulk`));
        expect(req.request.method).toBe(stryMutAct_9fa48("213") ? "" : (stryCov_9fa48("213"), 'PUT'));
        expect(req.request.body).toEqual(stryMutAct_9fa48("214") ? {} : (stryCov_9fa48("214"), {
          settings: data
        }));
        req.flush(null);
      }
    });
    it(stryMutAct_9fa48("215") ? "" : (stryCov_9fa48("215"), 'should get audit log by id'), () => {
      if (stryMutAct_9fa48("216")) {
        {}
      } else {
        stryCov_9fa48("216");
        service.getAuditLog(stryMutAct_9fa48("217") ? "" : (stryCov_9fa48("217"), 'log-001')).subscribe(log => {
          if (stryMutAct_9fa48("218")) {
            {}
          } else {
            stryCov_9fa48("218");
            expect(log.action).toBe(stryMutAct_9fa48("219") ? "" : (stryCov_9fa48("219"), 'CREATE'));
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("220") ? `` : (stryCov_9fa48("220"), `${environment.apiUrl}/admin/audit-logs/log-001`));
        expect(req.request.method).toBe(stryMutAct_9fa48("221") ? "" : (stryCov_9fa48("221"), 'GET'));
        req.flush(stryMutAct_9fa48("222") ? {} : (stryCov_9fa48("222"), {
          id: stryMutAct_9fa48("223") ? "" : (stryCov_9fa48("223"), 'log-001'),
          action: stryMutAct_9fa48("224") ? "" : (stryCov_9fa48("224"), 'CREATE')
        }));
      }
    });
  }
});