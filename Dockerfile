FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MySqlMcpServer.csproj ./
RUN dotnet restore MySqlMcpServer.csproj

COPY . .
RUN dotnet publish MySqlMcpServer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "MySqlMcpServer.dll"]
