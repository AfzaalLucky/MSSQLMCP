# Stage 1 — build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/SqlMcpServer.Host/SqlMcpServer.Host.csproj",           "SqlMcpServer.Host/"]
COPY ["src/SqlMcpServer.Application/SqlMcpServer.Application.csproj", "SqlMcpServer.Application/"]
COPY ["src/SqlMcpServer.Infrastructure/SqlMcpServer.Infrastructure.csproj", "SqlMcpServer.Infrastructure/"]
COPY ["src/SqlMcpServer.CrossCutting/SqlMcpServer.CrossCutting.csproj", "SqlMcpServer.CrossCutting/"]
COPY ["src/SqlMcpServer.Domain/SqlMcpServer.Domain.csproj",        "SqlMcpServer.Domain/"]

RUN dotnet restore "SqlMcpServer.Host/SqlMcpServer.Host.csproj"

COPY src/ .

RUN dotnet publish "SqlMcpServer.Host/SqlMcpServer.Host.csproj" \
    --no-restore \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Stage 2 — runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user
RUN addgroup --system --gid 1000 appgroup && \
    adduser  --system --uid 1000 --ingroup appgroup --no-create-home appuser

RUN mkdir -p /var/log/sqlmcpserver && \
    chown appuser:appgroup /var/log/sqlmcpserver

COPY --from=build --chown=appuser:appgroup /app/publish ./

USER appuser

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
  CMD dotnet SqlMcpServer.Host.dll --health-check || exit 1

ENTRYPOINT ["dotnet", "SqlMcpServer.Host.dll"]
