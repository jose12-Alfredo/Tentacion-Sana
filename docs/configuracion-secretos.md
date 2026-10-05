# Configuración local y secretos

Las credenciales no se guardan en `appsettings.json`, migraciones, documentos ni código fuente.

## PostgreSQL en Neon

La conexión será necesaria antes de:

1. aplicar migraciones a una base real;
2. crear el primer administrador;
3. ejecutar pruebas de integración contra PostgreSQL;
4. probar el inicio de sesión completo.

En desarrollo se configura mediante Secret Manager desde la raíz de la solución:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "CADENA_ENTREGADA_POR_NEON" --project src/TentacionSana.Web
```

La cadena de Neon debe exigir SSL. No debe pegarse en un archivo versionado.

En Render se usará esta variable protegida:

```text
ConnectionStrings__DefaultConnection
```

## Administrador inicial

Después de aplicar las migraciones y solo cuando todavía no exista ningún usuario:

```powershell
dotnet user-secrets set "BootstrapAdmin:UserName" "USUARIO_INICIAL" --project src/TentacionSana.Web
dotnet user-secrets set "BootstrapAdmin:DisplayName" "NOMBRE_VISIBLE" --project src/TentacionSana.Web
dotnet user-secrets set "BootstrapAdmin:TemporaryPassword" "CONTRASEÑA_TEMPORAL_SEGURA" --project src/TentacionSana.Web
dotnet run --project src/TentacionSana.Web -- --seed-admin
```

La contraseña es temporal y el usuario debe cambiarla al iniciar sesión. El comando rechaza la creación si ya existe cualquier usuario.

## Cloudinary

Las credenciales se necesitarán al implementar las imágenes del catálogo y las evidencias privadas. Se configurarán en desarrollo con:

```powershell
dotnet user-secrets set "Cloudinary:CloudName" "VALOR" --project src/TentacionSana.Web
dotnet user-secrets set "Cloudinary:ApiKey" "VALOR" --project src/TentacionSana.Web
dotnet user-secrets set "Cloudinary:ApiSecret" "VALOR" --project src/TentacionSana.Web
```

En Render se usarán variables protegidas:

```text
Cloudinary__CloudName
Cloudinary__ApiKey
Cloudinary__ApiSecret
```

La clave secreta nunca se enviará al navegador. Las cargas se firmarán desde ASP.NET Core.

Las evidencias de entrega usan las mismas credenciales, se cargan como recursos privados `authenticated` y solo se abren mediante enlaces firmados con vencimiento de cinco minutos. La URL temporal no se persiste en Neon.

La configuración puede comprobarse sin mostrar credenciales ni subir archivos:

```powershell
dotnet run --project src/TentacionSana.Web -- --check-cloudinary
```

El catálogo persistido y la accesibilidad de sus imágenes pueden comprobarse con:

```powershell
dotnet run --project src/TentacionSana.Web -- --check-catalog
```

## WhatsApp comercial

El número se guarda con código de país. Puede incluir `+`, espacios o guiones; la aplicación lo normaliza antes de construir el enlace.

```powershell
dotnet user-secrets set "Business:WhatsAppNumber" "+591 NUMERO" --project src/TentacionSana.Web
```

En Render se usará:

```text
Business__WhatsAppNumber
```

## Herramienta de Entity Framework Core

El repositorio declara `dotnet-ef` 10.0.12 como herramienta local. En una máquina con acceso a NuGet se restaura mediante:

```powershell
dotnet tool restore
```

Las migraciones se aplicarán mediante un paso controlado; la aplicación no ejecuta migraciones destructivas automáticamente al arrancar.
