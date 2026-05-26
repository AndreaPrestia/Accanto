FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm install --no-audit --no-fund
COPY . .
# URL pubblici del sito vetrina e dell'app SPA (sovrascrivibili da build args).
ARG SITE_URL=http://localhost:4321
ARG PUBLIC_APP_URL=http://localhost:5173
ENV SITE_URL=$SITE_URL
ENV PUBLIC_APP_URL=$PUBLIC_APP_URL
RUN npm run build

FROM nginx:1.27-alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q -O - http://localhost/ >/dev/null || exit 1
