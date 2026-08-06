# Artifact-packaging image. Nx builds the React app and publishes both .NET applications outside
# Docker (see apps/app-image/tools/stage-artifacts.sh); this only packages the results. There is no
# SDK stage, so the final image carries no Node.js, no npm, no Nx and no .NET SDK — just the
# ASP.NET Core runtime and the published output.
#
# The build context is the staged directory dist/apps/app-image/context, not the repository root.
FROM mcr.microsoft.com/dotnet/aspnet:10.0

# The entrypoint depends on `wait -n -p`, and the container health check runs `dotnet`. Both ship in
# this base image; assert the first here so a base-image change fails the build rather than the
# container.
RUN /bin/bash -c 'exit 0'

ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    APP_DIR=/app \
    APP_IMAGE_WEB_ROOT=/app/web \
    APP_IMAGE_API_DESTINATION=http://127.0.0.1:5000/

WORKDIR /app

COPY host/ ./host/
COPY api/ ./api/
COPY web/ ./web/
COPY app.env ./app.env
COPY docker-entrypoint.sh ./docker-entrypoint.sh

RUN chmod 0755 ./docker-entrypoint.sh

# APP_UID is defined by the base image (a non-root account that owns nothing under /app — the
# application only ever reads from it).
USER $APP_UID

# Only the app host is published. The API listens on loopback:5000 inside the container and is
# deliberately not exposed.
EXPOSE 8080

# No curl or wget in this image, and adding one means an apt layer plus its CVE surface for a single
# HTTP GET. The host implements the probe itself instead; see apps/app-image/Health/HealthProbe.cs.
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD ["dotnet", "/app/host/AppImage.Host.dll", "--healthcheck"]

ENTRYPOINT ["/app/docker-entrypoint.sh"]
