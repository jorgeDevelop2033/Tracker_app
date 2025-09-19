# ============================
# Etapa 1: Build
# ============================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias
COPY Tracker.WebSocket.csproj .
RUN dotnet restore Tracker.WebSocket.csproj

# Copiar todo y compilar
COPY . .
RUN dotnet publish Tracker.WebSocket.csproj -c Release -o /app/publish /p:UseAppHost=false

# ============================
# Etapa 2: Runtime
# ============================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copiar los archivos publicados desde la etapa de build
COPY --from=build /app/publish .

# Crear carpeta de logs
RUN mkdir -p /app/Logs

# Exponer puerto (80 dentro del contenedor)
EXPOSE 80

# Entrypoint
ENTRYPOINT ["dotnet", "Tracker.WebSocket.dll"]
