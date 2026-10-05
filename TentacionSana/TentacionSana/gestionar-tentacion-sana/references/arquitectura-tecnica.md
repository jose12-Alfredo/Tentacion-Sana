# Arquitectura técnica obligatoria

## Índice

1. Stack tecnológico
2. Estilo de arquitectura
3. Organización de la solución
4. Blazor y diseño responsive
5. Persistencia y transacciones
6. Seguridad
7. Pruebas
8. Infraestructura y almacenamiento
9. Restricciones

## 1. Stack tecnológico

Usar:

- C# y la versión LTS vigente de .NET;
- ASP.NET Core como plataforma del servidor;
- Blazor Web App para toda la interfaz;
- componentes Razor (`.razor`) y CSS para presentación;
- Entity Framework Core para persistencia;
- proveedor Npgsql para PostgreSQL;
- PostgreSQL alojado en Neon como base de datos principal;
- Cloudinary para imágenes y evidencias;
- Render para desplegar la aplicación;
- ASP.NET Core Identity para autenticación y gestión inicial de usuarios;
- xUnit para pruebas automatizadas.

No introducir React, Next.js, Angular, Vue ni un frontend Node.js separado. Blazor utiliza HTML y CSS porque se ejecuta en el navegador; esto no contradice el requisito de usar C#. Utilizar interoperabilidad con JavaScript solo cuando sea técnicamente necesaria y encapsularla.

## 2. Estilo de arquitectura

Construir un monolito modular: una sola aplicación desplegable con límites claros entre módulos. No crear microservicios para el alcance actual.

Separar como mínimo:

- dominio: entidades, valores, reglas e invariantes;
- aplicación: casos de uso, validaciones, permisos y contratos;
- infraestructura: Entity Framework Core, PostgreSQL, archivos y servicios externos;
- web: componentes Blazor, autenticación, navegación y presentación;
- pruebas: reglas del dominio, casos de uso e integración.

Evitar capas vacías o abstracciones sin uso. Priorizar claridad, transacciones consistentes y facilidad de mantenimiento.

## 3. Organización de la solución

Usar una solución inicial equivalente a:

```text
TentacionSana.sln
src/
  TentacionSana.Domain/
  TentacionSana.Application/
  TentacionSana.Infrastructure/
  TentacionSana.Web/
tests/
  TentacionSana.UnitTests/
  TentacionSana.IntegrationTests/
```

Organizar cada proyecto por módulos del negocio, por ejemplo: Pedidos, Clientes, Entregas, Pagos, Rendiciones, Inventario, Producción, Recetas, Costos y Usuarios.

## 4. Blazor y diseño responsive

Usar Blazor Web App. Renderizar la portada, catálogo y fichas públicas del lado del servidor para una carga inicial clara y contenido rastreable; habilitar interactividad solamente en formularios, solicitudes y componentes que la necesiten. Usar interactividad de servidor para el sistema interno como configuración inicial. No prometer funcionamiento sin conexión; si se vuelve requisito, evaluarlo expresamente antes de cambiar a un enfoque WebAssembly/PWA.

- Diseñar componentes reutilizables para formularios, estados, importes y confirmaciones.
- Aplicar CSS Grid, Flexbox y utilidades responsive; puede usarse Bootstrap incluido con Blazor si se mantiene una identidad visual consistente.
- No duplicar páginas separadas para celular y escritorio.
- Separar rutas públicas como `/`, `/catalogo`, `/productos/{slug}`, `/contacto` y `/solicitar` de las rutas internas protegidas bajo `/app`.
- Diseñar la parte pública con identidad visual coherente, fotografías protagonistas, jerarquía clara, confianza de marca y llamadas visibles a solicitar o contactar por WhatsApp.
- Adaptar tablas extensas mediante tarjetas, columnas prioritarias o desplazamiento interno.
- Mantener acciones esenciales disponibles en todos los tamaños definidos por `RN-UX-*`.
- Considerar pérdida temporal de conexión al confirmar entregas y pagos; mostrar estado de envío y evitar reintentos duplicados mediante claves de idempotencia.

## 5. Persistencia y transacciones

- Modelar importes con `decimal`, nunca `double` o `float`.
- Guardar fechas de auditoría en UTC y convertirlas a la zona horaria configurada al mostrarlas.
- Usar migraciones de Entity Framework Core versionadas.
- Aplicar índices y restricciones únicas también en PostgreSQL, no solo en la interfaz.
- Ejecutar en una misma transacción las operaciones inseparables, por ejemplo confirmar entrega, descontar inventario, registrar venta, aplicar pago y crear rendición.
- Implementar concurrencia optimista en pedidos, lotes, stock y pagos para prevenir sobrescrituras silenciosas.
- Conservar registros históricos mediante estados, vigencias y movimientos reversos; evitar borrado físico de información utilizada.

## 6. Seguridad

