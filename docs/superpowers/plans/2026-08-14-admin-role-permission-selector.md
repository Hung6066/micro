# Admin Role Permission Selector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Cho phép admin-app chọn permission từ catalog Identity API khi tạo/sửa role và gửi đúng permission codes về Identity Service.

**Architecture:** `RoleEditDialogComponent` tải `GET /permissions`, nhóm catalog theo `group`, giữ selected codes trong form và submit `permissions`. Identity API tiếp tục là authority, còn UI chỉ là client quản trị.

**Tech Stack:** Angular standalone component, Angular Forms, Angular Material, `@his-hope/frontend-foundation` i18n/theme components, RxJS, Karma/ChromeHeadless.

## Global Constraints

- Không hardcode permission catalog trong frontend.
- UI text mới phải dùng shared foundation i18n.
- Không dùng màu/font/CSS token trực tiếp ngoài contract.
- Backend permission/facility governance remains authoritative.

---

### Task 1: Role permission catalog selector

**Files:**
- Modify: `admin-app/src/app/features/roles/role-edit-dialog.component.ts`
- Test: `admin-app/src/app/features/roles/role-edit-dialog.component.spec.ts`

**Interfaces:**
- Consumes: `AdminApiService.getPermissions()` and `PermissionDefinition`.
- Produces: role payload with `permissions: string[]` for create/update.

- [ ] Load the permission catalog before rendering the form and expose grouped options.
- [ ] Initialize selected codes from the loaded role's permission objects or codes.
- [ ] Add search, group select-all, individual checkbox and selected count.
- [ ] Submit normalized selected codes through `createRole`/`updateRole`.
- [ ] Use foundation translation pipe and token-safe layout.
- [ ] Add component tests for catalog loading and payload mapping.

### Task 2: Contract validation

**Files:**
- No new production files.

- [ ] Run admin-app unit tests.
- [ ] Run shared foundation, i18n boundary and design-token validators.
- [ ] Build shared foundation first, then admin-app production build.
- [ ] Run `git diff --check` and record warnings separately from failures.
