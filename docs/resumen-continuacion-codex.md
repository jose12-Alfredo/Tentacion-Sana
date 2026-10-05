# Traspaso para continuar Tentación Sana

Actualizado: 2026-09-28.

## Objetivo y reglas acordadas

Construir Tentación Sana como monolito modular con .NET 10 LTS, ASP.NET Core Blazor Web App, EF Core, PostgreSQL en Neon, Cloudinary y despliegue futuro en Render. El propietario pidió priorizar funcionalidad; el pulido visual responsive queda para después de los flujos principales. Cada bloque terminado debe documentarse en `docs/registro-trabajo.md` y actualizar `docs/plan-implementacion.md`. No eliminar ni sobrescribir el proyecto original sin revisarlo.

Los pedidos deben ser editables con reglas distintas según su estado, historial, motivo, concurrencia optimista y transacciones. Las reglas completas están en `docs/plan-implementacion.md`.

## Estructura actual

- `src/TentacionSana.Domain`: entidades y reglas.
- `src/TentacionSana.Application`: contratos, DTO y políticas.
- `src/TentacionSana.Infrastructure`: EF Core, Identity, Neon, Cloudinary y servicios.
- `src/TentacionSana.Web`: Blazor, rutas públicas y panel.
- `tests/TentacionSana.UnitTests`: 11 pruebas aprobadas.
- `tests/TentacionSana.IntegrationTests`: 3 pruebas aprobadas.
- `TentacionSana/`: proyecto original conservado como respaldo; no es la aplicación que debe ejecutarse.
- Aplicación correcta para Rider: `src/TentacionSana.Web/TentacionSana.Web.csproj`.

La carpeta no tiene repositorio Git inicializado según la revisión original.

## Configuración externa

- Neon está configurado mediante User Secrets y todas las migraciones actuales fueron aplicadas.
- Cloudinary está configurado mediante User Secrets y su conexión fue comprobada.
- WhatsApp comercial está configurado mediante `Business:WhatsAppNumber` en User Secrets.
- Existe un administrador real; completó el cambio obligatorio de contraseña y conserva el rol Administrador.
- No copiar secretos a archivos, respuestas o commits. La credencial original de Neon fue compartida en una conversación anterior; conviene rotarla antes de producción.

## Etapas completadas o funcionales

### Etapa 0

Solución organizada por capas, .NET 10 fijado, proyectos de pruebas y proyecto original preservado.

### Etapa 1: base funcional, cierre incompleto

Implementado: EF Core/Npgsql, Identity, roles Administrador/Ventas/Producción/Repartidor/Finanzas, cookies seguras, bloqueo, contraseña temporal, cambio obligatorio, `/app` protegido, auditoría, idempotencia, eventos de seguridad y primer administrador.

Pendiente: recuperación de contraseña, administración/revocación inmediata de sesiones y ampliación de auditoría automática.

### Etapa 2: catálogo funcional

Implementado: layouts público/administrativo, portada, catálogo, ficha por slug, login, productos, publicación, precios vigentes, edición administrativa, Cloudinary, imagen y metadatos en Neon, SEO/Open Graph, WhatsApp general y por producto.

Validación real: se creó y publicó “Budin Proteico de Chocolate”, se subió una imagen a Cloudinary y apareció en el catálogo; los enlaces de WhatsApp funcionaron.

Pendiente: revisión estética final a 360/768/1024/1440. El propietario decidió hacerla después de la funcionalidad.

## Etapa 3: cerrada el 2026-09-28

### Implementado

- `/solicitar`: nombre, WhatsApp, correo opcional, producto, cantidad, observaciones, consentimiento y ubicación opcional.
- La ubicación acepta dirección, referencia o enlace de mapas y se almacena en Neon.
- Idempotencia básica mediante `IdempotencyRecords` y una clave generada por el componente.
- `ProductRequests` y `ProductRequestLines`.
- `/app/solicitudes`, protegido por Ventas, con conversión interactiva.
- Estados se muestran en español: Nueva, Contactada, Convertida y Cerrada.
- Conversión transaccional básica solicitud → cliente → pedido borrador.
- Reutilización de cliente por coincidencia exacta de teléfono.
- Restricción única de pedido por `SourceRequestId`.
- Pedido borrador con líneas y precio vigente congelado; no reserva ni descuenta inventario.
- `/app/clientes` y `/app/pedidos` como consultas protegidas.
- Tablas `Customers`, `CustomerContacts`, `DeliveryPoints`, `Orders` y `OrderLines`.

