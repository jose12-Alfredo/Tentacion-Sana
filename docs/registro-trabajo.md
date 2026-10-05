# Registro de trabajo

Este archivo se actualiza al terminar cada bloque de implementación.

## 2026-09-28 — Registro simple de producción y salidas a cargo de reparto

### Realizado

- `/app/inventario` pide únicamente producto y cantidad producida; crea un lote con número automático y aumenta el stock físico.
- Receta y costo estimado se adjuntan al lote solo si hay datos vigentes. Sin ellos se permite producir y el costo queda desconocido.
- Producción ya no registra mermas ni salidas. El repartidor registra las salidas no comerciales desde `/app/mis-entregas`, con tipo, cantidad y motivo.
- Al confirmar una entrega, el sistema consume la reserva y descuenta los lotes FIFO; el registro sigue limitado al repartidor asignado o administrador.
- Se agregaron migraciones para permitir lotes sin receta y costos desconocidos. No se aplicaron a la base de datos.

### Verificación

- Compilación Web Release: 0 errores y 0 advertencias.

## 2026-09-28 — Navegación administrativa completa

### Corregido

- El menú interno ya muestra Productos, Solicitudes, Clientes, Pedidos, Entregas, Producción e inventario, Mis entregas y Cuentas por cobrar según los roles autorizados.
- El rol Administrador aparece expresamente en todos los grupos de navegación y conserva acceso por las políticas del servidor a Ventas, Producción, Reparto y Finanzas.
- El resumen dejó de mostrar el marcador antiguo de fase 3 y ahora presenta tarjetas para todos los módulos implementados.
- La barra lateral admite desplazamiento vertical cuando la altura disponible no alcanza, sin ocultar el cierre de sesión.

### Verificación

- Compilación Web Release: 0 errores y 0 advertencias.
- `--check-admin` confirmó en Neon que la cuenta configurada está habilitada, no tiene cambio de contraseña pendiente y conserva el rol Administrador.

### Continúa

- Reiniciar la instancia abierta en Rider y volver a iniciar sesión para cargar los componentes y las reivindicaciones de rol actuales.

## 2026-09-28 — Fase 6 cerrada: entregas, pagos y cuentas por cobrar

### Completado

- La programación acepta exclusivamente pedidos listos o en reparto y limita cada línea a las reservas todavía disponibles para programar.
- La asignación y reasignación valida usuarios habilitados con rol Repartidor y conserva cada cambio en el historial de la entrega.
- La bandeja administrativa permite programar y asignar. “Mis entregas” muestra dirección, ubicación segura, contacto, productos y acciones breves para celular.
- El repartidor asignado puede iniciar ruta, registrar una entrega total o parcial, indicar receptor, cobro completo/parcial o intento fallido con motivo.
- La confirmación consume la reserva y el stock, asigna lotes FIFO, congela el costo, crea venta y cuenta por cobrar y registra pago/rendición dentro de una sola transacción.
- Finanzas puede registrar anticipos explícitos como crédito a favor y pagos posteriores completos o parciales. La cuenta por cobrar se presenta por pagador, cliente, pedido, saldo y antigüedad.
- Las confirmaciones y pagos usan claves de idempotencia, restricciones únicas y tokens de concurrencia; un reintento devuelve el resultado existente.
- Las evidencias se cargan como recursos `authenticated` en `tentacion-sana/entregas/{entregaId}`. Neon conserva metadatos y la consulta genera un enlace firmado con vencimiento de cinco minutos.
- Se creó y aplicó en Neon la migración `CompleteDeliveryOperations`, que añade `DeliveryEvidence` y sus índices.

### Verificación

- Compilación Release completa: 0 errores y 0 advertencias.
- Pruebas unitarias: 47 aprobadas. Pruebas de integración: 5 aprobadas.
- `--check-phase6` creó datos aislados en Neon y comprobó anticipo, programación, inicio de ruta, dos confirmaciones concurrentes con una sola venta y salida FIFO, pago parcial recibido por el repartidor, obligación de rendición, pago posterior idempotente, saldo cero y limpieza final.

### Continúa

- Fase 7: rendiciones parciales y totales, diferencias, trazabilidad a caja y dashboard MVP.

## 2026-09-28 — Fase 6: núcleo transaccional de entrega y cobro

### Realizado

- Se modelaron entregas, líneas parciales, receptor, estados propios e historial independiente del pedido.
- La confirmación idempotente consume reservas y stock físico, asigna lotes FIFO, congela costo histórico, crea venta y cuenta por cobrar dentro de una transacción.
- Un pago opcional valida el saldo y, cuando lo recibe el repartidor, crea una obligación de rendición por el mismo importe.
- Se añadieron bandejas protegidas de entregas, cuentas por cobrar y “Mis entregas” filtrada por el usuario autenticado.
- Se aplicó en Neon `AddDeliveriesPaymentsReceivables`.

### Verificación

- La solución compiló con 0 errores y 0 advertencias después del bloque.
- Las reglas unitarias cubren entrega parcial, receptor obligatorio, pago positivo y rechazo de sobrepago. Resultado acumulado: 42 unitarias y 5 de integración aprobadas.

### Continúa

