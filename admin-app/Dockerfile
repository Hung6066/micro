FROM node:22-alpine AS build
WORKDIR /workspace
COPY shared/frontend-foundation/package.json shared/frontend-foundation/package-lock.json shared/frontend-foundation/ng-package.json ./shared/frontend-foundation/
COPY shared/frontend-foundation/tsconfig.lib.json ./shared/frontend-foundation/
COPY shared/frontend-foundation/README.md shared/frontend-foundation/CHANGELOG.md shared/frontend-foundation/COMPATIBILITY.md shared/frontend-foundation/LICENSE ./shared/frontend-foundation/
COPY shared/frontend-foundation/docs ./shared/frontend-foundation/docs
COPY shared/frontend-foundation/src ./shared/frontend-foundation/src
RUN cd shared/frontend-foundation && npm ci --legacy-peer-deps --ignore-scripts && npm run build
# The application installs the file workspace next; remove the library's
# private Angular tree so peer dependencies resolve from the application root.
WORKDIR /workspace/admin-app
COPY admin-app/package.json admin-app/package-lock.json ./
RUN npm ci --legacy-peer-deps
COPY shared /workspace/shared
COPY admin-app .
RUN rm -rf /workspace/shared/frontend-foundation/node_modules
RUN rm -rf .angular/cache && npx ng build --configuration=production

FROM nginxinc/nginx-unprivileged:alpine AS final
USER 101
COPY --from=build /workspace/admin-app/dist/admin-app/browser /usr/share/nginx/html
COPY admin-app/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/ || exit 1
