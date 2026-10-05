# Plan maestro de implementación — Tentación Sana

## Propósito del documento

Este documento conserva el plan acordado para construir Tentación Sana por etapas. Debe actualizarse cuando cambie el alcance o se complete una etapa, sin borrar decisiones anteriores relevantes. No autoriza implementar todo de una vez: cada etapa debe diseñarse, implementarse, probarse y revisarse antes de avanzar.

## Estado inicial revisado

- Existe una solución `TentacionSana.slnx` con un único proyecto Blazor Web App.
- El proyecto apunta a `.NET 10`, versión LTS.
- La aplicación conserva las páginas y estilos de la plantilla (`Home`, `Counter` y `Weather`).
- Todavía no existen proyectos separados de dominio, aplicación, infraestructura o pruebas.
- Todavía no están incorporados Entity Framework Core, Npgsql, PostgreSQL, ASP.NET Core Identity ni Cloudinary.
- No existen migraciones, modelo de datos, autenticación, autorización, módulos administrativos ni configuración de Render.
- El logotipo oficial está disponible en la skill local y debe conservarse sin redibujarlo ni deformarlo.
- La carpeta examinada no es reconocida actualmente por `git status` como repositorio. Esto debe resolverse o documentarse antes de cambios estructurales.

## Alcance técnico obligatorio

- C# y .NET 10 LTS.
- ASP.NET Core y Blazor Web App.
- Componentes Razor; JavaScript solo mediante interoperabilidad cuando sea necesario.
- Entity Framework Core con Npgsql.
- PostgreSQL alojado en Neon.
- Imágenes públicas y privadas en Cloudinary.
- Despliegue mediante Docker en Render.
- ASP.NET Core Identity para usuarios internos.
- xUnit para pruebas.
- Monolito modular: una sola aplicación desplegable, organizada por módulos.

## Estructura objetivo

```text
TentacionSana.slnx
src/
  TentacionSana.Domain/
  TentacionSana.Application/
  TentacionSana.Infrastructure/
  TentacionSana.Web/
tests/
  TentacionSana.UnitTests/
  TentacionSana.IntegrationTests/
docs/
  plan-implementacion.md
```

### Responsabilidades

- `Domain`: entidades, objetos de valor, estados, transiciones e invariantes.
- `Application`: casos de uso, comandos, consultas, DTO, validaciones, permisos y límites transaccionales.
- `Infrastructure`: EF Core, PostgreSQL, Identity, Cloudinary y servicios externos.
- `Web`: componentes Razor, rutas, autenticación, layouts y presentación.
- `UnitTests`: reglas del dominio y casos de uso aislados.
- `IntegrationTests`: PostgreSQL, migraciones, transacciones, concurrencia, idempotencia y autorización.

No se agregarán microservicios ni repositorios genéricos sin una necesidad concreta.

## Áreas y módulos

### Sitio público

- Portada profesional.
- Catálogo, categorías y búsqueda.
- Ficha pública con URL legible por producto.
- Solicitud de productos.
- Contacto y enlace a WhatsApp con mensaje preparado.
- Contenido público, testimonios autorizados y políticas.
- Metadatos SEO y medición de conversión.

Abrir WhatsApp no crea una venta, reserva ni pedido. Una solicitud pública es un prospecto hasta que Ventas la revise y convierta.

### Sistema interno protegido

- Dashboard.
- Solicitudes públicas.
- Clientes, empresas, sucursales y responsables de pago.
- Productos, publicación, precios y descuentos.
- Pedidos.
- Producción y lotes.
- Inventario terminado.
- Entregas.
- Pagos y cuentas por cobrar.
- Rendiciones.
- Ingredientes, compras y recetas.
- Costos, gastos y rentabilidad.
- Reportes.
- Usuarios, roles, permisos y auditoría.
- Configuración.

## Decisión funcional: los pedidos se pueden modificar

Los pedidos serán editables. La edición cambia según su avance para mantener cifras correctas y conservar el historial.

### Pedido en borrador

- Ventas puede modificar cliente, sucursal, pagador, dirección, contacto, fecha prometida, productos, cantidades, precios y observaciones.
- Agregar o quitar líneas no afecta inventario.
- Se recalculan subtotales, descuentos y total.
- Se registra autor y fecha de la última modificación.

