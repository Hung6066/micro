# syntax=docker/dockerfile:1

# ============================================================
# BUILD STAGE (Angular 17)
# ============================================================
FROM node:20-alpine AS build
WORKDIR /workspace

# Install Angular CLI globally (cached)
RUN --mount=type=cache,target=/root/.npm \
    npm install -g @angular/cli@19

COPY shared/frontend-foundation/package.json shared/frontend-foundation/package-lock.json shared/frontend-foundation/ng-package.json ./shared/frontend-foundation/
COPY shared/frontend-foundation/tsconfig.lib.json ./shared/frontend-foundation/
COPY shared/frontend-foundation/README.md shared/frontend-foundation/CHANGELOG.md shared/frontend-foundation/COMPATIBILITY.md shared/frontend-foundation/LICENSE ./shared/frontend-foundation/
COPY shared/frontend-foundation/docs ./shared/frontend-foundation/docs
COPY shared/frontend-foundation/src ./shared/frontend-foundation/src
COPY shared/frontend-foundation/auth ./shared/frontend-foundation/auth
COPY shared/frontend-foundation/contracts ./shared/frontend-foundation/contracts
COPY shared/frontend-foundation/domain ./shared/frontend-foundation/domain
COPY shared/frontend-foundation/forms ./shared/frontend-foundation/forms
COPY shared/frontend-foundation/i18n ./shared/frontend-foundation/i18n
COPY shared/frontend-foundation/query ./shared/frontend-foundation/query
COPY shared/frontend-foundation/ui ./shared/frontend-foundation/ui
RUN cd shared/frontend-foundation && npm install --legacy-peer-deps --ignore-scripts && npm run build
WORKDIR /workspace/src/Frontend/his-hope-app
# Copy the application lockfile and the local workspace dependency before
# installing. The foundation is private and is intentionally resolved from
# the repository, not from the public npm registry.
COPY src/Frontend/his-hope-app/package.json src/Frontend/his-hope-app/package-lock.json ./
COPY shared /workspace/shared
RUN --mount=type=cache,target=/root/.npm \
    npm ci --legacy-peer-deps --ignore-scripts --no-audit --no-fund

# Copy the app and the shared design system used by its Sass entrypoints.
COPY src/Frontend/his-hope-app .

# The application consumes the foundation through a local file dependency.
# npm has already completed the foundation's lifecycle above, so point its
# dependency lookup at the application's single installed Angular tree. This
# avoids duplicate Angular type identities while keeping peer dependencies
# resolvable from the built package.
RUN rm -rf /workspace/shared/frontend-foundation/node_modules && \
    ln -s /workspace/src/Frontend/his-hope-app/node_modules /workspace/shared/frontend-foundation/node_modules

# Build with production configuration
RUN npm run build -- --configuration production

# ============================================================
# RUNTIME STAGE
# ============================================================
FROM nginxinc/nginx-unprivileged:alpine

# Install curl for healthcheck + tzdata for hospital timezone support
USER root
RUN apk add --no-cache curl tzdata && \
    rm -f /usr/share/nginx/html/index.html /usr/share/nginx/html/50x.html
USER nginx

# Copy built Angular app (localized under /en/)
COPY --from=build /workspace/src/Frontend/his-hope-app/dist/browser/en/ /usr/share/nginx/html/en/

# Copy nginx config
COPY src/Frontend/his-hope-app/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD ["curl", "-f", "-s", "-o", "/dev/null", "http://localhost:8080/"]

CMD ["nginx", "-g", "daemon off;"]
