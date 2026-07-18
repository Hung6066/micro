# syntax=docker/dockerfile:1

# ============================================================
# BUILD STAGE (Angular 17)
# ============================================================
FROM node:20-alpine AS build
WORKDIR /app

# Install Angular CLI globally (cached)
RUN --mount=type=cache,target=/root/.npm \
    npm install -g @angular/cli@19

# Copy package files and install dependencies with cache
COPY package.json package-lock.json ./
RUN --mount=type=cache,target=/root/.npm \
    npm ci

# Copy source code
COPY . .

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
COPY --from=build /app/dist/browser/en/ /usr/share/nginx/html/en/

# Copy nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD ["curl", "-f", "-s", "-o", "/dev/null", "http://localhost:8080/"]

CMD ["nginx", "-g", "daemon off;"]