### Pedido confirmado y aún no entregado

- Un usuario autorizado puede modificarlo.
- Toda modificación exige un motivo.
- Se guarda valor anterior, valor nuevo, usuario y fecha/hora.
- Al cambiar cantidades, el sistema ajusta las reservas por la diferencia dentro de la misma transacción.
- Si aumenta una cantidad y no hay stock disponible, se muestra el faltante para producir; no se crea stock negativo silencioso.
- Si disminuye una cantidad o se elimina una línea, se libera la reserva correspondiente.
- Cambiar cliente, sucursal, dirección, contacto o pagador crea una nueva instantánea para el pedido y conserva la anterior en el historial.
- Cambiar la fecha prometida crea un registro específico con valor anterior, nuevo valor y motivo.
- Cambiar el precio conserva precio estándar, precio anterior, precio nuevo, descuento y autorización requerida.

### Pedido en preparación, listo o en reparto

- Sigue siendo modificable con permisos reforzados.
- El sistema advierte el impacto sobre preparación, reservas y entrega asignada.
- No se puede reducir una cantidad por debajo de lo ya preparado, asignado o entregado sin resolver primero esa diferencia mediante un caso de uso autorizado.
- Si la modificación invalida la entrega preparada, se debe actualizar o reprogramar la entrega y registrar el motivo.

### Pedido parcialmente entregado

- Solo se modifican cantidades pendientes y datos futuros.
- Las cantidades, precios, costos, receptor, pago e inventario de la parte ya entregada no se sobrescriben.
- Un aumento genera nueva cantidad pendiente y nueva reserva.
- Una reducción no puede ser inferior a lo ya entregado.
- Las correcciones de una entrega existente se realizan mediante reverso o ajuste trazable.

### Pedido totalmente entregado, cancelado o con movimientos financieros

- No se reescribe el historial de la operación completada.
- Un administrador puede corregir mediante devolución, nota de crédito, pago reverso, reposición o movimiento de inventario, según corresponda.
- Cancelar exige motivo y libera solamente reservas no consumidas.
- Los pagos existentes no se eliminan; se mantienen o se corrigen mediante reversos.

### Reglas técnicas para la edición

- Usar concurrencia optimista para detectar que otra persona modificó el pedido.
- Enviar una versión o token de concurrencia al guardar.
- Ejecutar cambios del pedido, reservas, totales e historial en una sola transacción.
- Recalcular desde datos confiables del servidor; no aceptar totales calculados por el navegador.
- Comprobar permisos en el caso de uso del servidor, además de la interfaz.
- Hacer idempotentes las confirmaciones móviles y operaciones que puedan repetirse por pérdida de conexión.
- Nunca borrar físicamente un pedido confirmado ni su historial.

### Casos de uso previstos

- `CrearPedidoBorrador`
- `ModificarPedidoBorrador`
- `ConfirmarPedido`
- `ModificarPedidoConfirmado`
- `CambiarFechaPrometida`
- `CambiarPrecioDeLinea`
- `AgregarLineaAPedido`
- `CambiarCantidadDeLinea`
- `QuitarLineaDePedido`
- `CancelarPedido`
- `ConsultarHistorialDePedido`

Cada caso de uso definirá permiso, validaciones, impacto en reservas, auditoría y resultado de concurrencia.

## Modelo de datos inicial

