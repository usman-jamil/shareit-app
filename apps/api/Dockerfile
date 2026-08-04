FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
# Stamped by .github/workflows/release-share-api.yml from the release tag. A local build that
# does not pass it gets the placeholder rather than failing.
ARG VERSION=0.0.0
WORKDIR /src
# Central Package Management: restore cannot resolve versions without these.
COPY ["Directory.Packages.props", "Directory.Build.props", "./"]
COPY ["apps/api/Api.csproj", "apps/api/"]
COPY ["libs/api/infrastructure/Infrastructure.csproj", "libs/api/infrastructure/"]
COPY ["libs/api/application/Application.csproj", "libs/api/application/"]
COPY ["libs/api/domain/Domain.csproj", "libs/api/domain/"]
COPY ["libs/shared/shared-kernal/SharedKernel.csproj", "libs/shared/shared-kernal/"]
RUN dotnet restore "apps/api/Api.csproj"
COPY . .
WORKDIR "/src/apps/api"
RUN dotnet build "./Api.csproj" -c $BUILD_CONFIGURATION -p:Version=$VERSION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# Re-declared: an ARG does not carry across stages, even one built FROM another.
ARG VERSION=0.0.0
RUN dotnet publish "./Api.csproj" -c $BUILD_CONFIGURATION -p:Version=$VERSION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
