# syntax=docker/dockerfile:1.6

# Stage 1 — build Vite
FROM node:20-alpine AS build
WORKDIR /src

COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci

COPY apps/web/ ./
ENV VITE_API_URL=/api
RUN npm run build

# Stage 2 — nginx servindo estáticos + proxy /api
FROM nginx:1.27-alpine

COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html

EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget -qO- http://localhost/ > /dev/null || exit 1
