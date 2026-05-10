# syntax=docker/dockerfile:1.6

# Stage 1 — build do .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY apps/api/Api.csproj apps/api/
RUN dotnet restore apps/api/Api.csproj
COPY apps/api/ apps/api/
RUN dotnet publish apps/api/Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 2 — runtime com Python para o extrator
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

RUN apt-get update \
 && apt-get install -y --no-install-recommends python3 python3-pip python3-venv \
 && rm -rf /var/lib/apt/lists/*

# Cria venv isolado para o extrator e instala dependências
COPY apps/extractor/requirements.txt /opt/extractor/requirements.txt
RUN python3 -m venv /opt/extractor/venv \
 && /opt/extractor/venv/bin/pip install --no-cache-dir --upgrade pip \
 && /opt/extractor/venv/bin/pip install --no-cache-dir -r /opt/extractor/requirements.txt

COPY apps/extractor/ /opt/extractor/

# Binários do .NET
COPY --from=build /app/publish /app

# Diretórios de runtime
RUN mkdir -p /data/uploads /data/db

ENV ASPNETCORE_URLS=http://+:5074 \
    ASPNETCORE_ENVIRONMENT=Production \
    APP_Storage__UploadPath=/data/uploads \
    APP_Database__Path=/data/db/salarios.db \
    APP_Extractor__PythonPath=/opt/extractor/venv/bin/python3 \
    APP_Extractor__ScriptPath=/opt/extractor/main.py

EXPOSE 5074

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -fsS http://localhost:5074/api/health || exit 1

# curl para healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "Api.dll"]