- Usar ASP.NET Core Identity con cookies seguras para la aplicación interna.
- Configurar la aplicación para exigir autenticación por defecto en `/app` y operaciones internas. Permitir acceso anónimo únicamente a portada, catálogo, fichas públicas, contacto, solicitud, inicio de sesión, recuperación y recursos públicos estrictamente necesarios.
- No ofrecer registro público. Crear cuentas desde el módulo administrativo.
- Usar nombre de usuario único para el acceso; permitir correo verificado como dato opcional de recuperación.
- Entregar una contraseña temporal al crear una cuenta y exigir su cambio en el primer acceso.
- Configurar cookies con `Secure`, `HttpOnly`, protección `SameSite` y expiración controlada.
- Configurar bloqueo inicial después de cinco intentos fallidos por quince minutos, manteniendo estos valores en configuración.
- Revocar sesiones cuando se deshabilite una cuenta, cambie una contraseña sensible o un administrador fuerce el cierre.
- Aplicar limitación de solicitudes a los endpoints de inicio de sesión y recuperación.
- Aplicar autorización mediante roles y políticas tanto en componentes Blazor como en casos de uso del servidor.
- No confiar solamente en ocultar botones.
- Validar archivos de evidencia por tamaño, tipo permitido y nombre seguro.
- Mantener secretos y cadenas de conexión fuera del código fuente mediante configuración segura.
- Registrar auditoría para cambios sensibles sin guardar contraseñas, tokens ni secretos.

## 7. Pruebas

- Crear pruebas unitarias para descuentos, saldos, reservas, costos, recetas vigentes y transiciones de estado.
- Crear pruebas de integración con PostgreSQL para transacciones, restricciones, concurrencia y migraciones.
- Probar autorización de cada rol desde el servidor.
- Probar idempotencia de entrega, pago y rendición.
- Validar manual o automáticamente las pantallas en 360, 768, 1024 y 1440 píxeles.

## 8. Infraestructura y almacenamiento

### Render

- Desplegar `TentacionSana.Web` en Render mediante un `Dockerfile` reproducible.
- Escuchar en `0.0.0.0` y en el puerto recibido mediante la variable `PORT`.
- Usar inicialmente el subdominio gratuito `onrender.com`; no exigir dominio propio para el MVP.
- Exponer un endpoint de salud para verificar aplicación, base de datos y configuración esencial.
- Tratar el sistema de archivos local como temporal. No guardar allí imágenes, comprobantes, evidencias ni copias de seguridad.
- Mantener cadenas de conexión, claves de Cloudinary y demás secretos como variables de entorno de Render.
- Verificar las condiciones y límites vigentes del plan gratuito antes de cada publicación; no codificar supuestos permanentes sobre cuotas o tiempos de suspensión.

### Neon

- Crear en Neon la base de datos PostgreSQL de los ambientes autorizados.
- Configurar Npgsql con la cadena de conexión segura proporcionada por Neon y exigir SSL.
- Guardar la conexión en `ConnectionStrings__DefaultConnection` o equivalente dentro de las variables protegidas de Render.
- Aplicar migraciones de Entity Framework Core mediante un proceso controlado de despliegue; no ejecutar cambios destructivos automáticamente sin revisión.
- Preparar exportaciones periódicas de la información crítica incluso mientras se utilice el plan gratuito.
- No usar la base PostgreSQL gratuita temporal de Render como base principal.

### Cloudinary

- Integrar Cloudinary desde el servidor ASP.NET Core mediante su SDK para .NET o su API firmada.
- Subir imágenes de productos y catálogo como recursos públicos dentro de carpetas lógicas como `tentacion-sana/productos/{productoId}`.
- Subir evidencias de entrega y comprobantes como recursos `authenticated`, accesibles únicamente mediante URLs firmadas y temporales.
- Usar rutas lógicas como `tentacion-sana/entregas/{entregaId}` y `tentacion-sana/comprobantes/{pagoId}`.
- Guardar en PostgreSQL como mínimo: identificador interno, entidad relacionada, `public_id`, tipo de recurso, tipo de acceso, formato, tamaño, fecha, usuario y estado.
- No guardar permanentemente una URL firmada: generarla cuando un usuario autorizado solicite ver el archivo.
- Para el MVP, enviar el archivo al servidor ASP.NET Core y realizar desde allí la carga firmada a Cloudinary. Evaluar carga directa firmada solo si el volumen o el rendimiento lo justifican.
- Generar URLs firmadas de evidencias con una duración corta y configurable; usar cinco minutos como valor inicial.
- Validar extensión, tipo MIME, contenido, tamaño y dimensiones antes de subir. Aceptar inicialmente JPG, PNG y WebP con un límite configurable.
- Comprimir imágenes, limitar dimensiones y eliminar metadatos EXIF sensibles cuando corresponda.
- Si una operación se revierte, mantener consistencia entre Cloudinary y PostgreSQL mediante estados y reintentos; no dejar referencias activas a archivos eliminados.
- Eliminar un recurso de Cloudinary solo mediante un caso de uso autorizado y registrar auditoría.

### Separación obligatoria

```text
Render       → aplicación C# / Blazor
Neon         → datos PostgreSQL
Cloudinary   → imágenes y evidencias
PostgreSQL   → identificadores y metadatos de Cloudinary
```

## 9. Restricciones

- No cambiar el stack sin una decisión explícita del propietario del sistema.
- No dividir en microservicios por anticipación.
- No agregar repositorios genéricos encima de Entity Framework Core si no aportan una necesidad concreta.
- No colocar reglas de negocio dentro de componentes visuales; los componentes invocan casos de uso.
- No exponer entidades de persistencia directamente como contratos externos cuando se cree una API.
- No guardar imágenes como `bytea`, Base64 o blobs dentro de PostgreSQL salvo una excepción futura expresamente aprobada.
- No guardar archivos de usuario en `wwwroot/uploads` ni en otra carpeta local de Render.
- No exponer públicamente fotografías de entregas, comprobantes o documentos internos.
- No cambiar Neon, Cloudinary o Render sin una decisión explícita del propietario del sistema.
