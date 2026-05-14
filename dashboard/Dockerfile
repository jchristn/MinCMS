FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm install
COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
RUN apk add --no-cache curl wget busybox-extras bind-tools vim
COPY --from=build /app/dist /usr/share/nginx/html
COPY docker-entrypoint.sh /usr/local/bin/mincms-entrypoint.sh
RUN sed -i 's/\r$//' /usr/local/bin/mincms-entrypoint.sh \
    && chmod +x /usr/local/bin/mincms-entrypoint.sh
EXPOSE 8300
ENTRYPOINT ["/usr/local/bin/mincms-entrypoint.sh"]
