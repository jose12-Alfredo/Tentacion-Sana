# Reglas de negocio

## Índice

1. Definiciones financieras
2. Pedidos y fechas
3. Clientes, sucursales y pagadores
4. Productos, precios y descuentos
5. Entregas
6. Pagos, cuentas por cobrar y rendiciones
7. Producción e inventario terminado
8. Ingredientes, compras y recetas
9. Costos y rentabilidad
10. Usuarios, auditoría y correcciones
11. Catálogo público y solicitudes
12. Diseño responsive y usabilidad

## 1. Definiciones financieras

- **Venta:** importe comercial de productos entregados, neto de descuentos. Un pedido confirmado aún no es una venta realizada.
- **Cobrado:** dinero cuya recepción fue registrada, aunque todavía esté en manos de un repartidor.
- **Por cobrar:** total exigible menos pagos aplicados y devoluciones válidas.
- **Compra:** adquisición de insumos o materiales; no equivale automáticamente a gasto ni a costo de venta.
- **Costo de venta:** costo histórico asignado a las unidades que salieron por una venta.
- **Ganancia bruta estimada:** ventas netas menos costo de ventas.
- **Ganancia neta estimada:** ganancia bruta menos gastos del periodo.
- **Rendición:** entrega a caja de dinero que un colaborador cobró al cliente.

## 2. Pedidos y fechas

- **RN-PED-001:** Todo pedido tendrá número único, autor, fecha y hora de creación.
- **RN-PED-002:** Guardar por separado fecha del pedido, fecha prometida y fecha/hora real de entrega.
- **RN-PED-003:** La fecha prometida es obligatoria al confirmar el pedido y puede informarse al cliente.
- **RN-PED-004:** Una modificación de fecha prometida debe conservar valor anterior, nuevo valor, responsable y motivo.
- **RN-PED-005:** Tiempo de entrega = fecha/hora real de entrega menos fecha/hora del pedido.
- **RN-PED-006:** Retraso = fecha/hora real menos fecha/hora prometida; si el resultado no es positivo, no existe retraso.
- **RN-PED-007:** Estados operativos mínimos: borrador, confirmado, en preparación, listo, en reparto, entregado y cancelado.
- **RN-PED-008:** El estado de pago no debe mezclarse con el operativo. Estados de pago: pendiente, parcial, pagado y reembolsado cuando aplique.
- **RN-PED-009:** Cancelar un pedido debe liberar reservas y exigir motivo. No borrar pedidos confirmados.
- **RN-PED-010:** Una entrega parcial debe registrar cantidades entregadas y mantener pendiente el saldo de unidades.

## 3. Clientes, sucursales y pagadores

- **RN-CLI-001:** Diferenciar cliente comercial, punto o sucursal, dirección, receptor y responsable de pago.
- **RN-CLI-002:** Una empresa puede tener varias sucursales o franquicias.
- **RN-CLI-003:** Cada sucursal puede usar a la empresa central, al franquiciado u otro tercero como pagador.
- **RN-CLI-004:** El pedido debe guardar una instantánea del nombre, dirección, contacto y pagador usados al confirmarlo.
- **RN-CLI-005:** Cambiar datos actuales del cliente no altera pedidos históricos.
- **RN-CLI-006:** Las cuentas por cobrar se agrupan por responsable de pago, con desglose por cliente, sucursal y pedido.
- **RN-CLI-007:** Un cliente puede tener condiciones comerciales sugeridas, pero cada pedido conserva el precio realmente acordado.

## 4. Productos, precios y descuentos

