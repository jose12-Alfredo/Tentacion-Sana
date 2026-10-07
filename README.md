# Tentacion Sana

Sistema web para administrar el catalogo, solicitudes, clientes, pedidos, inventario, entregas, cuentas por cobrar y caja de Tentacion Sana.

La aplicacion principal es un monolito modular construido con ASP.NET Core Blazor Web App, .NET 10, Entity Framework Core, PostgreSQL, Identity y Cloudinary.

## Requisitos

- SDK de .NET indicado en [`global.json`](global.json) (actualmente .NET 10).
- PostgreSQL local o una base en Neon.
- Una cuenta de Cloudinary para usar las funciones de imagenes y evidencias.

## Configuracion local

ASP.NET Core necesita una cadena de conexion para iniciar. Los secretos locales deben guardarse con Secret Manager, fuera del repositorio:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require" --project src/TentacionSana.Web
dotnet user-secrets set "Cloudinary:CloudName" "..." --project src/TentacionSana.Web
dotnet user-secrets set "Cloudinary:ApiKey" "..." --project src/TentacionSana.Web
dotnet user-secrets set "Cloudinary:ApiSecret" "..." --project src/TentacionSana.Web
dotnet user-secrets set "Business:WhatsAppNumber" "+591..." --project src/TentacionSana.Web
```

El archivo [`.env.example`](.env.example) enumera las variables equivalentes para GitHub Actions, contenedores y plataformas de despliegue. Si una herramienta requiere `.env`, se puede copiar el ejemplo y completar sus valores; todos los archivos `.env` reales estan excluidos de Git.

Hay mas detalles, incluido el alta controlada del primer administrador, en [`docs/configuracion-secretos.md`](docs/configuracion-secretos.md).

## Base de datos y ejecucion

Desde la raiz del repositorio:

```powershell
dotnet tool restore
dotnet restore TentacionSana.slnx
dotnet ef database update --project src/TentacionSana.Infrastructure --startup-project src/TentacionSana.Web
dotnet run --project src/TentacionSana.Web
```

La aplicacion quedara disponible en las direcciones indicadas por la consola. El perfil local usa `https://localhost:7143` y `http://localhost:5091`.

## Validacion

```powershell
dotnet build TentacionSana.slnx --configuration Release
dotnet test TentacionSana.slnx --configuration Release --no-build
```

Cada `push` o `pull request` hacia `main` ejecuta esas comprobaciones mediante GitHub Actions.

## Despliegue de prueba en Render

El repositorio incluye un `Dockerfile` para desplegar la aplicacion web en Render. Al crear un **Web Service**, selecciona la rama `main`, el entorno **Docker**, el `Dockerfile` de la raiz y el puerto `10000`. Configura `/health` como Health Check Path.

En el plan gratuito, Render suspende el servicio tras un periodo de inactividad y su almacenamiento es temporal. La aplicacion volvera a funcionar al iniciarse, pero todos los usuarios deberan iniciar sesion de nuevo despues de un reinicio. Para conservar las sesiones en un plan con disco persistente, monta un disco en `/var/data` y configura `DataProtection__KeysPath=/var/data/data-protection-keys`.

Al arrancar el servicio web se aplican las migraciones pendientes de Entity Framework antes de aceptar solicitudes. El usuario de PostgreSQL configurado en `ConnectionStrings__DefaultConnection` debe tener permisos para modificar el esquema. Si una migracion falla, revisa el error en los registros del servicio de Render; no habilites `Development` en produccion.

Agrega en Render estas variables protegidas sin versionarlas:

```text
ConnectionStrings__DefaultConnection
Cloudinary__CloudName
Cloudinary__ApiKey
Cloudinary__ApiSecret
Business__WhatsAppNumber
```

## Estructura

- `src/TentacionSana.Domain`: entidades y reglas del dominio.
- `src/TentacionSana.Application`: contratos y casos de uso.
- `src/TentacionSana.Infrastructure`: persistencia, Identity y servicios externos.
- `src/TentacionSana.Web`: interfaz Blazor y punto de entrada.
- `tests`: pruebas unitarias y de integracion del modelo.
- `docs`: decisiones, configuracion y seguimiento funcional.
- `TentacionSana`: proyecto Blazor original conservado como referencia.

## Antes de publicar

No se deben versionar `.env`, credenciales, cadenas de conexion, llaves de proteccion de datos, salidas de compilacion ni configuracion personal del IDE. El `.gitignore` cubre esos archivos. Revisa siempre el contenido preparado con `git status` antes del primer `push`.
