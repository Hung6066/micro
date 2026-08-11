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
RUN cd shared/frontend-foundation && npm install --legacy-peer-deps --ignore-scripts && npm run build

WORKDIR /workspace/src/Frontend/his-hope-app
# Copy package files and install dependencies with cache
# The application lockfile is intentionally not committed in this legacy
# workspace; install from the manifest while the shared foundation remains
# lockfile-pinned above.
COPY src/Frontend/his-hope-app/package.json ./
RUN --mount=type=cache,target=/root/.npm \
    npm install --legacy-peer-deps --ignore-scripts --no-audit --no-fund

# Copy the app and the shared design system used by its Sass entrypoints.
COPY shared /workspace/shared
COPY src/Frontend/his-hope-app .

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