- Añadir formularios operativos para programar, iniciar, completar y fallar entregas.
- Implementar evidencias privadas en Cloudinary y comprobantes.
- Ejecutar pruebas transaccionales, concurrentes e idempotentes en Neon y cerrar la fase 6.

## 2026-09-28 — Fase 5 cerrada

### Realizado

- Se modelaron lotes, saldos por producto, movimientos inmutables, asignaciones FIFO, conteos, versiones mínimas de receta y costos estándar estimados vigentes.
- Registrar producción congela receta y costo estimado, conserva unidades buenas y merma, crea el lote y aumenta el stock físico.
- Las salidas no comerciales exigen motivo, validan disponibilidad, consumen lotes FIFO y conservan el costo histórico de cada asignación.
- Los pedidos reservan solamente el disponible real; la reserva guarda cantidad comprometida y faltante. Reducir, retirar o cancelar libera el saldo en la misma transacción.
- Los saldos y lotes usan concurrencia optimista y bloquean stock negativo o ajustes por debajo de lo reservado.
- Se creó `/app/inventario` para producción, stock físico/reservado/disponible, faltantes, lotes y salidas no comerciales.
- Se aplicaron en Neon `AddFinishedGoodsInventory` y `ConnectRealStockReservations`.

### Verificación

- 38 pruebas unitarias y 5 pruebas de integración aprobadas.
- Compilación completa con 0 errores y 0 advertencias.
- `--check-phase5` creó un producto temporal aislado, receta, costo, lote, movimiento, salida FIFO y conteo; comprobó los saldos y eliminó exclusivamente el agregado temporal.
- El revisor automático rechazó correctamente una primera versión del diagnóstico cuya limpieza podía afectar un saldo real. Se reemplazó por el producto aislado antes de ejecutar Neon.

### Continúa

- Comenzar la fase 6: entregas, evidencia privada, ventas, pagos y cuentas por cobrar.
- Consumir las reservas y lotes asignados al confirmar entregas totales o parciales.
- Completar ingredientes y recetas detalladas en la fase 8 sin recalcular los costos históricos ya congelados.

## 2026-09-28 — Fase 4 cerrada

### Realizado

- Los pedidos confirmados se pueden modificar con motivo obligatorio; cada cambio conserva valor anterior, valor nuevo, usuario y fecha.
- Los cambios comerciales generan una nueva instantánea sin sobrescribir las anteriores y los cambios de fecha prometida mantienen su historial específico.
- La reducción o aumento de cantidades ajusta la reserva y el faltante de producción dentro de la misma transacción; una línea confirmada no puede reducirse a cero de forma destructiva.
- Retirar una línea confirmada la marca inactiva, libera su reserva y conserva tanto la línea como el historial. La migración `PreserveRemovedOrderLines` quedó aplicada en Neon.
- Se implementaron las transiciones Confirmado → En preparación → Listo → En reparto → Entregado con historial y motivo.
- Desde preparación, las modificaciones y avances posteriores requieren autorización administrativa. Los pedidos entregados o cancelados no admiten reescritura.
- La pantalla del pedido permite modificar datos futuros, cantidades y precios confirmados, visualizar su impacto y avanzar el estado.
- El diagnóstico de Neon usa dos servicios y dos contextos separados para competir sobre la misma versión; solo una escritura puede ganar.

### Verificación

- Se añadieron pruebas de ajuste de reserva con pedido confirmado y bloqueo de reescritura después de la entrega.
- Pruebas unitarias: 34 aprobadas, 0 fallidas.
- Pruebas de integración: 5 aprobadas, 0 fallidas.
- Solución completa: 0 advertencias y 0 errores.
- El recorrido Neon verificó precio congelado, conflicto por versión, dos escrituras simultáneas, modificación confirmada, ajuste de reserva, historial, cancelación y limpieza.

### Continúa

- Comenzar la fase 5 con lotes de producción, movimientos de inventario terminado, consumos, mermas y cálculo real de stock físico, reservado y disponible.
- Sustituir el faltante total provisional por disponibilidad real de lotes conforme se implemente inventario.
- Mantener pendiente para el cierre visual la inspección de 360, 768, 1024 y 1440 píxeles.

## 2026-09-28 — Fase 4: núcleo de pedidos, precios y reservas

### Realizado

- Se implementó la creación manual y edición de pedidos borrador con cliente, sucursal, contacto, responsable de pago, fecha prometida, notas y líneas.
- PostgreSQL asigna números únicos mediante `OrderNumberSequence`; cada escritura exige la versión esperada y devuelve un conflicto claro si el pedido cambió en otra sesión.
- Cada línea conserva precio estándar vigente, precio vendido, descuento y motivo. Los descuentos requieren motivo y una cuenta administradora.
- La confirmación transaccional guarda una instantánea del cliente, dirección, contacto y pagador, registra el cambio de estado y crea reservas con el faltante visible para producción.
- La cancelación exige motivo, conserva historial, libera reservas activas y genera auditoría.
- Se añadieron `/app/pedidos` y `/app/pedidos/{id}` para crear, editar, confirmar, cancelar y consultar historial y faltantes.
- Se creó y aplicó en Neon la migración `CompleteOrderManagement`, preservando como precio estándar y vendido el precio de las líneas anteriores.

### Verificación

