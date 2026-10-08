# 1) Compila o app de gestão (React + Vite) que será servido em /admin/
FROM node:22-alpine AS admin-build
WORKDIR /admin
COPY admin/package.json admin/package-lock.json ./
RUN npm ci
COPY admin/ ./
RUN npm run build

# 2) nginx: páginas estáticas (súmula/placar/mesário) na raiz + app de gestão em /admin/
FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY public/ /usr/share/nginx/html/
COPY --from=admin-build /admin/dist/ /usr/share/nginx/html/admin/