### Migraciones aplicadas en Neon

- `InitialIdentityAndFoundation`
- `AddSecurityEvents`
- `AddCatalogProducts`
- `AddProductRequests`
- `AddCustomersAndDeliveryPoints`
- `AddDraftOrdersAndRequestConversion`
- `AddOptionalRequestLocation`

### Cierre realizado

- Ubicación visible y enlaces HTTP/HTTPS seguros en la bandeja.
- Rate limiting real mediante POST SSR protegido por antiforgery: cinco envíos por IP cada diez minutos.
- Historial persistente Nueva–Contactada–Convertida/Cerrada con usuario, fecha y motivo.
- Solicitudes administrativas con origen WhatsApp.
- Personas, empresas, contactos, sucursales, puntos y responsables de pago administrables.
- Teléfonos normalizados, correo/longitudes/cantidades validados y producto publicado obligatorio.
- Conversión concurrente e idempotente con precio vigente positivo y vínculo solicitud–cliente–pedido.
- Auditoría de solicitudes y estructura comercial.
- Migraciones `AddRequestStatusHistory` y `CompleteCustomerCommercialStructure` aplicadas en Neon.
- Recorrido integral real `--check-phase3` aprobado, incluidos dos intentos simultáneos de conversión y limpieza de datos ficticios.
- Verificación final: 28 pruebas unitarias, 5 de integración y compilación completa sin advertencias.

## Riesgos técnicos identificados

- `ProductRequestService` y varias páginas están escritos de forma muy compacta; conviene reformatearlos antes de ampliarlos.
- La idempotencia pública depende de una clave mantenida por el componente Blazor; falta una estrategia robusta para reconexión/reintento y rate limiting por IP.
- La conversión busca clientes por teléfono exacto sin normalizar el número.
- Un producto activo pero no publicado puede aceptarse en una solicitud si se conoce su ID.
- Si no existe precio vigente, la conversión congela `0`; debería fallar o exigir decisión explícita.
- `CustomerContact` y `DeliveryPoint` están modelados, pero no tienen casos de uso públicos para crearlos o editarlos.
- La ubicación ya está en formulario, dominio, DTO y base, pero todavía no aparece en la bandeja administrativa.
- El plan contiene notas antiguas que dicen que faltan Cloudinary/WhatsApp, aunque ambos ya están configurados y probados. Actualizar esas frases al cerrar fase 3.
- No hay historial persistente de estados de solicitud ni pedido.
- No hay despliegue Render, Dockerfile ni health check todavía.

## Verificación al momento del traspaso

- `TentacionSana.Web`: compilación aprobada, 0 advertencias, 0 errores.
- UnitTests: 11 aprobadas, 0 fallidas.
- IntegrationTests: 3 aprobadas, 0 fallidas.
- No se volvió a ejecutar una prueba integral real después de añadir `Location`; la migración sí fue aplicada en Neon.

## Próximo bloque recomendado

Comenzar etapa 4: edición completa de pedidos borrador, numeración, snapshots, precios/descuentos, concurrencia e historial. El pedido borrador mínimo actual solo es el resultado controlado de la conversión y no representa la etapa 4 terminada.

## Documentos de referencia

- `docs/plan-implementacion.md`: alcance y reglas de negocio.
- `docs/registro-trabajo.md`: cronología detallada.
- `docs/configuracion-secretos.md`: nombres de secretos y comandos sin valores.
- `docs/direccion-visual.md`: decisiones visuales.
- `gestionar-tentacion-sana/SKILL.md`: skill local original y sus referencias.
