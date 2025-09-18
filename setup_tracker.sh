#!/bin/bash
set -e

BASE_PATH="src/Services/Tracker"
SOLUTION_NAME="Tracker"

# Crear carpeta base
mkdir -p "$BASE_PATH"

# Crear solución
dotnet new sln -n $SOLUTION_NAME -o $BASE_PATH

# Crear proyectos en orden fijo
dotnet new classlib -n Tracker.Domain         -o "$BASE_PATH/Tracker.Domain"         --framework net9.0
dotnet new classlib -n Tracker.Infrastructure -o "$BASE_PATH/Tracker.Infrastructure" --framework net9.0
dotnet new classlib -n Tracker.Application    -o "$BASE_PATH/Tracker.Application"    --framework net9.0
dotnet new webapi   -n Tracker.API            -o "$BASE_PATH/Tracker.API"            --framework net9.0

# Agregar proyectos a la solución
dotnet sln "$BASE_PATH/$SOLUTION_NAME.sln" add "$BASE_PATH/Tracker.Domain"
dotnet sln "$BASE_PATH/$SOLUTION_NAME.sln" add "$BASE_PATH/Tracker.Infrastructure"
dotnet sln "$BASE_PATH/$SOLUTION_NAME.sln" add "$BASE_PATH/Tracker.Application"
dotnet sln "$BASE_PATH/$SOLUTION_NAME.sln" add "$BASE_PATH/Tracker.API"

# Agregar referencias entre capas (Clean Architecture)
dotnet add "$BASE_PATH/Tracker.Infrastructure/Tracker.Infrastructure.csproj" reference "$BASE_PATH/Tracker.Domain/Tracker.Domain.csproj"
dotnet add "$BASE_PATH/Tracker.Application/Tracker.Application.csproj" reference "$BASE_PATH/Tracker.Domain/Tracker.Domain.csproj"
dotnet add "$BASE_PATH/Tracker.Application/Tracker.Application.csproj" reference "$BASE_PATH/Tracker.Infrastructure/Tracker.Infrastructure.csproj"
dotnet add "$BASE_PATH/Tracker.API/Tracker.API.csproj" reference "$BASE_PATH/Tracker.Application/Tracker.Application.csproj"

# Crear carpetas
folders=(
  "Tracker.Domain/Entities"
  "Tracker.Domain/Enums"
  "Tracker.Domain/ValueObjects"
  "Tracker.Domain/Events"
  "Tracker.Domain/Abstractions"
  "Tracker.Infrastructure/DbContexts"
  "Tracker.Infrastructure/Configurations"
  "Tracker.Infrastructure/Repositories"
  "Tracker.Application/Commands/RegistrarPasada"
  "Tracker.Application/Commands/SubirBoleta"
  "Tracker.Application/Commands/ConciliarPasadas"
  "Tracker.Application/Queries"
  "Tracker.API/Controllers"
)

for folder in "${folders[@]}"; do
  mkdir -p "$BASE_PATH/$folder"
done

# Archivos vacíos
declare -A files
files["Tracker.Domain/Entities"]="Autopista.cs Portico.cs Tramo.cs TarifaTramo.cs Vehiculo.cs EventoPasada.cs Boleta.cs DetalleBoleta.cs"
files["Tracker.Domain/Enums"]="CategoriaVehiculo.cs EstadoEvento.cs"
files["Tracker.Domain/ValueObjects"]="Patente.cs Coordenada.cs Dinero.cs"
files["Tracker.Domain/Events"]="PasadaRegistradaEvent.cs"
files["Tracker.Domain/Abstractions"]="Entity.cs IAggregateRoot.cs"
files["Tracker.Infrastructure/DbContexts"]="TrackerDbContext.cs"
files["Tracker.Infrastructure/Configurations"]="AutopistaConfig.cs PorticoConfig.cs TramoConfig.cs TarifaTramoConfig.cs VehiculoConfig.cs EventoPasadaConfig.cs BoletaConfig.cs DetalleBoletaConfig.cs"
files["Tracker.Infrastructure/Repositories"]="GenericRepository.cs"
files["Tracker.Application/Commands/RegistrarPasada"]="RegistrarPasadaCommand.cs RegistrarPasadaHandler.cs RegistrarPasadaValidator.cs"
files["Tracker.Application/Queries"]="GetEventosPorVehiculoQuery.cs GetConciliacionQuery.cs GetReporteTenantQuery.cs"
files["Tracker.API/Controllers"]="EventosController.cs BoletasController.cs PorticosController.cs"
files["Tracker.API"]="Program.cs appsettings.json"

for folder in "${!files[@]}"; do
  for file in ${files[$folder]}; do
    touch "$BASE_PATH/$folder/$file"
  done
done

echo "✅ Solución, proyectos, referencias y estructura de carpetas/archivos generada en: $BASE_PATH"
