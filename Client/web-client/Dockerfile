# ---- Build stage -----------------------------------------------------------------------------
# Node 20 rather than 18: Angular 18 requires ^18.19.1 || ^20.11.1 || ^22, and pinning to a
# maintained LTS avoids depending on whichever 18.x the `node:18` tag happens to resolve to.
#
# Debian-based rather than Alpine: the Angular build toolchain pulls in `lmdb`, whose prebuilt
# native binaries target glibc. On Alpine's musl libc no prebuilt matches, so npm falls back to
# compiling from source and fails — Alpine ships neither Python nor a C++ toolchain. The larger
# image costs nothing in the deployed artefact because this stage is discarded; only the nginx
# runtime stage below is shipped.
FROM node:20 AS build

WORKDIR /app

# The npm version is pinned rather than inherited from the base image. node:20 ships npm 10.8.2,
# which resolves this dependency set into a lockfile it then refuses to validate — `sass` requires
# chokidar 4 while `karma` requires chokidar 3, and npm 10 writes a tree that fails its own
# `npm ci` consistency check. npm 11 resolves the split correctly. Pinning also means a future
# base-image refresh cannot silently change how dependencies are resolved.
RUN npm install -g npm@11.19.0

# Dependencies are installed before the source is copied so Docker can cache this layer; it only
# re-runs when the lockfile changes, not on every source edit.
COPY package.json package-lock.json ./

# `npm ci` rather than `npm install`: it installs exactly what the lockfile pins, so the image is
# reproducible and a build cannot silently pick up a newer transitive dependency.
RUN npm ci

COPY . .

RUN npm run build -- --configuration production

# ---- Runtime stage ---------------------------------------------------------------------------
# A built Angular app is static files, so the runtime image needs a web server, not Node.
FROM nginx:alpine

# Angular 17+ uses the `application` builder, which emits browser assets into a `browser`
# subdirectory. The template copied from `dist/web-client`, which under this builder contains only
# that subdirectory plus a licence file — nginx would have served a directory listing, not the app.
COPY --from=build /app/dist/web-client/browser /usr/share/nginx/html

# Replaces the default site with one that falls back to index.html, so client-side routes work.
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost/ || exit 1
