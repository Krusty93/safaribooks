FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY --link ./src/Directory.Build.props .

COPY src/SafariBooksDownloader.Core/SafariBooksDownloader.Core.csproj SafariBooksDownloader.Core/SafariBooksDownloader.Core.csproj
COPY src/SafariBooksDownloader.App/SafariBooksDownloader.App.csproj SafariBooksDownloader.App/SafariBooksDownloader.App.csproj

RUN dotnet restore "SafariBooksDownloader.App/SafariBooksDownloader.App.csproj"

COPY src/SafariBooksDownloader.Core/ SafariBooksDownloader.Core/
COPY src/SafariBooksDownloader.App/ SafariBooksDownloader.App/

RUN dotnet build "SafariBooksDownloader.App/SafariBooksDownloader.App.csproj" \
  --no-restore \
  --configuration $BUILD_CONFIGURATION

FROM build AS publish
ARG BUILD_CONFIGURATION=Release

WORKDIR /src/SafariBooksDownloader.App

RUN dotnet publish "SafariBooksDownloader.App.csproj" \
  --configuration $BUILD_CONFIGURATION \
  --no-restore \
  --output /app/publish \
  /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

COPY --from=publish /app/publish .

VOLUME ["/Books", "/cookies.json"]

ENTRYPOINT ["dotnet", "SafariBooksDownloader.App.dll"]