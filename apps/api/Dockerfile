FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apps/api/Api.csproj", "apps/api/"]
COPY ["libs/infrastructure/Infrastructure.csproj", "libs/infrastructure/"]
COPY ["libs/application/Application.csproj", "libs/application/"]
COPY ["libs/domain/Domain.csproj", "libs/domain/"]
COPY ["libs/shared-kernal/SharedKernel.csproj", "libs/shared-kernal/"]
RUN dotnet restore "apps/api/Api.csproj"
COPY . .
WORKDIR "/src/apps/api"
RUN dotnet build "./Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
