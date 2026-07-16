FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build -- --configuration production

FROM nginxinc/nginx-unprivileged:alpine
USER root
RUN apk add --no-cache curl
USER nginx
COPY --from=build /app/dist/ /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
  CMD curl -f -s -o /dev/null http://localhost:8080/ || exit 1
CMD ["nginx", "-g", "daemon off;"]
