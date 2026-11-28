import { Observable } from "rxjs";
import { HisHopeBulkAction, HisHopeBulkActionRequest } from "@his-hope/frontend-foundation/contracts";
import { HisHopeDataTableColumn } from "@his-hope/frontend-foundation/ui";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { formatHisHopeDateTime } from "@his-hope/mobile-foundation";
import {
  Consent,
  MobileBulkActionResponse,
  MobilePageQuery,
  MobilePageResult,
  MobileResource,
  OidcClient,
  Role,
  User,
} from "./contracts/mobile.contracts";
import { readPermission } from "./authorization/mobile-read-permissions";
import { ClientsApiService } from "./services/clients-api.service";
import { ConsentsApiService } from "./services/consents-api.service";
import { RolesApiService } from "./services/roles-api.service";
import { UsersApiService } from "./services/users-api.service";

export interface MobileResourceConfig {
  resource: MobileResource;
  titleKey: string;
  titleFallback: string;
  countLabelKey: string;
  countLabelFallback: string;
  emptyKey: string;
  emptyFallback: string;
  loadErrorKey: string;
  loadErrorFallback: string;
  loadMoreErrorKey: string;
  loadMoreErrorFallback: string;
  readPermission: string;
  writePermission: string;
  /** Set when the resource has a dedicated read-only detail route. */
  detailPath?: (id: string) => string;
  showCreate: boolean;
  selection: boolean;
  createColumns: (i18n: HisHopeI18nService) => HisHopeDataTableColumn[];
  createBulkActions: (
    i18n: HisHopeI18nService,
    canWrite: boolean,
  ) => HisHopeBulkAction[];
  loader: (
    services: MobileResourceServices,
    query: MobilePageQuery,
  ) => Observable<MobilePageResult<unknown>>;
  bulk?: (
    services: MobileResourceServices,
    request: HisHopeBulkActionRequest,
  ) => Observable<MobileBulkActionResponse>;
  toRow: (
    item: unknown,
    i18n: HisHopeI18nService,
  ) => Record<string, unknown>;
}

export interface MobileResourceServices {
  clients: ClientsApiService;
  users: UsersApiService;
  roles: RolesApiService;
  consents: ConsentsApiService;
}

export function createMobileResourceServices(
  clients: ClientsApiService,
  users: UsersApiService,
  roles: RolesApiService,
  consents: ConsentsApiService,
): MobileResourceServices {
  return { clients, users, roles, consents };
}

export const MOBILE_RESOURCE_CONFIGS: Record<
  MobileResource,
  MobileResourceConfig