- La solución compila con 0 advertencias y 0 errores.
- Pruebas unitarias: 31 aprobadas; pruebas de integración: 5 aprobadas.
- `--check-phase4` recorrió Neon con creación, edición, precio congelado, rechazo de versión obsoleta, confirmación, historial, reserva, cancelación, liberación y limpieza automática.
- El primer intento de migración detectó un escape SQL incorrecto y fue revertido íntegramente por PostgreSQL; el script corregido se aplicó después sin cambios parciales.

### Continúa

- Completar las reglas de modificación de pedidos ya confirmados: motivo obligatorio, ajuste de reservas por diferencia y restricciones según avance.
- Añadir la liberación parcial al reducir líneas y una prueba de dos escrituras realmente simultáneas con contextos separados.
- Cerrar la fase 4 y comenzar producción e inventario terminado de la fase 5.

## 2026-09-28 — Fase 3 cerrada

### Realizado

- Se completó la administración de personas y empresas con edición de datos comerciales.
- Se añadieron contactos con función, responsables de pago independientes y sucursales, franquicias u otros puntos de entrega.
- Cada punto puede vincular por separado a la persona receptora y al responsable de pago, además de dirección, referencia y ubicación.
- Todos los casos de uso comerciales generan auditoría y validan que las relaciones pertenezcan al mismo cliente.
- Se creó y aplicó en Neon `CompleteCustomerCommercialStructure`, preservando los clientes existentes como personas activas.
- El recorrido Neon descubrió y permitió corregir dos errores de seguimiento EF: historial nuevo tratado como actualización y nuevos hijos comerciales tratados como existentes.
- El diagnóstico `--check-phase3` ejecuta un recorrido real con datos ficticios y limpieza automática.

### Verificación

- Dos conversiones simultáneas con contextos separados devolvieron el mismo pedido; la restricción única actuó y la transacción perdedora recuperó el resultado persistido.
- Se verificaron en Neon el envío idempotente, historial Nueva–Contactada–Convertida, precio vigente congelado, cliente y pedido vinculados, empresa, contacto, sucursal, pagador y auditoría.
- Los datos ficticios del diagnóstico fueron eliminados al finalizar.
- Solución completa: 0 advertencias y 0 errores.
- Pruebas unitarias: 28 aprobadas, 0 fallidas.
- Pruebas de integración: 5 aprobadas, 0 fallidas.

### Continúa

- Comenzar la fase 4 con creación y edición completa de pedidos borrador, numeración única, instantáneas comerciales, precios/descuentos, concurrencia e historial.
- Mantener pendiente para el cierre visual la inspección de 360, 768, 1024 y 1440 píxeles acordada previamente.

## 2026-09-28 — Fase 3: normalización y conversión concurrente

### Realizado

- Los teléfonos de solicitudes, clientes y enlaces de WhatsApp usan una normalización internacional común; un número boliviano local de ocho dígitos recibe el prefijo `591`.
- El servidor valida formato y longitud de correo, límites de observaciones/ubicación y cantidad entre 1 y 1000.
- El formulario público rechaza productos ocultos aunque sigan activos.
- La conversión rechaza líneas sin precio vigente positivo para evitar pedidos con precio congelado en cero.
- Si dos conversiones compiten por la misma solicitud, la transacción que pierde la restricción única revierte sus cambios y devuelve el pedido ya creado.

### Verificación

- `TentacionSana.Web`: 0 advertencias y 0 errores.
- `TentacionSana.UnitTests`: 26 aprobadas, 0 fallidas; incluyen normalización boliviana e internacional y longitudes inválidas.
- `TentacionSana.IntegrationTests`: 3 aprobadas, 0 fallidas.
- Una primera ejecución paralela de compilación/pruebas chocó por el mismo archivo de salida; la suite de integración se repitió de forma aislada y aprobó.

### Continúa

- Completar el modelo y los casos de uso de empresas, sucursales, contactos, puntos de entrega y responsables de pago.
- Añadir pruebas de integración específicas para duplicación pública, carrera de conversión, precio congelado y relaciones completas.
- Ejecutar el recorrido funcional real en Neon con una sesión de Ventas y documentar los registros resultantes.

## 2026-09-28 — Fase 3: origen WhatsApp, historial y rate limiting

### Realizado

- El envío público dejó de depender de un evento dentro del circuito Blazor: ahora es un POST SSR con antiforgery e idempotencia conservada en el formulario.
- Se aplica una ventana fija de cinco envíos por IP cada diez minutos; los GET de `/solicitar` no consumen permisos.
- Se creó `RequestStatusHistory` con estado anterior/nuevo, usuario, fecha UTC y motivo.
- La bandeja permite marcar Contactada o Cerrada con motivo y consultar el historial persistente.
- La conversión registra el cambio a Convertida y su usuario; crear y cambiar solicitudes genera auditoría.
- Ventas puede crear manualmente solicitudes de origen WhatsApp y el origen se muestra en la bandeja.
- La migración `AddRequestStatusHistory` se aplicó en Neon y creó historial inicial para las solicitudes existentes.

### Verificación

- `TentacionSana.Web`: 0 advertencias y 0 errores.
- `TentacionSana.UnitTests`: 20 aprobadas, 0 fallidas.
- `TentacionSana.IntegrationTests`: 3 aprobadas, 0 fallidas.
- Neon confirmó creación de tabla, índice, backfill y registro de la migración.
- La ejecución HTTP confirmó que la ruta atraviesa `RateLimitingMiddleware`; la comprobación integral del formulario se hará en el recorrido real final.

