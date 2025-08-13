# Use the official .NET 9.0 runtime as the base image
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

# Use the official .NET 9.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/SafariBooksDownloader/SafariBooksDownloader.csproj", "src/SafariBooksDownloader/"]
RUN dotnet restore "src/SafariBooksDownloader/SafariBooksDownloader.csproj"
COPY . .
WORKDIR "/src/src/SafariBooksDownloader"
RUN dotnet build "SafariBooksDownloader.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SafariBooksDownloader.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create volumes for Books output and cookies
VOLUME ["/app/Books", "/app/cookies.json"]

ENTRYPOINT ["dotnet", "SafariBooksDownloader.dll"]