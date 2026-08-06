# See https://aka.ms/customizecontainer for how Visual Studio uses this Dockerfile when debugging.

# ---- Base runtime ----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

# tzdata provides the IANA time zone database. The once-per-day rule is evaluated in the team's
# local zone, and without this the lookup for "Australia/Melbourne" fails and the app silently
# falls back to UTC — which would roll the day over at the wrong time.
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*

# Runs as the image's non-root user, so a compromised process does not have root in the container.
USER app
WORKDIR /app
EXPOSE 8080

# ---- Build -----------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# The project file is restored before the rest of the source is copied so that the restore layer is
# cached and only re-runs when dependencies actually change.
COPY ["InterviewProjectTemplate.csproj", "InterviewProjectTemplate/"]
RUN dotnet restore "./InterviewProjectTemplate/InterviewProjectTemplate.csproj"

WORKDIR "/src/InterviewProjectTemplate"
COPY . .
RUN dotnet build "./InterviewProjectTemplate.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# ---- Publish ---------------------------------------------------------------------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./InterviewProjectTemplate.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ---- Final -----------------------------------------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Binds HTTP only. The container has no certificate for 8081, so serving HTTPS here would fail;
# TLS belongs at the ingress in a real deployment.
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "InterviewProjectTemplate.dll"]