- **RN-PRE-001:** Cada producto tendrá precio estándar vigente con fecha de inicio.
- **RN-PRE-002:** El usuario autorizado puede registrar un precio unitario distinto del estándar.
- **RN-PRE-003:** Guardar en cada línea el precio estándar vigente y el precio vendido como instantáneas históricas.
- **RN-PRE-004:** Si precio vendido < precio estándar, descuento unitario = estándar − vendido.
- **RN-PRE-005:** Descuento total de línea = descuento unitario × cantidad. Si el precio vendido es mayor o igual, el descuento es cero.
- **RN-PRE-006:** Todo descuento exige motivo; descuentos superiores a un umbral configurable requieren autorización.
- **RN-PRE-007:** Modificar el precio estándar crea una nueva vigencia; nunca recalcula pedidos anteriores.
- **RN-PRE-008:** Totales monetarios deben redondearse a dos decimales de forma consistente.
- **RN-PRE-009:** Una reposición sin cobro no debe simular una venta con descuento; debe usar el motivo de salida “reposición”.

## 5. Entregas

- **RN-ENT-001:** Cada entrega se asigna a un repartidor y conserva historial de reasignaciones.
- **RN-ENT-002:** El repartidor solo puede consultar y operar las entregas asignadas a él, salvo permiso superior.
- **RN-ENT-003:** Estados propios de la entrega: programada, asignada, en ruta, entregada, parcial, fallida y cancelada. El pedido solo queda totalmente entregado cuando no quedan cantidades pendientes.
- **RN-ENT-004:** Marcar “entregado” exige fecha/hora, persona que recibió y resultado del cobro.
- **RN-ENT-005:** La fotografía es evidencia asociada; no actualiza por sí sola el pedido.
- **RN-ENT-006:** La evidencia puede configurarse como obligatoria por tipo de cliente o entrega.
- **RN-ENT-007:** Una entrega fallida registra motivo y no descuenta unidades como venta.
- **RN-ENT-008:** Al confirmar cantidades entregadas, descontar existencias físicas de los lotes asignados y actualizar reservas.
- **RN-ENT-009:** No permitir entregar más unidades que las preparadas/asignadas sin autorización y ajuste trazable.
- **RN-ENT-010:** Confirmar una entrega debe ser idempotente: un reintento con la misma clave no puede duplicar entrega, salida de inventario, venta, pago o rendición.

## 6. Pagos, cuentas por cobrar y rendiciones

- **RN-PAG-001:** Permitir pago anticipado, completo o parcial y varios pagos para un mismo pedido.
- **RN-PAG-002:** Cada pago guarda monto, fecha/hora, método, receptor del dinero, identificador interno único y comprobante o referencia externa cuando corresponda. La referencia externa puede quedar vacía en efectivo.
- **RN-PAG-003:** Métodos iniciales: efectivo, QR y transferencia; deben ser configurables.
- **RN-PAG-004:** Saldo = total exigible − pagos confirmados − notas de crédito/devoluciones válidas.
- **RN-PAG-005:** Un pago no puede exceder el saldo sin registrar explícitamente crédito a favor o devolución.
- **RN-PAG-006:** Si el repartidor recibe el pago, el cliente queda pagado por el monto recibido y se genera igual monto pendiente de rendición.
- **RN-PAG-007:** Rendir dinero no vuelve a sumar un cobro ni modifica el saldo del cliente.
- **RN-PAG-008:** La rendición puede agrupar varios pagos, pero debe mantener el detalle de origen.
- **RN-PAG-009:** La caja confirma monto recibido, fecha y responsable. Diferencias exigen motivo y resolución.
- **RN-PAG-010:** Anular o corregir un pago exige permiso, motivo y movimiento reverso; no borrar el registro original.
- **RN-PAG-011:** Las cuentas por cobrar deben mostrar pagador, sucursal, pedido, fecha de entrega, total, pagado, saldo y antigüedad.
- **RN-PAG-012:** Estados del pago: registrado, confirmado, anulado y reembolsado. El pago del repartidor queda confirmado al completar válidamente la entrega.
- **RN-PAG-013:** Estados de rendición: pendiente, parcial, rendida, con diferencia y anulada.
- **RN-PAG-014:** Se permiten rendiciones parciales; pendiente de rendición = monto recibido por el colaborador − monto rendido confirmado. Nunca puede ser negativo.
- **RN-PAG-015:** Una misma operación móvil debe usar clave de idempotencia para impedir pagos duplicados por reintentos.

