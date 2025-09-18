#!/bin/bash
set -e

BASE_PATH="src/Services/Tracker"
SOLUTION_PATH="$BASE_PATH/Tracker.sln"

# Crear proyecto WebSocket (puede ser un webapi minimal o consola con Kestrel)
dotnet new webapi -n Tracker.WebSocket -o "$BASE_PATH/Tracker.WebSocket" --framework net9.0
dotnet sln "$SOLUTION_PATH" add "$BASE_PATH/Tracker.WebSocket"

# Crear proyecto Worker (background service)
dotnet new worker -n Tracker.Worker -o "$BASE_PATH/Tracker.Worker" --framework net9.0
dotnet sln "$SOLUTION_PATH" add "$BASE_PATH/Tracker.Worker"

# Agregar referencias (ambos consumen Application)
dotnet add "$BASE_PATH/Tracker.WebSocket/Tracker.WebSocket.csproj" reference "$BASE_PATH/Tracker.Application/Tracker.Application.csproj"
dotnet add "$BASE_PATH/Tracker.Worker/Tracker.Worker.csproj" reference "$BASE_PATH/Tracker.Application/Tracker.Application.csproj"

echo "✅ Proyectos Tracker.WebSocket y Tracker.Worker agregados a la solución con sus referencias."
