# PriceVision

Proyecto con arquitectura separada en `frontend` (Angular) y `backend` (.NET 10) para estimación de recursos en proyectos de construcción.

## Estructura de carpetas y archivos

```text
PriceVision/
├─ backend/
│  ├─ PriceVision.slnx
│  ├─ PriceVision.Api/
│  │  ├─ Program.cs
│  │  ├─ PriceVision.Api.csproj
│  │  ├─ appsettings.json
│  │  ├─ appsettings.Development.json
│  │  ├─ PriceVision.Api.http
│  │  ├─ Properties/
│  │  │  └─ launchSettings.json
│  │  ├─ Artifacts/
│  │  │  ├─ synthetic-training-data.csv
│  │  │  ├─ materials-model.zip
│  │  │  ├─ labor-model.zip
│  │  │  └─ model-version.txt
│  │  └─ pricevision.db
│  ├─ PriceVision.Domain/
│  │  ├─ PriceVision.Domain.csproj
│  │  └─ Entities/
│  │     └─ Prediction.cs
│  ├─ PriceVision.Application/
│  │  ├─ PriceVision.Application.csproj
│  │  ├─ Abstractions/
│  │  │  ├─ IModelTrainingService.cs
│  │  │  ├─ IPredictiveModelService.cs
│  │  │  └─ IPredictionRepository.cs
│  │  └─ Contracts/
│  │     ├─ PredictionRequest.cs
│  │     ├─ MaterialsEstimate.cs
│  │     ├─ PredictionResult.cs
│  │     └─ TrainingResult.cs
│  └─ PriceVision.Infrastructure/
│     ├─ PriceVision.Infrastructure.csproj
│     ├─ DependencyInjection.cs
│     ├─ Ml/
│     │  ├─ SyntheticDatasetGenerator.cs
│     │  ├─ ModelTrainingService.cs
│     │  ├─ PredictiveModelService.cs
│     │  ├─ PredictionTrainingRow.cs
│     │  ├─ PredictionInputModel.cs
│     │  └─ RegressionPrediction.cs
│     └─ Persistence/
│        ├─ PriceVisionDbContext.cs
│        └─ PredictionRepository.cs
├─ frontend/
│  └─ pricevision-ui/
│     ├─ angular.json
│     ├─ package.json
│     ├─ proxy.conf.json
│     ├─ server.ts
│     ├─ src/
│     │  ├─ main.ts
│     │  ├─ index.html
│     │  ├─ styles.scss
│     │  ├─ environments/
│     │  │  └─ environment.ts
│     │  └─ app/
│     │     ├─ app.component.ts
│     │     ├─ app.component.html
│     │     ├─ app.component.scss
│     │     ├─ app.routes.ts
│     │     ├─ app.config.ts
│     │     ├─ app.config.server.ts
│     │     └─ core/services/api.service.ts
│     └─ README.md
└─ .gitignore
```

## Cambios implementados para la HU #3

Historia de usuario:
`Como Project Manager quiero obtener una estimación automática de recursos para planificar materiales y mano de obra.`

### 1) Modelo de regresión en C#

Se implementó en `Infrastructure/Ml` con ML.NET:

- `ModelTrainingService.cs`:
  - Genera dataset sintético de entrenamiento.
  - Entrena dos modelos de regresión:
    - Cantidad de materiales.
    - Horas-persona de mano de obra.
  - Guarda artefactos del modelo en `PriceVision.Api/Artifacts`.
- `PredictiveModelService.cs`:
  - Carga modelos entrenados.
  - Normaliza duración (`dias`/`meses`) a días.
  - Predice:
    - `MaterialesEstimados.Quantity`
    - `ManoObraRequeridaHorasPersona`
  - Calcula además costo estimado en COP para materiales.

### 2) Servicio PredictiveModel

Se creó mediante contrato y su implementación:

- Contrato: `Application/Abstractions/IPredictiveModelService.cs`
- Implementación: `Infrastructure/Ml/PredictiveModelService.cs`

También se agregó:

- `IModelTrainingService` para entrenamiento.
- `IPredictionRepository` para persistencia.

### 3) Persistencia en entidad Prediction

Se creó `Domain/Entities/Prediction.cs` y persistencia con EF Core SQLite:

- `PriceVisionDbContext.cs`
- `PredictionRepository.cs`
- Configuración DI en `Infrastructure/DependencyInjection.cs`
- Connection string en `Api/appsettings.json`

Campos persistidos relevantes:

- Entradas: `AreaM2`, `Type`, `Location`, `DurationDays`
- Salidas: `EstimatedMaterialQuantity`, `EstimatedMaterialCostCop`, `RequiredLaborHours`
- Trazabilidad: `ModelVersion`, `CreatedAtUtc`

### 4) API expuesta para entrenamiento y predicción

Endpoints en `Api/Program.cs`:

- `POST /api/predictions/train`
- `POST /api/predictions`
- `GET /api/predictions`
- `GET /api/predictions/{id}`
- `GET /api/health`

### 5) Dataset histórico (estado actual)

Para desarrollo inicial se dejó flujo con dataset sintético (`synthetic-training-data.csv`) para habilitar pruebas y reemplazo posterior por dataset histórico real sin cambiar el contrato del API.

## Comandos para correr y probar

## Requisitos

- .NET SDK 10
- Node.js + npm (para frontend)

## Backend

Restaurar y compilar:

```powershell
dotnet restore backend\PriceVision.Api\PriceVision.Api.csproj
dotnet build backend\PriceVision.Api\PriceVision.Api.csproj
```

Ejecutar API (perfil HTTP):

```powershell
dotnet run --project backend\PriceVision.Api\PriceVision.Api.csproj --launch-profile http
```

URL esperada:

- `http://localhost:5054`

Entrenar modelo (PowerShell):

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5054/api/predictions/train" `
  -ContentType "application/json" `
  -Body '{"rows":3000}'
```

Predecir (PowerShell):

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5054/api/predictions" `
  -ContentType "application/json" `
  -Body '{
    "areaM2": 850,
    "type": "Comercial",
    "location": "Bogota",
    "duration": 10,
    "durationUnit": "meses"
  }'
```

Consultar historial:

```powershell
Invoke-RestMethod -Method Get -Uri "http://localhost:5054/api/predictions?take=10"
```

## Frontend

Instalar dependencias:

```powershell
cd frontend\pricevision-ui
npm install
```

Ejecutar en desarrollo:

```powershell
npm start
```

## Notas de prueba funcional para HU #3

- Entrenar primero con `POST /api/predictions/train`.
- Luego usar `POST /api/predictions` con `areaM2`, `type`, `location`, `duration`, `durationUnit`.
- Verificar respuesta con:
  - `materialesEstimados.quantity`
  - `manoObraRequeridaHorasPersona`
- Verificar persistencia en `GET /api/predictions`.