## 7. Producción e inventario terminado

- **RN-INV-001:** Registrar producción en unidades por producto, fecha y lote.
- **RN-INV-002:** Stock físico = entradas de producción y ajustes positivos − salidas confirmadas.
- **RN-INV-003:** Stock reservado = unidades comprometidas en pedidos confirmados aún no entregados ni cancelados.
- **RN-INV-004:** Stock disponible = stock físico − stock reservado.
- **RN-INV-005:** Confirmar un pedido reserva stock. Entregar descuenta stock físico y libera la reserva correspondiente.
- **RN-INV-006:** Si no alcanza el stock, mostrar faltante para producir; no permitir stock negativo silencioso.
- **RN-INV-007:** Movimientos mínimos: producción, venta entregada, degustación, muestra, consumo interno, merma, vencimiento, daño, reposición, donación y ajuste.
- **RN-INV-008:** Toda salida no comercial exige motivo y consume unidades disponibles por FIFO. El costo histórico se registra cuando el lote tiene un costo conocido.
- **RN-INV-009:** Producción registra producto y unidades buenas; el sistema genera el número de lote, registra fecha y aumenta el stock físico. Producción no registra salidas.
- **RN-INV-012:** La persona encargada de envíos registra las salidas no comerciales. La confirmación de una entrega descuenta las unidades reservadas y consume los lotes por FIFO.
- **RN-INV-013:** La receta y el costo estimado del lote se asocian automáticamente cuando existen datos vigentes; su ausencia no bloquea registrar lo producido. Un costo desconocido permanece sin valor.
- **RN-INV-010:** Usar FIFO (primero en entrar, primero en salir) como método inicial para asignar lotes a las salidas, permitiendo asignación manual autorizada por vencimiento.
- **RN-INV-011:** Un ajuste de inventario requiere conteo, diferencia, motivo, usuario y aprobación según umbral.

## 8. Ingredientes, compras y recetas

- **RN-REC-001:** Definir ingredientes con unidad base coherente: g, ml o unidad. Convertir presentaciones de compra a esa unidad.
- **RN-REC-002:** Una compra registra proveedor opcional, fecha, ingrediente, cantidad, presentación, costo total y costo unitario convertido.
- **RN-REC-003:** Calcular costo promedio ponderado tras cada entrada: (valor anterior + valor comprado) / cantidad total resultante.
- **RN-REC-004:** Una receta pertenece a un producto y define ingredientes, cantidades y rendimiento esperado.
- **RN-REC-005:** Estados: borrador, validada, activa, inactiva e histórica.
- **RN-REC-006:** Solo administradores autorizados crean, validan o activan recetas.
- **RN-REC-007:** Solo puede existir una receta activa para un producto en una misma fecha.
- **RN-REC-008:** Activar una nueva versión cierra la vigencia anterior sin eliminarla.
- **RN-REC-009:** Si existe una receta activa, se asocia automáticamente al lote según producto y fecha; el usuario normal no elige de una lista histórica. La receta no es requisito para registrar unidades producidas.
- **RN-REC-010:** Usar una receta excepcional requiere permiso, receta validada, motivo y auditoría.
- **RN-REC-011:** Cambiar cantidad, ingrediente, rendimiento o costo de referencia crea una nueva versión; no editar versiones usadas en producción.
- **RN-REC-012:** Al producir, descontar consumo teórico de ingredientes según receta y escala del lote. Identificarlo como teórico.
- **RN-REC-013:** Si se registra conteo real, guardar la variación entre consumo teórico y real sin modificar el historial de la receta.

## 9. Costos y rentabilidad

