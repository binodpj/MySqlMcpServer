# Stage 1: Restore NuGet dependencies (cached unless .csproj changes)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY MySqlMcpServer.csproj ./
RUN dotnet restore MySqlMcpServer.csproj --use-current-runtime -p:SelfContained=true

# Stage 2: Build application
FROM restore AS build
COPY . .
RUN dotnet build MySqlMcpServer.csproj -c Release --no-restore --use-current-runtime -p:SelfContained=true

# Stage 3: Publish self-contained, single-file, partially trimmed binary without debug symbols
FROM build AS publish
RUN dotnet publish MySqlMcpServer.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    --use-current-runtime \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial \
    -p:DebugType=none \
    -p:DebugSymbols=false

# Stage 4: Minimal distroless runtime-deps image (no SDK, no shell, no package manager)
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled-extra AS final
WORKDIR /app

ENV DOTNET_EnableDiagnostics=0

COPY --from=publish /app/publish .

USER $APP_UID

ENTRYPOINT ["./MySqlMcpServer"]
