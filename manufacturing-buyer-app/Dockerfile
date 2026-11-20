FROM node:22-alpine AS build
WORKDIR /workspace

COPY package.json package-lock.json ./
COPY shared/frontend-foundation/package.json shared/frontend-foundation/ng-package.json ./shared/frontend-foundation/
COPY shared/frontend-foundation/tsconfig.lib.json ./shared/frontend-foundation/
COPY manufacturing-buyer-app/package.json ./manufacturing-buyer-app/
COPY shared /workspace/shared

RUN npm pkg delete scripts.prepare --workspace @his-hope/frontend-foundation
RUN npm ci --legacy-peer-deps --ignore-scripts

COPY manufacturing-buyer-app /workspace/manufacturing-buyer-app
RUN rm -rf /workspace/shared/frontend-foundation/dist && npm run build:shared
WORKDIR /workspace/manufacturing-buyer-app
RUN rm -rf .angular/cache && npx ng build --configuration=production

FROM ghcr.io/nginx/nginx-unprivileged:1.30.7-alpine3.24-slim@sha256:8432c57ac86f178741af2b89948bd9087cb9df1c415bc1ab4d23735c4f0e3e61 AS final
USER 101
COPY --from=build /workspace/manufacturing-buyer-app/dist/manufacturing-buyer-app/browser /usr/share/nginx/html
COPY manufacturing-buyer-app/nginx.conf /etc/nginx/conf.d/default.conf
COPY manufacturing-buyer-app/runtime-config.openship.js.template /etc/nginx/runtime-config.openship.js.template
COPY --chmod=755 manufacturing-buyer-app/docker-entrypoint.sh /usr/local/bin/buyer-entrypoint
ENTRYPOINT ["/usr/local/bin/buyer-entrypoint"]
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget -q -O /dev/null http://127.0.0.1:8080/health || exit 1