### Continúa

- Normalizar teléfonos y reforzar longitudes, correo, cantidad máxima y producto publicado.
- Endurecer la conversión concurrente e idempotente y rechazar conversiones sin precio vigente.
- Completar empresas, sucursales, contactos, puntos de entrega y responsables de pago.
- Añadir pruebas de integración específicas y ejecutar el recorrido completo en Neon.

## 2026-09-28 — Fase 3: ubicación y errores en la bandeja

### Realizado

- La bandeja `/app/solicitudes` muestra la ubicación opcional de cada solicitud.
- Una ubicación que sea una URL absoluta HTTP o HTTPS se presenta como **Abrir mapa** en una pestaña nueva con `noopener noreferrer`.
- Direcciones escritas, URLs relativas y esquemas inseguros se conservan como texto y no se convierten en enlaces.
- La tabla usa desplazamiento interno controlado y permite que ubicaciones largas se ajusten sin ensanchar toda la página.
- La conversión deshabilita temporalmente su botón y muestra los errores devueltos por el caso de uso o un mensaje seguro ante una falla inesperada.

### Verificación

- `TentacionSana.Web`: compilación aprobada, 0 advertencias y 0 errores.
- `TentacionSana.UnitTests`: 18 aprobadas, 0 fallidas.
- Las pruebas nuevas cubren enlaces HTTP/HTTPS, direcciones escritas, URLs relativas y el esquema `javascript:`.

### Continúa

- Implementar rate limiting real para el envío público y comprobar que el límite se aplica a cada intento de creación, incluso dentro del circuito interactivo de Blazor.
- Después: historial persistente y acciones Contactada/Cerrada con usuario, fecha y motivo.

## 2026-09-28 — Auditoría y documento de continuidad

### Realizado

- Se revisaron el plan, el registro, la configuración, las migraciones y los módulos actuales.
- Se verificó el proyecto Web y ambas suites de pruebas.
- Se creó `docs/resumen-continuacion-codex.md` con estado real, decisiones, riesgos y próximo orden de trabajo.

### Verificación

- Web: 0 advertencias y 0 errores.
- Pruebas unitarias: 11 aprobadas.
- Pruebas de integración: 3 aprobadas.

### Continúa

- Cerrar los pendientes técnicos enumerados para la etapa 3 antes de comenzar la edición completa de pedidos.

## 2026-09-23 — Etapa 0: estructura inicial

### Realizado

- Se verificó que el proyecto original compilaba antes de reorganizar.
- Se crearon los proyectos Domain, Application, Infrastructure, Web, UnitTests e IntegrationTests.
- Se mantuvo el proyecto original en la solución como respaldo temporal.
- Se fijó .NET SDK 10 mediante `global.json`.
- Se activaron análisis y advertencias como errores.
- Se documentó que la raíz examinada no contiene `.git`.

### Verificación

- Solución completa compilada con cero advertencias y cero errores.

## 2026-09-23 — Etapa 1, bloque 1: persistencia y autenticación

### Realizado

- Se agregaron EF Core 10.0.12, ASP.NET Core Identity 10.0.12 y Npgsql para PostgreSQL.
- Se creó `ApplicationDbContext` con esquema `tentacion_sana`.
- Se modelaron usuarios internos con cuenta habilitada y cambio obligatorio de contraseña temporal.
- Se definieron los roles Administrador, Ventas, Producción, Repartidor y Finanzas.
- Se definieron políticas de autorización por función.
- Se configuró bloqueo después de cinco intentos durante quince minutos.
- Se configuró una cookie segura, `HttpOnly`, `SameSite=Lax` y duración controlada.
- Se añadieron auditoría, idempotencia y eventos de seguridad.
- Se protegió la ruta `/app` mediante política de acceso interno.
- Se implementaron endpoints de inicio de sesión, cierre y cambio de contraseña.
- Se añadió limitación de solicitudes al inicio de sesión.
- Se creó un proceso controlado para generar exclusivamente el primer administrador.
- Se generaron las migraciones `InitialIdentityAndFoundation` y `AddSecurityEvents`.
- Se habilitó Secret Manager en el proyecto Web.
- Se documentaron las variables de Neon, administrador inicial y Cloudinary.

### Verificación

- Compilación completa: cero advertencias y cero errores.
- Pruebas de integración: 3 aprobadas, 0 fallidas.
- Se verifican tablas base, restricción única de idempotencia y configuración de bloqueo/autorización.

### Pendiente de la etapa 1

- Base Neon configurada mediante User Secrets; migraciones aplicadas correctamente el 2026-09-23.
- Crear y probar el primer administrador contra PostgreSQL real.
- Implementar recuperación de contraseña por correo verificado.
- Implementar administración de cuentas y revocación inmediata de sesiones.
- Ampliar la auditoría automática de cambios de entidades.

### Incidencia de herramientas

La descarga de la herramienta local `dotnet-ef` 10.0.12 fue rechazada porque el revisor automático de permisos alcanzó su límite de uso. El manifiesto quedó declarado para restaurarlo posteriormente. Las migraciones fueron generadas con la herramienta global 10.0.9 y el runtime EF Core 10.0.12.

