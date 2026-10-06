# Build context: frontend/ (see docker-compose.yml).

FROM node:24-alpine AS build
WORKDIR /app
ENV NG_CLI_ANALYTICS=false
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npx ng build --configuration production

FROM nginx:1.28-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
# The application builder writes to dist/<project>/browser (angular.json has no custom outputPath).
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
EXPOSE 80