> = {
  clients: {
    resource: "clients",
    titleKey: "admin.oidcApplications",
    titleFallback: "OIDC applications",
    countLabelKey: "admin.clients",
    countLabelFallback: "Clients",
    emptyKey: "mobile.noClients",
    emptyFallback: "No OIDC clients found.",
    loadErrorKey: "admin.loadClientsFailed",
    loadErrorFallback: "Failed to load clients.",
    loadMoreErrorKey: "mobile.unableLoadMore",
    loadMoreErrorFallback: "Unable to load more clients.",
    readPermission: readPermission("clients"),
    writePermission: "admin.clients.write",
    showCreate: true,
    selection: true,
    createColumns: (i18n) => [
      {
        key: "clientId",
        label: i18n.t("admin.clientId", "Client ID"),
        sortable: true,
        responsivePriority: 1,
      },
      {
        key: "displayName",
        label: i18n.t("admin.displayName", "Display name"),
        sortable: true,
        responsivePriority: 1,
      },
      {
        key: "clientType",
        label: i18n.t("admin.type", "Type"),
        responsivePriority: 2,
      },
      {
        key: "redirectUris",
        label: i18n.t("admin.redirectUris", "Redirect URIs"),
        responsivePriority: 3,
      },
    ],
    createBulkActions: (i18n, canWrite) =>
      canWrite
        ? [
            {
              id: "delete",
              label: i18n.t("admin.deleteSelected", "Delete selected"),
              tone: "danger",
            },
          ]
        : [],
    loader: (services, query) => services.clients.getClientsPage(query),
    bulk: (services, request) => services.clients.bulkClients(request),
    toRow: (item) => {
      const value = item as OidcClient;
      return {
        id: value.id ?? value.clientId,
        clientId: value.clientId,
        displayName: value.displayName,
        clientType: value.clientType,
        redirectUris: (value.redirectUris || []).join(", "),
        entity: value,
      };
    },
  },
  users: {
    resource: "users",
    titleKey: "admin.users",
    titleFallback: "Users",
    countLabelKey: "admin.users",
    countLabelFallback: "Users",
    emptyKey: "mobile.noUsers",
    emptyFallback: "No users found.",
    loadErrorKey: "admin.loadUsersFailed",
    loadErrorFallback: "Failed to load users.",
    loadMoreErrorKey: "mobile.unableLoadMore",
    loadMoreErrorFallback: "Unable to load more users.",
    readPermission: readPermission("users"),
    writePermission: "admin.users.write",
    showCreate: true,
    selection: true,
    createColumns: (i18n) => [
      {
        key: "userName",
        label: i18n.t("admin.username", "Username"),
        sortable: true,
        responsivePriority: 1,
      },
      {
        key: "email",
        label: i18n.t("admin.email", "Email"),
        sortable: true,
        responsivePriority: 2,
      },
      {
        key: "roles",
        label: i18n.t("admin.roles", "Roles"),
        responsivePriority: 3,
      },
      {
        key: "isActive",
        label: i18n.t("admin.active", "Active"),
        responsivePriority: 2,
        status: true,
      },
    ],
    createBulkActions: (i18n, canWrite) =>
      canWrite
        ? [
            {
              id: "activate",
              label: i18n.t("admin.activateSelected", "Activate selected"),
              icon: "person_add",
            },
            {
              id: "deactivate",
              label: i18n.t("admin.deactivateSelected", "Deactivate selected"),
              icon: "person_off",
              tone: "danger",
            },
          ]
        : [],
    loader: (services, query) => services.users.getUsersPage(query),
    bulk: (services, request) => services.users.bulkUsers(request),
    toRow: (item, i18n) => {
      const value = item as User;
      return {
        id: value.id,
        userName: value.userName,
        email: value.email,
        roles: (value.roles || []).join(", "),
        isActive: value.isActive
          ? i18n.t("common.yes", "Yes")
          : i18n.t("common.no", "No"),
        entity: value,
      };
    },
  },
  roles: {
    resource: "roles",
    titleKey: "admin.roles",
    titleFallback: "Roles",
    countLabelKey: "admin.roles",
    countLabelFallback: "Roles",
    emptyKey: "mobile.noRoles",
    emptyFallback: "No roles found.",
    loadErrorKey: "admin.loadRolesFailed",
    loadErrorFallback: "Failed to load roles.",
    loadMoreErrorKey: "mobile.unableLoadMore",
    loadMoreErrorFallback: "Unable to load more roles.",
    readPermission: readPermission("roles"),
    writePermission: "admin.roles.write",
    showCreate: true,
    selection: true,
    createColumns: (i18n) => [
      {
        key: "name",
        label: i18n.t("admin.name", "Name"),
        sortable: true,
        responsivePriority: 1,
      },
      {
        key: "description",
        label: i18n.t("admin.description", "Description"),
        responsivePriority: 2,
      },
    ],
    createBulkActions: (i18n, canWrite) =>
      canWrite
        ? [
            {
              id: "delete",
              label: i18n.t("admin.deleteSelected", "Delete selected"),
              tone: "danger",
            },
          ]
        : [],
    loader: (services, query) => services.roles.getRolesPage(query),
    bulk: (services, request) => services.roles.bulkRoles(request),
    toRow: (item) => {
      const value = item as Role;
      return {
        id: value.id ?? value.name,
        name: value.name,
        description: value.description ?? "",
        entity: value,
      };
    },
  },
  consents: {
    resource: "consents",
    titleKey: "admin.consents",
    titleFallback: "Consents",
    countLabelKey: "admin.consents",
    countLabelFallback: "Consents",
    emptyKey: "mobile.noConsents",
    emptyFallback: "No consents recorded.",
    loadErrorKey: "admin.loadConsentsFailed",
    loadErrorFallback: "Failed to load consents.",
    loadMoreErrorKey: "mobile.unableLoadMore",
    loadMoreErrorFallback: "Unable to load more consents.",
    readPermission: readPermission("consents"),
    writePermission: "admin.consents.write",
    detailPath: (id) => `/admin/consents/${encodeURIComponent(id)}`,
    showCreate: false,
    selection: false,
    createColumns: (i18n) => [
      {
        key: "subject",
        label: i18n.t("admin.subject", "Subject"),
        sortable: true,
        responsivePriority: 1,
      },
      {
        key: "clientId",
        label: i18n.t("admin.clientId", "Client ID"),
        responsivePriority: 2,
      },
      {
        key: "scopes",
        label: i18n.t("admin.scopes", "Scopes"),
        responsivePriority: 3,
      },
      {
        key: "created",
        label: i18n.t("admin.created", "Created"),
        sortable: true,
        responsivePriority: 2,
      },
    ],
    createBulkActions: () => [],
    loader: (services, query) => services.consents.getConsentsPage(query),
    toRow: (item) => {
      const value = item as Consent;
      return {
        id: value.id,
        subject: value.subject,
        clientId: value.clientId,
        scopes: (value.scopes || []).join(", "),
        created: formatHisHopeDateTime(value.created),
        entity: value,
      };
    },
  },
};
