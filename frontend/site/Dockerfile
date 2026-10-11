# Site público (Next.js): compila e roda como servidor Node enxuto (output: standalone).
FROM node:22-alpine AS build
WORKDIR /site
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:22-alpine
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME=0.0.0.0
COPY --from=build /site/.next/standalone ./
COPY --from=build /site/.next/static ./.next/static
COPY --from=build /site/public ./public
USER node
EXPOSE 3000
CMD ["node", "server.js"]
