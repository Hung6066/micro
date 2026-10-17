FROM node:22-alpine AS build
WORKDIR /workspace

COPY package.json package-lock.json ./
COPY shared/frontend-foundation/package.json shared/frontend-foundation/ng-package.json ./shared/frontend-foundation/
COPY shared/frontend-foundation/tsconfig.lib.json ./shared/frontend-foundation/
COPY internal-operator-app/package.json ./internal-operator-app/
COPY shared /workspace/shared

# The foundation package has a developer-facing `prepare` hook. Disable that
# hook during dependency installation so the package is built once, after all
# source files are present, instead of racing ng-packagr entry points in npm ci.
RUN npm pkg delete scripts.prepare --workspace @his-hope/frontend-foundation
RUN npm ci --legacy-peer-deps --ignore-scripts

COPY internal-operator-app /workspace/internal-operator-app
RUN rm -rf /workspace/shared/frontend-foundation/dist && npm run build:shared
WORKDIR /workspace/internal-operator-app
RUN rm -rf .angular/cache && npx ng build --configuration=production

FROM nginxinc/nginx-unprivileged:alpine AS final
USER 101
COPY --from=build /workspace/internal-operator-app/dist/internal-operator-app/browser /usr/share/nginx/html
COPY internal-operator-app/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/health || exit 1
