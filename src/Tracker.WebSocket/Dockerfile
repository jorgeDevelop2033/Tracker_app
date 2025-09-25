# Dockerfile (para carpeta de publicación)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

# Copiamos TODO el output publicado
COPY . .

# Asegúrate que este nombre DLL existe en la carpeta publicada
ENTRYPOINT ["dotnet", "Tracker.WebSocket.dll"]
