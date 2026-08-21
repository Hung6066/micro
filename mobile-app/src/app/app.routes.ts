import { Routes } from '@angular/router';

import { MobileShellComponent } from './mobile-shell.component';

import { MobileLoginComponent } from './mobile-login.component';

import { MobileCallbackComponent } from './mobile-callback.component';

import { mobileAuthGuard } from './core/auth.guard';

import { mobileReadGuard } from './core/mobile-read.guard';

import { mobileWriteGuard } from './core/mobile-write.guard';

import { dashboardReadPermission } from './core/authorization/mobile-read-permissions';



export const routes: Routes = [

  { path: '', redirectTo: 'admin/dashboard', pathMatch: 'full' },

  { path: 'admin', component: MobileShellComponent, canActivate: [mobileAuthGuard], children: [

    { path: '', redirectTo: 'dashboard', pathMatch: 'full' },

    {

      path: 'dashboard',

      loadComponent: () =>

        import('./features/mobile-dashboard.component').then(

          (m) => m.MobileDashboardComponent,

        ),

      canActivate: [mobileReadGuard],

      data: { readPermission: dashboardReadPermission() },

    },

    {

      path: 'forbidden',

      loadComponent: () =>

        import('./features/mobile-forbidden-page.component').then(

          (m) => m.MobileForbiddenPageComponent,

        ),

    },

    {

      path: 'clients',

      loadComponent: () =>

        import('./features/mobile-resource-page.component').then(

          (m) => m.MobileResourcePageComponent,

        ),

      canActivate: [mobileReadGuard],

      data: { resource: 'clients' },

    },

    {

      path: 'clients/new',

      loadComponent: () =>

        import('./features/edits/mobile-client-edit-page.component').then(

          (m) => m.MobileClientEditPageComponent,

        ),

      canActivate: [mobileWriteGuard],

      data: {

        writePermission: 'admin.clients.write',

        resourceListPath: '/admin/clients',

      },

    },

    {

      path: 'clients/:id/edit',

      loadComponent: () =>

        import('./features/edits/mobile-client-edit-page.component').then(

          (m) => m.MobileClientEditPageComponent,

        ),

      canActivate: [mobileWriteGuard],

      data: {

        writePermission: 'admin.clients.write',

        resourceListPath: '/admin/clients',

      },

    },

    {

      path: 'users',

      loadComponent: () =>

        import('./features/mobile-resource-page.component').then(

          (m) => m.MobileResourcePageComponent,

        ),

      canActivate: [mobileReadGuard],

      data: { resource: 'users' },

    },

    {

      path: 'users/new',

      loadComponent: () =>

        import('./features/edits/mobile-user-edit-page.component').then(

          (m) => m.MobileUserEditPageComponent,

        ),

      canActivate: [mobileWriteGuard],

      data: {

        writePermission: 'admin.users.write',

        resourceListPath: '/admin/users',

      },

    },

    {

      path: 'users/:id/edit',

      loadComponent: () =>

        import('./features/edits/mobile-user-edit-page.component').then(

          (m) => m.MobileUserEditPageComponent,

        ),

      canActivate: [mobileWriteGuard],

      data: {

        writePermission: 'admin.users.write',

        resourceListPath: '/admin/users',

      },

    },

    {

      path: 'roles',

      loadComponent: () =>

        import('./features/mobile-resource-page.component').then(

          (m) => m.MobileResourcePageComponent,

        ),

      canActivate: [mobileReadGuard],

      data: { resource: 'roles' },

    },

    {

      path: 'roles/new',

      loadComponent: () =>

        import('./features/edits/mobile-role-edit-page.component').then(

          (m) => m.MobileRoleEditPageComponent,

        ),

      canActivate: [mobileWriteGuard],

      data: {

        writePermission: 'admin.roles.write',

        resourceListPath: '/admin/roles',

      },

    },

    {

      path: 'roles/:id/edit',

      loadComponent: () =>

        import('./features/edits/mobile-role-edit-page.component').then(

          (m) => m.MobileRoleEditPageComponent,

        ),

      canActivate: [mobileWriteGuard],

      data: {

        writePermission: 'admin.roles.write',

        resourceListPath: '/admin/roles',

      },

    },

    {

      path: 'consents',

      loadComponent: () =>

        import('./features/mobile-resource-page.component').then(

          (m) => m.MobileResourcePageComponent,

        ),

      canActivate: [mobileReadGuard],

      data: { resource: 'consents' },

    },

    {

      path: 'consents/:id',

      loadComponent: () =>

        import('./features/mobile-consent-detail-page.component').then(

          (m) => m.MobileConsentDetailPageComponent,

        ),

      canActivate: [mobileReadGuard],

      data: { resource: 'consents' },

    },

    {

      path: 'mfa',

      loadComponent: () =>

        import('./features/mobile-mfa.component').then(

          (m) => m.MobileMfaComponent,

        ),

      canActivate: [mobileReadGuard],

      data: { readPermission: 'admin.credentials.reset' },

    },

    {

      path: 'notifications',

      loadComponent: () =>

        import('./features/mobile-notifications.component').then(

          (m) => m.MobileNotificationsComponent,

        ),

      canActivate: [mobileReadGuard],

      data: { readPermission: 'admin.users.read' },

    },

  ] },

  { path: 'auth/login', component: MobileLoginComponent },

  { path: 'auth/callback', component: MobileCallbackComponent },

  {

    path: 'auth/mfa',

    loadComponent: () =>

      import('./mobile-native-mfa.component').then(

        (m) => m.MobileNativeMfaComponent,

      ),

  },

  { path: '**', redirectTo: '' },

];