| Módulo | Tablas principales |
|---|---|
| Seguridad | `AspNetUsers`, `AspNetRoles`, tablas de Identity, `UserSessions`, `SecurityEvents` |
| Auditoría | `AuditEntries`, `IdempotencyRecords` |
| Clientes | `Customers`, `CustomerContacts`, `DeliveryPoints`, `PaymentResponsibleParties` |
| Catálogo | `Products`, `ProductCategories`, `ProductPublications`, `ProductImages`, `ProductPrices` |
| Solicitudes | `ProductRequests`, `ProductRequestLines`, `RequestStatusHistory` |
| Pedidos | `Orders`, `OrderLines`, `OrderStatusHistory`, `OrderChangeHistory`, `PromisedDateHistory`, `OrderSnapshots` |
| Inventario | `ProductionLots`, `StockMovements`, `StockReservations`, `LotAllocations`, `InventoryCounts` |
| Entregas | `Deliveries`, `DeliveryLines`, `DeliveryAssignments`, `DeliveryAttempts`, `DeliveryEvidence` |
| Finanzas | `Payments`, `PaymentAllocations`, `PaymentReversals`, `Receivables`, `Settlements`, `SettlementLines` |
| Recetas | `Ingredients`, `PurchasePresentations`, `IngredientPurchases`, `Recipes`, `RecipeVersions`, `RecipeItems` |
| Costos | `LotCostSnapshots`, `IngredientCostSnapshots`, `Expenses`, `ExpenseCategories` |
| Archivos | `MediaAssets` con identificadores y metadatos de Cloudinary |
| Configuración | `BusinessSettings`, `PaymentMethods`, `DiscountReasons`, `MovementReasons` |

### Decisiones de persistencia

- Importes en `decimal`, con precisión explícita en PostgreSQL.
- Auditoría y eventos almacenados en UTC.
- Cliente, punto de entrega, receptor y pagador son entidades o conceptos diferentes.
- Pedido, entrega, pago y rendición tienen estados independientes.
- Al confirmar se congelan datos comerciales y precios mediante instantáneas históricas.
- El stock se respalda con movimientos y reservas trazables.
- FIFO es el método inicial de asignación de lotes.
- PostgreSQL guarda metadatos de Cloudinary, no archivos binarios ni URLs firmadas permanentes.
- Productos públicos usan recursos públicos; evidencias y comprobantes usan acceso autenticado y URLs temporales.

## Plan por etapas

### Etapa 0 — Protección del punto de partida

- [x] Documentar la situación de Git: no existe `.git` en la raíz inspeccionada.
- [x] Registrar un inventario del proyecto original.
- [x] Crear la estructura de solución por capas sin perder el proyecto existente.
- [x] Añadir proyectos de pruebas.
- [x] Fijar la familia de SDK .NET 10 mediante `global.json`.
- [x] Verificar compilación después de reorganizar.

El proyecto original `TentacionSana/TentacionSana.csproj` permanece en la solución durante la transición. La estructura nueva se creó en `src/` y `tests/` para evitar sobrescribirlo antes de validar el reemplazo.

**Verificación realizada:** la solución completa compila con .NET SDK 10.0.301, cero advertencias y cero errores. La restauración inicial necesitó acceso a NuGet para las dependencias de xUnit.

**Criterio de salida:** solución reorganizada y compilable, sin pérdida de archivos.

### Etapa 1 — Persistencia, seguridad y base transversal

- [x] Incorporar EF Core y Npgsql.
- [x] Configurar ASP.NET Core Identity.
- [x] Crear roles Administrador, Ventas, Producción, Repartidor y Finanzas.
- [x] Proteger `/app` y eliminar el registro público.
- [ ] Implementar contraseña temporal, cambio obligatorio, bloqueo y recuperación. La recuperación permanece pendiente.
- [ ] Configurar cookies seguras y revocación de sesiones. Las cookies están configuradas; falta la revocación administrativa inmediata.
- [x] Crear auditoría, idempotencia y configuración base.
- [x] Crear las migraciones iniciales de PostgreSQL.
- [x] Probar la configuración de Identity y autorización desde el servidor.

**Estado:** en progreso. La base técnica está implementada, las migraciones fueron aplicadas en Neon y el primer administrador completó el acceso y cambio obligatorio de contraseña. Permanecen pendientes la recuperación y la administración de sesiones.

**Criterio de salida:** login funcional y sistema interno protegido.

### Etapa 2 — Base visual y catálogo público