## 2026-09-23 — Neon conectado

### Realizado

- La cadena de PostgreSQL se guardó en User Secrets y no en el repositorio.
- Se corrigió el flujo de diseño de EF para que utilice la configuración del proyecto Web.
- Se aplicaron `InitialIdentityAndFoundation` y `AddSecurityEvents` en Neon.
- Se creó el esquema `tentacion_sana`, las tablas de Identity, auditoría, idempotencia y eventos de seguridad.
- Se insertaron los cinco roles iniciales.

### Verificación

- Neon aceptó ambas migraciones.
- EF Core registró ambas versiones en `__EFMigrationsHistory`.
- No se creó todavía ningún usuario administrador porque faltan los datos elegidos por el propietario.

## 2026-09-23 — Etapa 2, bloque 1: base visual pública

### Realizado

- Se definió y documentó una dirección visual basada en cacao, avena, verde hoja y un corte diagonal inspirado en el logotipo.
- Se copió el logotipo oficial al proyecto Web sin modificar el archivo maestro.
- Se creó un layout público independiente del panel administrativo.
- Se implementaron portada, catálogo vacío y contacto.
- Se añadieron navegación pública e inicio de sesión del personal.
- Se incorporaron estilos responsive, foco visible y reducción de movimiento.
- Se evitó publicar fotografías, testimonios o afirmaciones nutricionales inventadas.
- Se configuraron claves de protección de datos en una carpeta local excluida de Git para que autenticación y antiforgery funcionen en desarrollo restringido.
- Se configuró logging de consola compatible con desarrollo y Render.

### Verificación

- La aplicación respondió HTTP 200 en `/`, `/catalogo`, `/contacto` y `/login`.
- La portada renderizó propuesta de valor, logotipo y acción hacia el catálogo.
- `/app` redirige al acceso cuando no existe una sesión.
- Compilación: cero advertencias y cero errores.

### Pendiente del bloque visual

- Revisar visualmente en navegador a 360, 768, 1024 y 1440 píxeles; el navegador automatizado no estaba disponible en esta sesión.
- Sustituir el estado vacío por productos reales almacenados en PostgreSQL.
- Configurar el número oficial de WhatsApp.
- Integrar imágenes reales mediante Cloudinary.
- Definir almacenamiento persistente y protegido para las claves de Data Protection en producción.

## 2026-09-23 — Etapa 2, bloque 2: catálogo administrable y canales públicos

### Realizado

- Se modelaron productos, categorías, publicaciones, precios con vigencia e imágenes.
- Se creó la migración `AddCatalogProducts` y se aplicó correctamente en Neon.
- Se implementaron consultas públicas que solo muestran productos activos y publicados.
- Se creó la ficha pública con URL legible, precio vigente, galería y estado no encontrado.
- Se implementó el panel protegido para crear y editar productos, precio, visibilidad, destacado y descripción pública.
- Los cambios de producto y las cargas de imagen generan entradas de auditoría.
- Se integró CloudinaryDotNet 1.29.3 para cargas desde el servidor.
- La carga acepta JPG, PNG y WebP, exige texto alternativo y limita cada archivo a 10 MB.
- Se almacenan en PostgreSQL el identificador público, URL segura, formato, dimensiones, tamaño, orden y estado principal de cada imagen.
- Se implementó el enlace configurable de WhatsApp con mensaje preparado para producto, presentación y cantidad.
- Se añadieron descripción SEO y metadatos Open Graph a la ficha pública.

### Verificación

- Compilación completa: cero advertencias y cero errores.
- Pruebas unitarias: 9 aprobadas, 0 fallidas.
- Pruebas de integración: 3 aprobadas, 0 fallidas.
- La aplicación consultó Neon correctamente durante la prueba de ejecución.
- `/catalogo` y `/productos/no-existe` respondieron HTTP 200.
- `/app/productos` respondió HTTP 302 hacia el login sin sesión.

### Pendiente de la etapa 2

- Configurar `Cloudinary:CloudName`, `Cloudinary:ApiKey` y `Cloudinary:ApiSecret` mediante secretos y probar una carga real.
- Configurar `Business:WhatsAppNumber` con el número oficial.
- Cargar productos, textos e imágenes aprobados por el negocio.
- Completar el layout administrativo definitivo.
- Hacer la inspección visual manual en 360, 768, 1024 y 1440 píxeles.

## 2026-09-23 — Etapa 2, bloque 3: verificación de Cloudinary

### Realizado

- Se confirmaron `CloudName`, `ApiKey` y `ApiSecret` en User Secrets sin mostrar sus valores.
- Se añadió el comando seguro `--check-cloudinary` para comprobar configuración y conectividad sin subir archivos.
- Se documentó el comando para repetir el diagnóstico localmente o durante la preparación del despliegue.

### Verificación

- Cloudinary autenticó correctamente las credenciales mediante su operación de diagnóstico.
- El proyecto Web compiló con cero advertencias y cero errores.

### Siguiente paso

- Crear o seleccionar el primer producto y realizar una carga real desde `/app/productos` para validar el flujo completo Cloudinary → PostgreSQL → catálogo público.

## 2026-09-23 — Etapa 1, bloque 2: primer administrador

### Realizado

