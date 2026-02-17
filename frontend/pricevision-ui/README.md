# PriceVision

Este repositorio contiene una arquitectura separada en **Frontend (Angular)** y **Backend (ASP.NET Core)**.
El objetivo actual es tener una base limpia para consumir APIs desde la UI con una estructura mantenible.

## Estructura actual del proyecto

```text
PriceVision/
|-- backend/
|   |-- PriceVision.Api/
|   |-- PriceVision.Application/
|   |-- PriceVision.Domain/
|   |-- PriceVision.Infrastructure/
|   `-- PriceVision.slnx
|-- frontend/
|   `-- pricevision-ui/
`-- .gitignore
```

## Backend (`backend/`)

La solucion esta organizada en capas para facilitar escalabilidad y separacion de responsabilidades.

### `PriceVision.Api/`
Contiene la API HTTP que expone endpoints para el frontend.

Archivos esenciales:
- `backend/PriceVision.Api/Program.cs`: Configuracion principal del backend (CORS, OpenAPI, endpoints `/api/health` y `/api/weatherforecast`).
- `backend/PriceVision.Api/Properties/launchSettings.json`: Puertos y perfiles de ejecucion local (ejemplo: `http://localhost:5054`).
- `backend/PriceVision.Api/appsettings.json`: Configuracion base de la aplicacion.
- `backend/PriceVision.Api/appsettings.Development.json`: Overrides para entorno de desarrollo.

### `PriceVision.Application/`
Capa de casos de uso y logica de aplicacion.

Que contendra aqui:
- Servicios de aplicacion.
- Casos de uso (commands/queries).
- Contratos entre API e infraestructura.

### `PriceVision.Domain/`
Capa de dominio puro.

Que contendra aqui:
- Entidades de negocio.
- Value objects.
- Reglas de negocio independientes de frameworks.

### `PriceVision.Infrastructure/`
Implementaciones tecnicas.

Que contendra aqui:
- Persistencia (EF Core, repositorios).
- Integraciones externas (APIs de terceros, mensajeria).
- Implementaciones concretas de interfaces del dominio/aplicacion.

## Frontend (`frontend/pricevision-ui/`)

Aplicacion Angular 17 con SSR habilitado, conectada al backend mediante proxy en desarrollo.

Archivos esenciales:
- `frontend/pricevision-ui/angular.json`: Configuracion global de build/serve/test.
- `frontend/pricevision-ui/package.json`: Scripts y dependencias del frontend.
- `frontend/pricevision-ui/proxy.conf.json`: Proxy de `/api` hacia backend (`http://localhost:5054`).
- `frontend/pricevision-ui/src/main.ts`: Punto de entrada cliente.
- `frontend/pricevision-ui/src/main.server.ts`: Punto de entrada para SSR.
- `frontend/pricevision-ui/src/environments/environment.ts`: Variables de entorno (incluye `apiBaseUrl: '/api'`).

### Estructura de `src/app`

```text
src/app/
|-- core/
|   `-- services/
|       `-- api.service.ts
|-- app.component.ts
|-- app.component.html
|-- app.component.scss
|-- app.component.spec.ts
|-- app.config.ts
|-- app.config.server.ts
`-- app.routes.ts
```

Que va en cada archivo clave:
- `src/app/core/services/api.service.ts`: Servicio HTTP central para consumir endpoints del backend.
- `src/app/app.config.ts`: Proveedores globales de Angular (`Router`, `HttpClient`, hidratacion).
- `src/app/app.config.server.ts`: Configuracion especifica del render del lado servidor.
- `src/app/app.component.ts`: Componente raiz; carga datos de la API al iniciar.
- `src/app/app.component.html`: Vista principal con tabla de datos recibidos.
- `src/app/app.component.scss`: Estilos del componente raiz.
- `src/app/app.routes.ts`: Definicion de rutas de la app (actualmente base).

## Flujo de conexion Frontend-Backend

1. El frontend realiza llamadas a rutas relativas, por ejemplo: `/api/weatherforecast`.
2. En desarrollo, `proxy.conf.json` redirige esas llamadas a `http://localhost:5054`.
3. El backend responde desde endpoints definidos en `Program.cs`.
4. CORS esta habilitado para `http://localhost:4200`.

## Como ejecutar el proyecto

### Prerequisitos

- .NET SDK 10 instalado.
- Node.js 18+ y npm instalados.

### Primera instalacion

Desde la raiz del repositorio:

```bash
cd frontend/pricevision-ui
npm install
cd ../..
```

### Ejecucion en desarrollo

1. Levantar backend (Terminal 1):

```bash
dotnet run --project backend/PriceVision.Api
```

2. Levantar frontend (Terminal 2):

```bash
cd frontend/pricevision-ui
npm start
```

### URLs de verificacion

- Frontend: `http://localhost:4200`
- Backend health: `http://localhost:5054/api/health`
- Backend weather: `http://localhost:5054/api/weatherforecast`

Si ambos procesos estan activos, el frontend debe mostrar los datos del endpoint `weatherforecast`.

## Scripts utiles del frontend

- `npm start`: Levanta Angular en modo desarrollo con proxy.
- `npm run build`: Genera build de produccion (cliente + servidor SSR).
- `npm test`: Ejecuta pruebas unitarias con Karma.

## Estado actual

Implementado:
- Separacion `frontend`/`backend`.
- Endpoint de prueba de salud y endpoint de ejemplo (`weatherforecast`).
- Consumo real del backend desde Angular.
- Base para crecer por capas en backend y por modulos/servicios en frontend.

Siguiente evolucion recomendada:
- Crear modulos funcionales por dominio en frontend.
- Agregar controladores/casos de uso reales en backend.
- Integrar persistencia y autenticacion.