- [x] Definir tokens visuales y componentes base.
- [x] Incorporar el logotipo oficial sin modificar el archivo maestro.
- [x] Separar `PublicLayout` y `AdminLayout`, con navegación propia para el sistema interno.
- [x] Crear portada, catálogo, ficha, contacto, solicitud por WhatsApp y login.
- [x] Crear productos, categorías, precios vigentes y publicación.
- [x] Integrar imágenes públicas de Cloudinary. Credenciales, carga real y publicación en el catálogo verificadas.
- [x] Implementar WhatsApp con número configurable. Número comercial y enlaces general/específico verificados.
- [x] Añadir metadatos SEO y Open Graph en la ficha de producto.
- [ ] Validar 360, 768, 1024 y 1440 píxeles. Por decisión del propietario, la revisión estética detallada se realizará después de completar los flujos funcionales principales.

**Criterio de salida:** catálogo profesional, responsive y administrable.

**Estado:** funcionalmente aceptado para continuar. El panel, Neon, Cloudinary, catálogo y WhatsApp funcionan. La segunda dirección visual queda como base y su revisión estética detallada se aplaza hasta completar los módulos funcionales principales.

**Mejora visual al 2026-09-28:** `/app/productos` fue rediseñada como panel administrativo responsive con búsqueda,
filtros reales, indicadores dinámicos, imágenes de Cloudinary, marcador cuando falta imagen y filas convertibles en
tarjetas móviles. Los estados Activo y Publicado se administran de forma independiente mediante interruptores que
persisten en el servidor y generan auditoría. Compilación Release sin advertencias y 47 pruebas unitarias aprobadas.
Continúa pendiente la revisión visual conjunta de todas las pantallas en 360, 768, 1024 y 1440 píxeles.

### Etapa 3 — Solicitudes, clientes y conversión

- [x] Crear formulario público con consentimiento, validación, rate limiting e idempotencia.
- [x] Crear bandeja administrativa de solicitudes con ubicación, enlaces HTTP/HTTPS seguros, errores, estados e historial.
- [x] Permitir solicitudes manuales con origen WhatsApp.
- [x] Implementar personas, empresas, sucursales, contactos y pagadores.
- [x] Convertir una solicitud una sola vez a cliente y pedido borrador.
- [x] Mantener vínculo solicitud–cliente–pedido.

**Criterio de salida:** captación pública conectada con Ventas sin generar ventas o reservas automáticas.

**Orden interno acordado:** primero persistencia y bandeja de solicitudes; después clientes y direcciones; finalmente conversión idempotente a pedido borrador.

**Avance verificado al 2026-09-28:** ubicación y enlaces seguros completados en la bandeja. Las URLs con otros esquemas y los valores relativos se muestran como texto y no se convierten en enlaces. Compilación Web sin advertencias y 18 pruebas unitarias aprobadas.

**Avance adicional al 2026-09-28:** el formulario público usa POST SSR con antiforgery y una política real de cinco envíos por IP cada diez minutos; las visitas GET no consumen cupo. Se creó y aplicó en Neon `AddRequestStatusHistory`, con backfill para solicitudes existentes. La bandeja permite registrar Contactada/Cerrada con motivo, consultar el historial y crear solicitudes de origen WhatsApp. Quedan pendientes las validaciones y el endurecimiento concurrente antes de marcar completo el formulario y la conversión.

**Validación y concurrencia al 2026-09-28:** los teléfonos nuevos se normalizan a formato internacional canónico, los correos y longitudes se validan en servidor y la cantidad queda limitada a 1–1000. Una solicitud web solo acepta productos activos y publicados. La conversión falla si no existe precio vigente positivo y, ante la restricción única por carrera concurrente, recupera y devuelve el pedido creado por la transacción ganadora.

**Cierre verificado al 2026-09-28:** fase 3 completada. Se aplicaron en Neon `AddRequestStatusHistory` y `CompleteCustomerCommercialStructure`. El comando `--check-phase3` comprobó envío duplicado, transición de estado, dos conversiones simultáneas, precio congelado, vínculos solicitud–cliente–pedido, empresa, contacto, sucursal, responsable de pago y auditoría; eliminó los datos ficticios al finalizar. Resultado final: solución sin advertencias, 28 pruebas unitarias y 5 pruebas de integración aprobadas.

### Etapa 4 — Pedidos editables, precios y reservas

