FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["nuget.config", "."]

COPY ["services/SisMaster.WebApps.WebApi/SisMaster.WebApps.WebApi.csproj", "services/SisMaster.WebApps.WebApi/"]
COPY ["building_blocks/SisMaster.Core/SisMaster.Core.csproj", "building_blocks/SisMaster.Core/"]
COPY ["building_blocks/SisMaster.WebApi.Core/SisMaster.WebApi.Core.csproj", "building_blocks/SisMaster.WebApi.Core/"]
RUN dotnet restore "services/SisMaster.WebApps.WebApi/SisMaster.WebApps.WebApi.csproj"

COPY . .
RUN dotnet publish "services/SisMaster.WebApps.WebApi/SisMaster.WebApps.WebApi.csproj" \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SisMaster.WebApps.WebApi.dll"]