- Se configuró el correo y nombre visible del propietario mediante User Secrets.
- Se creó la primera cuenta interna en Neon.
- Se asignó el rol Administrador.
- La cuenta quedó habilitada y marcada para cambio obligatorio de contraseña en el primer acceso.
- Se registró el evento de seguridad correspondiente.
- Se eliminó la contraseña temporal de User Secrets después de crear la cuenta; en PostgreSQL solo permanece su hash administrado por Identity.

### Verificación

- Neon confirmó la inserción del usuario y la relación con el rol Administrador.
- El proceso protegido de creación inicial finalizó correctamente.

### Siguiente paso

- Iniciar sesión, cambiar la contraseña temporal y entrar a `/app/productos`.

## 2026-09-23 — Etapa 1, bloque 3: primer acceso validado

### Realizado

- El propietario inició sesión desde la aplicación Web con la cuenta administradora.
- El flujo de cambio obligatorio de contraseña se completó correctamente.
- La cuenta quedó lista para utilizar las funciones administrativas protegidas.
- Se consultó posteriormente el estado persistido en Neon para confirmar el cambio.

### Verificación

- Cuenta habilitada: sí.
- Cambio obligatorio pendiente: no.
- Rol Administrador: sí.

### Siguiente paso

- Entrar a `/app/productos`, registrar el primer producto y probar una imagen real en Cloudinary.

## 2026-09-23 — Etapa 2, bloque 4: navegación administrativa

### Realizado

- Se reemplazó la pantalla interna mínima por un dashboard administrativo.
- Se creó `AdminLayout` con el logotipo oficial, navegación y cierre de sesión.
- Se añadió acceso visible a Productos y al catálogo público.
- Se mostraron Solicitudes y Pedidos como módulos previstos para las siguientes fases.
- El diseño administrativo se adapta a escritorio y móvil.

### Verificación

- El proyecto Web compiló con cero advertencias y cero errores en una salida aislada, porque la instancia de Rider permanecía abierta.

### Uso

- Desde `/app`, seleccionar **Productos** o **Administrar productos** para abrir `/app/productos`.

## 2026-09-23 — Etapa 2, bloque 5: carga de imagen dentro del formulario

### Realizado

- Se añadió la selección de imagen al formulario de creación y edición de productos.
- El formulario acepta JPG, PNG y WebP y solicita texto alternativo accesible.
- Al guardar, primero se persiste el producto, luego se carga la imagen en Cloudinary y finalmente se almacenan su URL segura y metadatos en Neon.
- Si Cloudinary falla, el producto permanece guardado y la interfaz explica que solamente falló la imagen.
- Se conserva la carga individual en la lista para añadir imágenes posteriormente.

### Verificación

- El proyecto Web compiló con cero advertencias y cero errores.

### Corrección posterior

- Se retiró la validación `Required` residual del campo técnico `Slug`; ahora la dirección automática se genera antes de enviar el producto al servidor.

## 2026-09-23 — Etapa 2, bloque 7: primer producto real verificado

### Verificación en Neon y Cloudinary

- Productos registrados: 1.
- Producto verificado: `Budin Proteico de Chocolate`.
- Ruta generada: `/productos/budin-proteico-de-chocolate`.
- Estado del producto: activo.
- Estado de publicación: oculto.
- Registros de precio: 2, correspondientes al historial generado durante creación/edición.
- Imágenes asociadas: 1.
- La URL segura almacenada en Neon responde correctamente desde Cloudinary.

### Herramienta añadida

- Se añadió `--check-catalog` para comprobar productos, publicación, precios, imágenes y disponibilidad de Cloudinary sin mostrar secretos.

### Siguiente paso

- Editar el producto, marcar **Publicar inmediatamente** y guardar para mostrarlo en `/catalogo`.

### Validación del propietario

- El producto fue publicado y se confirmó visualmente que aparece en `/catalogo`.
- Quedó validado el recorrido producto → precio → imagen Cloudinary → catálogo público.

## 2026-09-23 — Etapa 2, bloque 8: WhatsApp comercial

### Realizado

- Se configuró el número comercial boliviano en User Secrets.
- El número no se guardó en archivos del repositorio.
- Las fichas de producto y la página de contacto pueden generar enlaces `wa.me` con el mensaje preparado.

### Verificación

- Las 9 pruebas unitarias aprobaron, incluidas las pruebas de normalización del número y contexto del producto.

### Uso

- Reiniciar la aplicación abierta en Rider para cargar el nuevo secreto.
- Abrir una ficha desde `/catalogo` y pulsar **Solicitar por WhatsApp**.

### Validación del propietario

- El enlace general abrió WhatsApp con el mensaje: `Hola Tentación Sana, quiero consultar sus productos.`
- Quedó validado el canal comercial y el número configurado.
- El mensaje específico con nombre y presentación se genera desde la ficha individual del producto.
- El propietario confirmó que el mensaje específico incluye correctamente el producto y la presentación.

### Revisión responsive

- Se intentó iniciar la inspección automatizada final, pero esta sesión no expuso ningún navegador controlable.
- Se conserva pendiente únicamente la comprobación visual en 360, 768, 1024 y 1440 píxeles; las reglas CSS responsive ya están implementadas.

## 2026-09-23 — Etapa 2, bloque 9: segunda dirección visual pública

### Motivo