- [x] Implementar creación y edición de borradores.
- [x] Generar números únicos.
- [x] Implementar precios con vigencias.
- [x] Congelar precio estándar, vendido y descuento por línea.
- [x] Exigir motivo y autorización de descuentos.
- [x] Guardar instantáneas de cliente, dirección, contacto y pagador.
- [x] Separar estados operativo y de pago.
- [x] Implementar transiciones e historial.
- [x] Confirmar pedidos y reservar inventario.
- [x] Implementar todas las reglas de modificación descritas en este documento.
- [x] Mostrar faltantes de producción.
- [x] Liberar reservas al reducir o cancelar.
- [x] Probar concurrencia y cambios simultáneos.

**Criterio de salida:** pedidos editables con precios, reservas e historial consistentes.

**Cierre verificado al 2026-09-28:** fase 4 completada. Los pedidos confirmados exigen motivo para cualquier modificación, ajustan reservas en la misma transacción, conservan instantáneas e historiales y requieren autorización administrativa desde preparación. Un pedido entregado o cancelado no puede reescribirse. `--check-phase4` ejecutó en Neon dos escrituras simultáneas con contextos separados y comprobó un único ganador, reducción de cantidad, ajuste de reserva, cancelación y limpieza. Resultado final: 34 pruebas unitarias y 5 pruebas de integración aprobadas, sin errores ni advertencias de compilación.

### Etapa 5 — Producción e inventario terminado

- [x] Registrar lotes, unidades buenas y merma.
- [x] Crear movimientos de producción y ajustes.
- [x] Mostrar stock físico, reservado y disponible.
- [x] Implementar FIFO y asignaciones de lote.
- [x] Registrar salidas no comerciales con motivo y costo.
- [x] Congelar costo estimado del lote.
- [x] Implementar conteos y ajustes autorizados.
- [x] Probar concurrencia en stock y reservas.

**Criterio de salida:** inventario terminado rastreable y protegido contra stock negativo silencioso.

**Cierre verificado al 2026-09-28:** fase 5 completada. Neon contiene las migraciones `AddFinishedGoodsInventory` y `ConnectRealStockReservations`. El inventario usa saldos con token de concurrencia, movimientos inmutables, lotes versionados, asignación FIFO, costos estándar estimados congelados y reservas reales. `--check-phase5` verificó con un producto temporal aislado la producción, merma, movimiento de entrada, salida FIFO, costo histórico, conteo, ajuste y limpieza. Resultado: 38 pruebas unitarias y 5 pruebas de integración aprobadas; solución sin advertencias ni errores.

**Ajuste operativo 2026-09-28:** Producción registra solo producto y cantidad; el sistema asigna el lote. No se requiere receta ni costo para sumar stock. Reparto registra las salidas no comerciales y confirma entregas; las entregas consumen reservas y lotes FIFO. Cuando no hay costo vigente, el costo del lote y el costo de venta quedan desconocidos. Migraciones nuevas preparadas; pendientes de aplicar a la base de datos.

### Etapa 6 — Entregas, pagos y cuentas por cobrar

- [x] Asignar y reasignar repartidores.
- [x] Crear bandeja móvil “Mis entregas”.
- [x] Confirmar entregas totales, parciales y fallidas.
- [x] Subir evidencia privada a Cloudinary.
- [x] Confirmar entrega, lote, inventario, reserva, venta, saldo, pago y rendición en una sola transacción.
- [x] Implementar pagos completos, parciales, anticipados y posteriores.
- [x] Construir cuentas por cobrar agrupadas por pagador.
- [x] Probar idempotencia de entrega y pago.

**Criterio de salida:** flujo completo desde pedido hasta entrega y cobro.

**Cierre verificado al 2026-09-28:** fase 6 completada. La operación permite programar contra reservas disponibles, asignar y reasignar solo usuarios habilitados como Repartidor, iniciar ruta, confirmar cantidades totales o parciales, registrar intentos fallidos, receptor, cobro y evidencia privada `authenticated` en Cloudinary. Las evidencias se consultan mediante enlaces firmados que vencen en cinco minutos. La confirmación actualiza FIFO, inventario, reserva, venta, cuenta por cobrar, pago y obligación de rendición en una transacción; los anticipos, pagos parciales y pagos posteriores usan concurrencia e idempotencia sin permitir sobrepagos accidentales. `CompleteDeliveryOperations` fue aplicada en Neon y `--check-phase6` verificó anticipo, dos confirmaciones simultáneas, reintentos de pago, saldo, movimiento y limpieza. Resultado: 47 pruebas unitarias, 5 pruebas de integración y compilación sin advertencias.

