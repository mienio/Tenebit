# syntax=docker/dockerfile:1.7
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run lint -- --max-warnings 0 && npm test && npm run build

FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
COPY security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080 8443
USER 101
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 CMD wget -q -O /dev/null --no-check-certificate https://127.0.0.1:8443/healthz || exit 1
ENTRYPOINT ["nginx", "-g", "daemon off;"]