- El propietario consideró que la primera versión era demasiado simple y que la experiencia responsive no estaba suficientemente resuelta.
- La etapa 2 se mantiene abierta hasta aprobar la nueva presentación.

### Realizado

- Se compactó el encabezado y se añadió una franja comercial de marca.
- La navegación móvil ahora usa una distribución horizontal clara en lugar de una columna irregular.
- La portada muestra productos reales del catálogo con sus imágenes, presentación y precio.
- Se añadieron profundidad, bordes redondeados, sombras moderadas y jerarquía tipográfica a las tarjetas.
- El catálogo incorporó acciones visibles para abrir cada producto.
- La ficha de producto ahora incluye ruta de navegación, imagen protagonista, bloque de precio, llamada de WhatsApp y nota de coordinación.
- Se reorganizó el pie de página y se mejoraron los cambios de composición para móvil.
- No se añadieron fotografías, beneficios o testimonios inventados.

### Verificación

- Proyecto Web: compilación con cero advertencias y cero errores.
- Pruebas unitarias: 9 aprobadas, 0 fallidas.

### Siguiente paso

- Reiniciar la aplicación, revisar `/`, `/catalogo` y la ficha del producto en escritorio y móvil, y recoger la opinión del propietario.

## 2026-09-23 — Decisión de prioridad: funcionalidad antes del acabado visual

### Decisión

- El propietario pidió continuar con la funcionalidad y realizar el pulido visual al final.
- La segunda versión responsive se conserva como base operativa.
- La inspección estética exhaustiva en cuatro anchos se trasladó al bloque final de revisión, sin eliminarla del alcance.

### Trabajo que continúa

- Fase 3, bloque 1: solicitudes públicas persistidas en Neon y bandeja protegida para Ventas.
- Fase 3, bloque 2: clientes, empresas, contactos y puntos de entrega.
- Fase 3, bloque 3: conversión idempotente de solicitud a cliente y pedido borrador.

## 2026-09-23 — Fase 3 autorizada: análisis del bloque 1

### Alcance confirmado

- Solicitud pública con datos de contacto, consentimiento, origen y observaciones.
- Líneas vinculadas a productos y cantidades solicitadas.
- Estados e historial independientes de pedidos y ventas.
- Protección por rate limiting e idempotencia.
- Bandeja protegida por la política de Ventas.
- Una solicitud no reservará inventario ni generará una venta automáticamente.

### Integración revisada

- El nuevo módulo se incorporará al `ApplicationDbContext` existente y al esquema `tentacion_sana`.
- Reutilizará productos publicados, auditoría, idempotencia, roles y políticas ya implementados.
- La navegación administrativa sustituirá el marcador “Solicitudes próximamente” por el enlace funcional cuando termine el bloque.

### Estado

- Fase 3 autorizada y bloque 1 en implementación.

### Avance técnico del bloque 1

- Se crearon `ProductRequest` y `ProductRequestLine` en el dominio.
- Se definieron los estados Nueva, Contactada, Convertida y Cerrada.
- Se exige nombre, teléfono, consentimiento y al menos una cantidad positiva por línea.
- Se preparó la persistencia EF Core para `ProductRequests` y `ProductRequestLines`.
- Las líneas congelan el nombre del producto para conservar el contexto histórico.
- Se añadieron pruebas de consentimiento y cantidades inválidas.

### Verificación parcial

- Pruebas unitarias: 11 aprobadas, 0 fallidas.

### Pendiente inmediato

- Crear servicio de aplicación, formulario público, bandeja administrativa, migración y aplicación en Neon.

### Avance técnico adicional

- Se implementó el contrato y servicio de registro de solicitudes.
- El servicio valida producto activo, consentimiento y cantidad.
- Se incorporó idempotencia por solicitud para evitar registros duplicados.
- Se creó la consulta administrativa ordenada por fecha.
- Se añadió `/app/solicitudes`, protegida por el permiso de Ventas.
- El menú administrativo ya incluye el módulo Solicitudes.

### Verificación

- El proyecto Web compila con cero advertencias y cero errores.

### Pendiente del bloque

- Formulario público, estilos, rate limiting, migración, aplicación en Neon y prueba completa de envío.

### Bloque de formulario y persistencia completado

- Se creó `/solicitar` con nombre, WhatsApp, correo, producto, cantidad, observaciones y consentimiento.
- El formulario utiliza productos publicados reales y muestra confirmación sin crear venta o reserva.
- Se añadió acceso desde la navegación pública.
- Se generó y aplicó en Neon la migración `AddProductRequests`.
- Neon contiene `ProductRequests`, `ProductRequestLines`, claves foráneas e índices por estado y fecha.
- La aplicación y el formulario compilan con cero advertencias y cero errores.

### Continúa

- Rate limiting del envío público, prueba real, estados e historial, clientes y conversión idempotente a pedido borrador.

### Bloque de clientes y direcciones

- Se modelaron clientes, contactos y puntos de entrega como conceptos separados.
- Se configuraron relaciones, longitudes e índice de búsqueda por teléfono.
- Se generó y aplicó en Neon la migración `AddCustomersAndDeliveryPoints`.
- Se crearon `Customers`, `CustomerContacts` y `DeliveryPoints` con sus claves foráneas e índices.
- La infraestructura compila con cero advertencias y cero errores.

### Continúa