### Etapa 7 — Rendiciones y dashboard MVP

- [ ] Mostrar dinero recibido por cada colaborador.
- [ ] Registrar rendiciones totales y parciales.
- [ ] Mantener diferencias abiertas con motivo.
- [ ] Mostrar pedidos, retrasos, faltantes, ventas, cobros, deuda y rendiciones.
- [ ] Permitir rastrear cada cifra hasta su operación.
- [ ] Probar el recorrido solicitud → pedido → reserva → producción → entrega → pago → rendición.

**Criterio de salida:** MVP operativo del negocio.

### Etapa 8 — Ingredientes, recetas, compras y costos

- [ ] Crear ingredientes, unidades base y presentaciones.
- [ ] Implementar conversiones y compras.
- [ ] Calcular costo promedio ponderado.
- [ ] Crear recetas versionadas, validadas y con vigencias no solapadas.
- [ ] Seleccionar automáticamente la receta por producto y fecha.
- [ ] Registrar consumo teórico y variaciones reales.
- [ ] Congelar costos históricos por lote.
- [ ] Registrar gastos operativos.
- [ ] Calcular rentabilidad histórica.

**Criterio de salida:** costos y rentabilidad sin recalcular operaciones pasadas.

### Etapa 9 — Reportes, operación y despliegue

- [ ] Crear reportes de descuentos, deuda, inventario, pérdidas, puntualidad y rentabilidad.
- [ ] Crear endpoint de salud.
- [ ] Configurar logs estructurados sin datos sensibles.
- [ ] Crear `Dockerfile` reproducible para Render.
- [ ] Escuchar en `0.0.0.0` y el puerto de `PORT`.
- [ ] Configurar variables protegidas para Neon y Cloudinary.
- [ ] Definir un proceso controlado de migraciones.
- [ ] Preparar exportaciones periódicas.
- [ ] Ejecutar pruebas funcionales, responsive, seguridad y recuperación.
- [ ] Desplegar primero a pruebas y luego a producción.

**Criterio de salida:** sistema desplegable, observable y recuperable.

## Orden de entrega acordado

1. Estructura, seguridad y persistencia.
2. Catálogo, WhatsApp y solicitudes.
3. Clientes, productos y pedidos editables.
4. Producción e inventario terminado.
5. Entregas, pagos, cuentas por cobrar y rendiciones.
6. Dashboard MVP.
7. Ingredientes, recetas, costos avanzados y rentabilidad.
8. Reportes, endurecimiento y despliegue.

## Reglas de trabajo para no perder ni sobrescribir avances

- Revisar el estado del proyecto y los archivos afectados antes de cada etapa.
- No eliminar ni reemplazar archivos existentes sin comprobar su función y sus cambios.
- Realizar cambios pequeños y verificables.
- Mantener este documento actualizado con casillas y decisiones nuevas.
- Crear pruebas para reglas financieras, históricas, de inventario, autorización e idempotencia.
- No declarar terminada una función si solo cambia la pantalla: revisar permisos, auditoría, inventario, cuentas por cobrar, rendiciones y reportes afectados.
- No almacenar secretos en el repositorio.
- No aplicar migraciones destructivas automáticamente.
- No publicar ni desplegar una etapa sin revisión del resultado correspondiente.

## Decisiones que se solicitarán cuando sean necesarias

- Número comercial y texto inicial de WhatsApp.
- Productos, categorías, presentaciones y precios públicos iniciales.
- Contenido autorizado: beneficios, alérgenos, información nutricional y testimonios.
- Correos y datos de las primeras cuentas internas.
- Umbral de descuento que exige autorización.
- Reglas de evidencia obligatoria por cliente o tipo de entrega.
- Límites de imágenes y vigencia de URLs firmadas.
- Ambientes de Neon, Cloudinary y Render que se habilitarán.