- **RN-COS-001:** Costo estándar estimado de receta = suma de cantidades teóricas × costos vigentes de ingredientes, más envases y otros componentes directos configurados.
- **RN-COS-002:** Costo estándar unitario = costo estándar de receta / rendimiento esperado.
- **RN-COS-003:** Al registrar un lote, congelar la versión, los costos usados, el costo total y el costo unitario histórico.
- **RN-COS-004:** Si se conoce el costo total consumido y las unidades buenas reales, costo unitario histórico del lote = costo total / unidades buenas. La merma eleva el costo de las unidades útiles.
- **RN-COS-005:** Si existe un costo estándar vigente, congelarlo como estimación del lote. Si no existe, registrar el costo como desconocido sin bloquear la producción.
- **RN-COS-006:** Degustación, muestra, consumo interno, vencimiento, daño, reposición y donación se valorizan al costo histórico del lote, no al precio de venta.
- **RN-COS-007:** Venta neta = suma de precio vendido × cantidad entregada, menos devoluciones comerciales aplicables.
- **RN-COS-008:** Costo de venta = suma del costo histórico de las unidades entregadas.
- **RN-COS-009:** Ganancia bruta estimada = venta neta − costo de venta.
- **RN-COS-010:** Ganancia neta estimada = ganancia bruta − gastos operativos del periodo.
- **RN-COS-011:** No calcular ganancia como ventas − compras; lo comprado puede permanecer en inventario.
- **RN-COS-012:** Mostrar por separado ventas, cobrado, por cobrar, compras, costo de ventas, gastos, ganancia bruta y ganancia neta.

## 10. Usuarios, auditoría y correcciones

- **RN-SEG-001:** Aplicar mínimo privilegio por rol y permitir combinaciones de permisos autorizadas.
- **RN-SEG-002:** Producción no puede modificar recetas ni costos; reparto no puede ver costos o reportes financieros.
- **RN-SEG-003:** Registrar autor, fecha/hora y detalle anterior/nuevo en operaciones sensibles.
- **RN-SEG-004:** No eliminar físicamente pedidos, pagos, lotes, recetas o movimientos usados; anularlos o desactivarlos con trazabilidad.
- **RN-SEG-005:** Las correcciones financieras o de inventario se realizan con reversos y nuevos movimientos.
- **RN-SEG-006:** Los reportes deben poder rastrear cada cifra hasta sus operaciones de origen.
- **RN-SEG-007:** La interfaz de reparto debe ser móvil, breve y no mostrar funciones ajenas a la tarea.
- **RN-SEG-008:** Exigir sesión autenticada para administración, contabilidad y operaciones internas. Permitir acceso anónimo únicamente a portada, catálogo, fichas públicas, contacto, solicitud de productos, inicio de sesión y recuperación.
- **RN-SEG-009:** No existe registro público. Solo un administrador autorizado puede crear cuentas y asignar o retirar roles.
- **RN-SEG-010:** Usar ASP.NET Core Identity para almacenar contraseñas con hash seguro. Nunca guardar, registrar ni transmitir contraseñas en texto plano.
- **RN-SEG-011:** La cuenta inicial recibe una contraseña temporal y debe cambiarla en el primer inicio de sesión.
- **RN-SEG-012:** Bloquear temporalmente la cuenta después de cinco intentos fallidos consecutivos durante quince minutos. Mantener ambos valores configurables.
- **RN-SEG-013:** La sesión usa cookie segura, `HttpOnly` y protección `SameSite`; cerrar sesión invalida la sesión actual.
- **RN-SEG-014:** Deshabilitar una cuenta impide nuevos accesos y revoca sus sesiones vigentes.
- **RN-SEG-015:** Registrar auditoría de accesos exitosos, fallidos, bloqueos, cierres y restablecimientos sin almacenar contraseñas ni tokens completos.
- **RN-SEG-016:** Si el usuario tiene correo verificado, permitir recuperación mediante un token único y temporal sin revelar públicamente si la cuenta existe. Si no tiene correo, el restablecimiento lo realiza un administrador mediante contraseña temporal.
- **RN-SEG-017:** Aplicar limitación de solicitudes al inicio de sesión y recuperación para reducir ataques automatizados.

## 11. Catálogo público y solicitudes

