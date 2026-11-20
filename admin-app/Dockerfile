FROM node:22-alpine AS build
WORKDIR /workspace
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
RUN cd shared/frontend-foundation && npm ci --legacy-peer-deps --ignore-scripts && npm run build
WORKDIR /workspace/admin-app
COPY admin-app/package.json admin-app/package-lock.json ./
RUN npm ci --legacy-peer-deps && rm -rf /workspace/shared/frontend-foundation/node_modules && ln -s /workspace/admin-app/node_modules /workspace/shared/node_modules
COPY shared /workspace/shared
COPY admin-app .
RUN rm -rf .angular/cache && npx ng build --configuration=production

FROM ghcr.io/nginx/nginx-unprivileged:1.30.7-alpine3.24-slim@sha256:8432c57ac86f178741af2b89948bd9087cb9df1c415bc1ab4d23735c4f0e3e61 AS final
USER root
RUN mkdir -p /etc/nginx/templates
COPY --from=build /workspace/admin-app/dist/admin-app/browser /usr/share/nginx/html
COPY admin-app/nginx.conf /etc/nginx/templates/admin.conf.template
COPY admin-app/runtime-config.openship.js.template /etc/nginx/templates/runtime-config.openship.js.template
COPY --chmod=755 admin-app/docker-entrypoint.sh /usr/local/bin/admin-entrypoint
RUN rm -f /etc/nginx/conf.d/default.conf \
    && ln -s /tmp/default.conf /etc/nginx/conf.d/default.conf
USER 101
ENTRYPOINT ["/usr/local/bin/admin-entrypoint"]
CMD ["nginx", "-g", "daemon off;"]
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/ || exit 1
