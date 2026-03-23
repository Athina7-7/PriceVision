# PriceVision

Aplicacion con arquitectura separada en `frontend` (Angular 17) y `backend` (.NET 10) para registrar proyectos de construccion, generar predicciones de recursos y costo financiero, y calcular indicadores EVM.

## Estado actual

La app hoy trabaja con este flujo:

1. Registrar proyecto
2. Ejecutar predicciones por proyecto
3. Generar prediccion financiera
4. Calcular EVM
5. Consultar recientes e historial

Ya no se usa un `ProjectId` escrito manualmente como flujo principal. Primero se registra un proyecto y luego se selecciona desde listados en los modulos de `Predicciones` y `EVM`.

## Funcionalidades implementadas

### 1. Registro de proyectos

Permite guardar un proyecto con:

- nombre
- area en m2
- ubicacion
- tipo de proyecto
- duracion estimada en meses
- costos base en COP

Incluye:

- validaciones obligatorias en frontend y backend
- persistencia en SQLite
- advertencias no bloqueantes basadas en historicos

### 2. Predicciones de recursos

El sistema usa ML.NET para estimar:

- cantidad de materiales
- horas de mano de obra

Caracteristicas actuales:

- los modelos se entrenan con dataset sintetico generado por la aplicacion
- el usuario puede aplicar solo materiales, solo mano de obra, o completar el modelo faltante en un proyecto
- no se repite una prediccion ya realizada para el mismo tipo de modelo
- la vista muestra detalle de la prediccion y recientes

### 3. Prediccion financiera

Ademas del modelo de recursos, la app incluye una prediccion financiera asociada al proyecto.

Esta prediccion considera:

- recursos estimados
- costos historicos almacenados
- tendencia por ubicacion

Muestra:

- costo total estimado
- rango minimo y maximo
- nivel de confianza en porcentaje
- clasificacion de confianza (`Alto`, `Medio`, `Bajo`)

### 4. EVM

La app calcula indicadores de Earned Value Management:

- `PV`: Planned Value
- `EV`: Earned Value
- `AC`: Actual Cost
- `CPI`: Cost Performance Index
- `SPI`: Schedule Performance Index

Comportamiento actual:

- EVM se calcula por proyecto seleccionado
- requiere que el proyecto tenga predicciones de materiales y mano de obra
- no permite recalcular EVM si ya existe un registro para el proyecto
- muestra detalle y recientes en una vista dedicada

### 5. Historial y recientes

El sistema expone y muestra:

- historial del proyecto
- predicciones recientes
- predicciones financieras recientes
- EVM recientes

Cada detalle incluye informacion del proyecto:

- nombre
- area
- tipo
- ubicacion
- duracion
- costo base

## Arquitectura

```text
PriceVision/
|-- backend/
|   |-- PriceVision.Api/
|   |   |-- Program.cs
|   |   |-- appsettings.json
|   |   |-- Artifacts/
|   |   `-- pricevision.db
|   |-- PriceVision.Application/
|   |   |-- Abstractions/
|   |   `-- Contracts/
|   |-- PriceVision.Domain/
|   |   `-- Entities/
|   `-- PriceVision.Infrastructure/
|       |-- Forecasting/
|       |-- Ml/
|       |-- Persistence/
|       `-- Validation/
`-- frontend/
    `-- pricevision-ui/
        `-- src/app/
```

## Persistencia

La base de datos actual es SQLite y se guarda en:

- `backend/PriceVision.Api/pricevision.db`

### Tablas principales

#### `Projects`

Guarda el registro base del proyecto:

- `Id`
- `Name`
- `AreaM2`
- `Location`
- `Type`
- `DurationMonths`
- `BaseCostCop`
- `CreatedAtUtc`

#### `Predictions`

Guarda las predicciones de recursos:

- `ProjectId`
- `AreaM2`
- `Type`
- `Location`
- `DurationDays`
- `EstimatedMaterialQuantity`
- `EstimatedMaterialCostCop`
- `RequiredLaborHours`
- `PredictedMaterials`
- `PredictedLabor`
- `ModelVersion`
- `CreatedAtUtc`

#### `FinancialPredictions`

Guarda la prediccion financiera por proyecto:

- `ProjectId`
- `EstimatedTotalCostCop`
- `MinimumEstimatedCostCop`
- `MaximumEstimatedCostCop`
- `ConfidencePercentage`
- `ConfidenceLevel`
- `HistoricalAverageCostPerM2`
- `LocationTrendFactor`
- `CreatedAtUtc`

#### `EVM_Records`

Guarda calculos EVM:

- `ProjectId`
- `PeriodDateUtc`
- `PV`
- `EV`
- `AC`
- `CPI`
- `SPI`
- `CostInterpretation`
- `ScheduleInterpretation`
- `CreatedAtUtc`

## Endpoints principales

### Salud

- `GET /api/health`

### Proyectos

- `GET /api/projects`
- `POST /api/projects`
- `GET /api/projects/{projectId}/history`

### Predicciones de recursos

- `POST /api/projects/{projectId}/predict`
- `GET /api/predictions`
- `GET /api/predictions/{id}`
- `POST /api/predictions/train`

### Prediccion financiera

- `POST /api/projects/{projectId}/financial-predict`
- `GET /api/financial-predictions`

### EVM

- `POST /api/evm/calculate`
- `GET /api/evm/recent`
- `GET /api/evm/{projectId}/history`

## Requisitos

- .NET SDK 10
- Node.js y npm

## Ejecucion

### Backend

Desde la raiz del repo:

```powershell
dotnet restore backend\PriceVision.Api\PriceVision.Api.csproj
dotnet run --project backend\PriceVision.Api\PriceVision.Api.csproj --launch-profile http
```

URL esperada:

- `http://localhost:5054`

Comprobacion rapida:

```powershell
Invoke-RestMethod -Method Get -Uri "http://localhost:5054/api/health"
```

### Frontend

```powershell
cd frontend\pricevision-ui
npm install
npm start
```

URL esperada:

- `http://localhost:4200`

El frontend usa proxy hacia el backend.

## Flujo recomendado de prueba

### 1. Registrar proyecto

Desde la interfaz:

- ir a `Registrar proyecto`
- completar los datos obligatorios
- guardar

Ejemplo API:

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5054/api/projects" `
  -ContentType "application/json" `
  -Body '{
    "name": "Centro Logistico Norte",
    "areaM2": 1250,
    "location": "Bogota",
    "type": "Comercial",
    "durationMonths": 12,
    "baseCostCop": 980000000
  }'
```

### 2. Ejecutar prediccion de recursos

Entrenamiento inicial del modelo, si hace falta:

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5054/api/predictions/train" `
  -ContentType "application/json" `
  -Body '{"rows":3000}'
```

Prediccion por proyecto:

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5054/api/projects/{PROJECT_ID}/predict" `
  -ContentType "application/json" `
  -Body '{
    "predictMaterials": true,
    "predictLabor": true
  }'
```

### 3. Generar prediccion financiera

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5054/api/projects/{PROJECT_ID}/financial-predict"
```

### 4. Calcular EVM

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5054/api/evm/calculate" `
  -ContentType "application/json" `
  -Body '{
    "projectId": "{PROJECT_ID}"
  }'
```

## Reglas funcionales importantes

- un proyecto puede registrarse sin ejecutar predicciones inmediatamente
- las predicciones de materiales y mano de obra se aplican por separado o juntas
- solo se permite ejecutar el modelo que falte en un proyecto
- la prediccion financiera queda asociada al proyecto
- EVM solo puede ejecutarse una vez por proyecto
- el historial muestra eventos de prediccion, prediccion financiera y EVM

## Validaciones historicas

Al registrar un proyecto, el backend puede mostrar advertencias no bloqueantes si detecta incoherencias frente a historicos guardados.

Ejemplos:

- costo base por m2 extremadamente bajo o alto
- duracion inconsistente con el tipo de proyecto

Estas advertencias:

- no bloquean el registro
- se calculan en backend
- se muestran visualmente en Angular

## Notas tecnicas

- los modelos de materiales y mano de obra usan dataset sintetico en esta etapa
- la prediccion financiera actual es un servicio de calculo, no un modelo ML independiente
- los artefactos del modelo se guardan en `backend/PriceVision.Api/Artifacts`
- si cambias endpoints o esquema, reinicia el backend para que la app refleje los cambios
