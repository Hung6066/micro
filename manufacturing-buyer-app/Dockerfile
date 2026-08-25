FROM node:22-alpine AS build
WORKDIR /workspace

COPY . /workspace/manufacturing-buyer-app
COPY shared/frontend-foundation /workspace/shared/frontend-foundation

WORKDIR /workspace/shared/frontend-foundation
RUN npm install --no-audit --no-fund && npm run build

WORKDIR /workspace/manufacturing-buyer-app
RUN npm install --no-audit --no-fund
# npm installs file: dependencies as symlinks. Replace that link with the
# already-built package so Angular resolves peer dependencies from this app's
# node_modules inside the image.
RUN rm -rf node_modules/@his-hope/frontend-foundation \
  && mkdir -p node_modules/@his-hope/frontend-foundation \
  && cp -R /workspace/shared/frontend-foundation/dist/. node_modules/@his-hope/frontend-foundation/ \
  && mkdir -p node_modules/@his-hope/frontend-foundation/dist \
  && cp -R /workspace/shared/frontend-foundation/dist/. node_modules/@his-hope/frontend-foundation/dist/
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /workspace/manufacturing-buyer-app/dist/manufacturing-buyer-app/browser /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget -q -O /dev/null http://127.0.0.1:8080/ || exit 1