- Casos de uso administrativos para clientes y conversión idempotente solicitud → cliente → pedido borrador.

### Base de conversión y pedido borrador

- Se modelaron pedidos borrador y líneas con nombre, cantidad y precio congelados.
- Cada pedido puede conservar la solicitud que lo originó.
- Se añadieron a la solicitud los vínculos al cliente y pedido resultantes.
- El índice único por solicitud impide crear dos pedidos desde la misma solicitud.
- Se generó y aplicó en Neon `AddDraftOrdersAndRequestConversion`.
- Se crearon `Orders` y `OrderLines`; esta base no reserva ni descuenta inventario.
- La infraestructura compila con cero advertencias y cero errores.

### Continúa

- Caso de uso transaccional de conversión, acción en la bandeja y pruebas de idempotencia.

### Conversión funcional implementada

- Se implementó la transacción solicitud → cliente → pedido borrador.
- Se reutiliza un cliente existente cuando coincide el teléfono; de lo contrario se crea uno.
- Se copian producto, cantidad y precio vigente a las líneas del pedido.
- Una solicitud ya convertida devuelve el pedido existente y no crea duplicados.
- Se añadió **Convertir a pedido** en `/app/solicitudes` y el estado visual “Pedido creado”.
- El proyecto Web compila con cero advertencias y cero errores.

### Continúa inmediatamente

- Vistas administrativas de clientes y pedidos, historial explícito de estados, rate limiting del formulario y pruebas automatizadas de conversión simultánea.

### Corrección de la bandeja de solicitudes

- El botón **Convertir a pedido** se mostraba, pero la página estaba renderizada de forma estática.
- Se habilitó `InteractiveServer` en `/app/solicitudes` para ejecutar el evento del botón.
- El proyecto Web compiló con cero advertencias y cero errores después de la corrección.

### Continúa

- Reiniciar la aplicación y repetir la conversión; después verificar cliente, pedido y líneas directamente en Neon.

### Conversión validada y estados localizados

- El propietario confirmó que la solicitud cambió a pedido creado.
- Se confirmó la ejecución funcional del botón y la conversión.
- Los estados técnicos `New`, `Contacted`, `Converted` y `Closed` ahora se presentan como Nueva, Contactada, Convertida y Cerrada.
- La corrección compila con cero advertencias y cero errores.

### Continúa

- Verificar los registros relacionados en Neon y crear las vistas administrativas de clientes y pedidos.

### Vistas administrativas de clientes y pedidos

- Se añadió `/app/clientes` con nombre, WhatsApp, correo y fecha de registro.
- Se añadió `/app/pedidos` con cliente, producto, cantidad, precio congelado y origen.
- Ambas páginas están protegidas por el permiso de Ventas.
- El menú administrativo ahora permite abrir Clientes y Pedidos.
- Los pedidos muestran explícitamente que siguen en borrador y no afectan inventario.
- El proyecto Web compila con cero advertencias y cero errores.

### Continúa

- Historial persistente de estados, rate limiting del formulario, pruebas de concurrencia/idempotencia y verificación integral en Neon.

### Uso

- Seleccionar una imagen en **Imagen del producto**, completar el texto alternativo y pulsar **Guardar producto**.
- Reiniciar la aplicación que está abierta en Rider para cargar esta actualización.

## 2026-09-23 — Etapa 2, bloque 6: dirección pública automática

### Realizado

- Se eliminó la obligación de escribir manualmente la URL legible al crear un producto.
- El sistema genera automáticamente el segmento de URL desde el nombre, en minúsculas, sin acentos y separado por guiones.
- La dirección solo se muestra durante la edición, para permitir una corrección administrativa excepcional.

### Ejemplo

- `Budín Proteico de Chocolate` genera `/productos/budin-proteico-de-chocolate`.

### Verificación

- El proyecto Web compiló con cero advertencias y cero errores.
## Rediseño funcional del administrador de productos — 2026-09-28

- Se rediseñó `/app/productos` siguiendo la referencia visual aprobada: encabezado, filtros, indicadores, filas con imagen y adaptación móvil.
- La búsqueda filtra nombre, presentación y descripción; los filtros de categoría, actividad y publicación usan datos reales.
- Los indicadores Total, Activos, Publicados y Ocultos se calculan con los productos registrados, sin porcentajes inventados.
- El resumen administrativo ahora incluye categoría e imagen principal de Cloudinary; cuando no hay imagen se muestra un marcador de marca.
- Actividad y publicación son interruptores independientes. Cada cambio persiste en PostgreSQL y genera auditoría (`ProductActivated`, `ProductDeactivated`, `ProductPublished` o `ProductHidden`).
- La carga de archivo quedó detrás de `Cambiar imagen`; crear, editar, cargar imagen y abrir la ficha pública conservan la lógica existente.
- En móvil se elimina la cabecera tabular y cada producto se reorganiza como tarjeta sin desplazamiento horizontal.

**Verificación:** `TentacionSana.Web.csproj` compiló en Release con cero errores y cero advertencias. Las 47 pruebas unitarias aprobaron. No se requirió migración.

**Ajuste posterior:** se retiraron el filtro y la columna Categoría porque el propietario confirmó que no utilizará esa clasificación en esta pantalla. El espacio se reasignó a la información y los estados del producto.