- **RN-CAT-001:** La portada, el catálogo, las fichas públicas, el contacto y la solicitud de productos deben poder consultarse sin iniciar sesión.
- **RN-CAT-002:** Publicar únicamente productos activos y marcados expresamente como visibles. La desactivación interna no elimina su historial.
- **RN-CAT-003:** Cada ficha pública puede mostrar nombre comercial, descripción, beneficios aprobados, fotografías, presentación, precio público si se autoriza y llamada a la acción.
- **RN-CAT-004:** No exponer costos, márgenes, recetas, existencias exactas, clientes, pedidos, ventas, descuentos internos ni información administrativa.
- **RN-CAT-005:** El botón de WhatsApp usa un número configurable y un mensaje preparado con producto, presentación, cantidad inicial y enlace de origen. No depende de una API pagada para abrir la conversación.
- **RN-CAT-006:** Una solicitud web registra como mínimo nombre, teléfono, productos, cantidades, observación, medio preferido y aceptación del uso de datos necesario para responder.
- **RN-CAT-007:** Estados de solicitud: nueva, en revisión, contactada, convertida, descartada y cancelada.
- **RN-CAT-008:** Crear una solicitud no reserva existencias, no registra una venta, no genera deuda ni crea automáticamente un pedido confirmado.
- **RN-CAT-009:** Solo un usuario interno autorizado puede convertir la solicitud en cliente y pedido; debe conservar el vínculo con la solicitud original y evitar conversiones duplicadas.
- **RN-CAT-010:** Aplicar validación, limitación de solicitudes y protección antispam. Añadir CAPTCHA solamente si el abuso lo justifica.
- **RN-CAT-011:** Usar una URL legible y única (`slug`) por producto y metadatos apropiados para buscadores y vista previa al compartir.
- **RN-CAT-012:** Servir las imágenes públicas desde Cloudinary y mantener separadas las evidencias privadas.
- **RN-CAT-013:** La navegación pública debe ofrecer un acceso discreto y claro a “Iniciar sesión” para el personal, sin mezclar funciones internas con el catálogo.
- **RN-CAT-014:** No implementar pago en línea, carrito de compra confirmado ni cuentas de clientes en el MVP salvo decisión posterior explícita.
- **RN-CAT-015:** Si el contacto comienza directamente en WhatsApp, un usuario de Ventas puede registrar manualmente una solicitud con origen “WhatsApp” para incorporarla al mismo seguimiento. Abrir el enlace de WhatsApp por sí solo no crea registros.
- **RN-CAT-016:** El envío del formulario debe ser idempotente para evitar solicitudes duplicadas por doble clic o reintento de conexión.
- **RN-CAT-017:** El sitio público debe usar un diseño propio de marca, visualmente profesional y diferente del panel administrativo, con imágenes optimizadas, jerarquía clara, confianza y llamadas a la acción consistentes.

## 12. Diseño responsive y usabilidad

- **RN-UX-001:** Todo el sistema debe ser responsive (adaptable) y funcionar correctamente en celulares, tabletas, laptops y monitores de escritorio.
- **RN-UX-002:** Ninguna pantalla puede ocultar, cortar o volver inaccesible información o acciones esenciales debido al tamaño de la pantalla.
- **RN-UX-003:** Menús, paneles, formularios, filtros, tarjetas, ventanas y botones deben reorganizarse según el espacio disponible, sin depender de una resolución fija.
- **RN-UX-004:** Las tablas extensas deben convertirse en tarjetas, priorizar columnas o usar desplazamiento interno controlado; no deben provocar desplazamiento horizontal de toda la página.
- **RN-UX-005:** Los formularios deben mantener etiquetas, mensajes de validación y controles legibles, con áreas táctiles adecuadas para el uso desde celular.
- **RN-UX-006:** Las ventanas modales deben caber dentro de la pantalla, permitir desplazamiento interno y mantener accesibles sus acciones principales.
- **RN-UX-007:** No se debe crear una versión reducida sin funciones esenciales para celular. El mismo proceso debe poder completarse desde cualquier dispositivo autorizado.
- **RN-UX-008:** Validar como mínimo las interfaces en anchos de 360, 768, 1024 y 1440 píxeles antes de considerar una pantalla terminada.
