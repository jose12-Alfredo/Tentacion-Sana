# Historias de usuario y criterios de aceptación

## Índice

1. Pedidos
2. Clientes y sucursales
3. Productos, precios y descuentos
4. Entregas
5. Pagos y rendiciones
6. Producción e inventario
7. Ingredientes, compras y recetas
8. Costos, gastos y reportes
9. Usuarios y auditoría
10. Catálogo público y solicitudes
11. Diseño responsive y usabilidad
12. Priorización

Formato: **Como [actor], quiero [capacidad], para [beneficio].** Los criterios están escritos en forma verificable.

## 1. Pedidos

### HU-PED-001 — Registrar pedido

Como usuario de ventas, quiero registrar un pedido con cliente, sucursal, pagador, productos, cantidades, precios y observaciones para iniciar su seguimiento.

**Criterios de aceptación**

- Genera número único y registra creador y fecha/hora.
- Calcula subtotales y total.
- No permite confirmar sin cliente, al menos una línea y fecha prometida.
- Conserva borradores sin afectar inventario.

### HU-PED-002 — Definir fechas de entrega

Como usuario de ventas, quiero registrar la fecha del pedido y la fecha prometida para responder al cliente cuándo llegará.

**Criterios de aceptación**

- La fecha del pedido se registra automáticamente y puede corregirse con permiso.
- La fecha prometida es obligatoria al confirmar.
- Al entregar se guarda la fecha/hora real.
- El sistema calcula duración y retraso.

### HU-PED-003 — Consultar y filtrar pedidos

Como administrador, quiero filtrar pedidos por estado, fecha, cliente, sucursal, pagador y repartidor para localizar pendientes rápidamente.

**Criterios de aceptación**

- Muestra estado operativo y de pago por separado.
- Permite buscar por número y nombre.
- Los filtros pueden combinarse.

### HU-PED-004 — Cambiar el estado operativo

Como usuario autorizado, quiero avanzar el pedido por preparación, listo y reparto para conocer su situación real.

**Criterios de aceptación**

- Solo permite transiciones válidas.
- Registra usuario y fecha/hora de cada cambio.
- No confunde “entregado” con “pagado”.

### HU-PED-005 — Cancelar pedido

Como usuario autorizado, quiero cancelar un pedido con motivo para liberar sus unidades sin perder el historial.

**Criterios de aceptación**

- Exige motivo.
- Libera reservas no entregadas.
- No elimina pagos; los deriva al proceso de devolución o crédito.

### HU-PED-006 — Gestionar entrega parcial

Como usuario de ventas, quiero registrar cantidades entregadas parcialmente para mantener pendientes las restantes.

**Criterios de aceptación**

- No permite entregar más de lo pendiente sin autorización.
- Descuenta solo las unidades realmente entregadas.
- Mantiene visible el saldo pendiente de productos e importe.

## 2. Clientes y sucursales

### HU-CLI-001 — Registrar cliente

Como usuario de ventas, quiero registrar una persona o empresa con sus datos de contacto para asociarle pedidos.

**Criterios de aceptación**

- Admite cliente individual o empresa.
- Advierte duplicados evidentes y permite confirmar coincidencias legítimas.
- Conserva pedidos si el cliente se desactiva.

### HU-CLI-002 — Administrar sucursales y franquicias

Como usuario de ventas, quiero registrar varios puntos de entrega de una empresa para entregar en el lugar correcto.

**Criterios de aceptación**

- Cada punto tiene nombre, dirección, ubicación y contacto.
- Permite marcar si es propio, franquicia u otro tipo configurable.
- No mezcla el punto de entrega con el pagador.

### HU-CLI-003 — Asignar responsable de pago

Como administrador, quiero asignar un pagador diferente por sucursal para cobrar a Nutriarte central o al franquiciado correcto.

**Criterios de aceptación**

- Permite elegir empresa central, franquiciado u otro tercero.
- El pedido hereda el pagador sugerido y permite cambiarlo con autorización.
- Las cuentas por cobrar se agrupan por pagador.

### HU-CLI-004 — Conservar datos históricos

Como administrador, quiero que los cambios de dirección o pagador no alteren pedidos pasados para mantener evidencia correcta.

**Criterios de aceptación**

- El pedido conserva una instantánea al confirmarse.
- Los datos nuevos se usan solo en pedidos posteriores o cambios explícitos auditados.

## 3. Productos, precios y descuentos

### HU-PRE-001 — Registrar producto y precio estándar

Como administrador, quiero crear productos con precio estándar vigente para agilizar los pedidos.

**Criterios de aceptación**

- El precio requiere fecha de vigencia.
- Cambiarlo crea un nuevo periodo.
- Pedidos anteriores conservan su precio estándar histórico.

### HU-PRE-002 — Vender a precio acordado

Como usuario de ventas autorizado, quiero cambiar el precio unitario de una línea para respetar una negociación comercial.

**Criterios de aceptación**

- Guarda precio estándar y precio vendido.
- Recalcula descuento automáticamente si el vendido es menor.
- No genera descuento si el precio es mayor.

### HU-PRE-003 — Justificar descuento

Como administrador, quiero exigir el motivo de cada descuento para saber cuánto se concede y por qué.

**Criterios de aceptación**

- No confirma una línea rebajada sin motivo.
- Los motivos son configurables.
- Solicita autorización al superar el límite configurado.

### HU-PRE-004 — Reportar descuentos

Como administrador, quiero consultar descuentos por periodo, producto, cliente, usuario y motivo para controlar concesiones.

**Criterios de aceptación**

- Muestra descuento unitario y total.
- Usa las instantáneas del pedido, no precios actuales.

## 4. Entregas

### HU-ENT-001 — Asignar repartidor

Como usuario de ventas, quiero asignar pedidos listos a Lennon u otro repartidor para organizar el reparto.

**Criterios de aceptación**

- Solo asigna usuarios habilitados como repartidores.
- Registra reasignaciones.
- Muestra la nueva entrega en la bandeja del repartidor.

### HU-ENT-002 — Consultar mis entregas desde celular

Como repartidor, quiero ver solamente mis entregas con dirección, contacto, importe y observaciones para repartir sin revisar Excel o WhatsApp.

**Criterios de aceptación**

- La vista es adaptable a celular.
- Agrupa por fecha y estado.
- Permite abrir ubicación y contacto.
- No expone costos, recetas ni reportes financieros.

### HU-ENT-003 — Confirmar entrega

Como repartidor, quiero marcar un pedido como entregado e identificar a quien recibió para que la empresa se entere inmediatamente.

**Criterios de aceptación**

- Solicita cantidades, receptor, fecha/hora y resultado de pago.
- Adjunta evidencia según configuración.
- Actualiza inventario, reserva, estado y cuenta por cobrar de forma consistente.
- Un reintento de la misma confirmación devuelve el resultado original y no duplica movimientos.

### HU-ENT-004 — Registrar intento fallido

Como repartidor, quiero indicar que no pude entregar y el motivo para reprogramar sin descontar inventario.

**Criterios de aceptación**

- Exige motivo.
- Conserva la entrega pendiente o reprogramada.
- No crea venta entregada ni cobro.

## 5. Pagos y rendiciones

### HU-PAG-001 — Registrar resultado del cobro al entregar

Como repartidor, quiero indicar no pagó, pago completo o pago parcial para actualizar la deuda sin avisos verbales.

**Criterios de aceptación**

- Si hubo pago, solicita método y monto.
- Admite comprobante.
- Un pago parcial deja saldo pendiente.

### HU-PAG-002 — Registrar pago posterior

Como usuario de ventas o caja, quiero registrar el QR, efectivo o transferencia que envía un cliente después de la entrega para saldar su cuenta.

**Criterios de aceptación**

- Aplica el pago al pedido o deuda seleccionada.
- Guarda receptor, método, referencia y fecha.
- Advierte posibles cobros duplicados.

### HU-PAG-003 — Consultar cuentas por cobrar

Como Alfredo o Alison, quiero ver quién debe, cuánto y desde cuándo para realizar el seguimiento de cobro.

**Criterios de aceptación**

- Muestra pagador, cliente, sucursal, pedido, entrega, total, pagado y saldo.
- Filtra pendientes, parciales, pagados y vencidos.
- Calcula antigüedad desde la fecha exigible configurada.

### HU-PAG-004 — Controlar dinero del repartidor

Como responsable de caja, quiero ver los pagos que cada repartidor recibió pero aún no rindió para controlar el dinero bajo su custodia.

**Criterios de aceptación**

- El cliente queda pagado desde la recepción confirmada.
- El monto aparece simultáneamente como pendiente de rendición.
- El panel muestra total por repartidor y detalle por pago.

### HU-PAG-005 — Registrar rendición

Como responsable de caja, quiero confirmar el dinero entregado por el repartidor para cerrar su responsabilidad sin duplicar ingresos.

**Criterios de aceptación**

- Permite seleccionar uno o varios pagos pendientes.
- Registra fecha, caja, receptor y monto.
- Mantiene trazabilidad pago–rendición.
- Permite rendición total o parcial y conserva el saldo pendiente por pago.
- Una diferencia exige motivo y queda abierta hasta resolución.

### HU-PAG-006 — Corregir pago

Como administrador, quiero anular un pago equivocado con motivo para corregir saldos sin borrar evidencia.

**Criterios de aceptación**

- Genera un reverso.
- Restaura el saldo correspondiente.
- Ajusta la rendición relacionada o impide anular hasta resolverla.

## 6. Producción e inventario

### HU-INV-001 — Registrar unidades producidas

Como usuario de producción, quiero registrar producto, fecha y unidades fabricadas para aumentar el inventario terminado.

**Criterios de aceptación**

- Crea un lote identificable.
- Selecciona automáticamente la receta vigente.
- Registra unidades buenas y merma cuando se informa.
- Calcula y congela el costo estimado del lote.

### HU-INV-002 — Ver stock físico, reservado y disponible

Como usuario de ventas, quiero distinguir existencias físicas, comprometidas y disponibles para no prometer unidades ya reservadas.

**Criterios de aceptación**

- Disponible = físico − reservado.
- Cada cifra permite consultar sus movimientos de origen.
- Muestra faltantes al confirmar pedidos.

### HU-INV-003 — Reservar pedido confirmado

Como usuario de ventas, quiero reservar unidades al confirmar un pedido para proteger su disponibilidad.

**Criterios de aceptación**

- Reserva hasta el saldo pendiente de entregar.
- Si no alcanza, informa faltante y no crea stock negativo oculto.
- Cancelar o reducir libera la diferencia.

### HU-INV-004 — Registrar degustación o consumo interno

Como usuario autorizado, quiero retirar unidades por degustación, muestra o consumo interno para descontar stock y reconocer su costo.

**Criterios de aceptación**

- Exige producto, cantidad, motivo, fecha y responsable.
- Selecciona lotes por FIFO salvo excepción autorizada.
- Registra el costo histórico en la categoría correspondiente.

### HU-INV-005 — Registrar merma, vencimiento o daño

Como usuario autorizado, quiero registrar productos perdidos para que el inventario y las pérdidas sean reales.

**Criterios de aceptación**

- Exige motivo y cantidad.
- Descuenta stock y registra pérdida al costo.
- Admite evidencia opcional.

### HU-INV-006 — Registrar reposición

Como usuario de ventas, quiero entregar una reposición sin tratarla como descuento para medir correctamente garantías y pérdidas.

**Criterios de aceptación**

- Se vincula al pedido o incidente original cuando existe.
- Descuenta inventario al costo.
- No incrementa ventas ni descuentos.

### HU-INV-007 — Ajustar inventario contado

Como administrador, quiero comparar conteo físico con el sistema y ajustar diferencias para recuperar exactitud.

**Criterios de aceptación**

- Guarda cantidad esperada, contada y diferencia.
- Exige motivo y aprobación según umbral.
- Genera movimiento, no sobrescribe el saldo sin rastro.

## 7. Ingredientes, compras y recetas

### HU-REC-001 — Registrar ingrediente y presentación

Como administrador, quiero definir ingredientes en g, ml o unidades y sus presentaciones de compra para calcular costos comparables.

**Criterios de aceptación**

- Cada ingrediente tiene una unidad base.
- Una bolsa de 1 kg puede convertirse a 1000 g.
- Rechaza conversiones incompatibles.

### HU-REC-002 — Registrar compra

Como responsable de compras, quiero ingresar cantidades y precios de insumos para actualizar inventario y costo promedio.

**Criterios de aceptación**

- Calcula cantidad base y costo unitario base.
- Actualiza costo promedio ponderado.
- Conserva precio, fecha y comprobante histórico.

### HU-REC-003 — Crear receta en borrador

Como administrador, quiero configurar ingredientes, cantidades y rendimiento de una receta sin afectar producción hasta revisarla.

**Criterios de aceptación**

- El borrador puede editarse.
- Calcula costo estimado con los valores seleccionados.
- No puede usarse en producción normal.

### HU-REC-004 — Validar receta

Como administrador autorizado, quiero revisar y validar la formulación para evitar que una receta incompleta llegue a producción.

**Criterios de aceptación**

- Verifica producto, ingredientes, unidades, cantidades y rendimiento positivo.
- Registra quién validó y cuándo.
- Los cambios posteriores requieren una nueva validación.

### HU-REC-005 — Activar nueva versión

Como administrador, quiero activar una receta desde una fecha para que las producciones nuevas usen el costo actualizado.

**Criterios de aceptación**

- Muestra costo anterior, nuevo y variación.
- Pide confirmación y fecha de vigencia.
- Cierra la versión anterior sin modificar lotes históricos.
- Impide solapamiento de recetas activas.

### HU-REC-006 — Selección automática de receta

Como usuario de producción, quiero que el sistema elija la receta oficial por producto y fecha para no equivocarme entre versiones.

**Criterios de aceptación**

- No muestra recetas históricas en el selector común.
- Informa receta vigente y costo estimado antes de confirmar.
- Bloquea producción si no existe receta activa y deriva a un administrador.

### HU-REC-007 — Usar receta excepcional

Como administrador, quiero autorizar una receta validada distinta en un lote excepcional para registrar lo que realmente ocurrió.

**Criterios de aceptación**

- Solo aparece con permiso especial.
- Exige motivo.
- El lote queda marcado como excepción y conserva la receta utilizada.

### HU-REC-008 — Descontar consumo teórico

Como administrador, quiero que registrar producción descuente automáticamente los insumos previstos para evitar capturar cada gramo cada vez.

**Criterios de aceptación**

- Escala cantidades según la producción o tanda registrada.
- Identifica el consumo como teórico.
- Alerta faltantes de materia prima.
- Permite posteriormente ajustar por conteo real con trazabilidad.

## 8. Costos, gastos y reportes

### HU-COS-001 — Calcular costo estándar

Como administrador, quiero obtener el costo estimado de una receta y por unidad para establecer precios y márgenes.

**Criterios de aceptación**

- Incluye ingredientes, envase y componentes directos configurados.
- Divide por rendimiento esperado.
- Muestra fecha y fuentes de costo.
- Lo etiqueta como estimado si no existe consumo real.

### HU-COS-002 — Conservar costo histórico del lote

Como administrador, quiero que cada lote conserve el costo aplicado al producirse para medir ganancias pasadas correctamente.

**Criterios de aceptación**

- Cambiar precios o recetas no recalcula lotes cerrados.
- Cada salida utiliza el costo del lote consumido.

### HU-COS-003 — Registrar gastos

Como responsable de finanzas, quiero registrar delivery, publicidad, combustible y otros gastos para calcular utilidad neta.

**Criterios de aceptación**

- Guarda categoría, fecha, importe, descripción y comprobante opcional.
- Distingue compra de inventario y gasto operativo.

### HU-COS-004 — Ver dashboard administrativo

Como Alfredo o Alison, quiero ver un resumen del negocio para decidir qué producir, cobrar y corregir.

**Criterios de aceptación**

- Muestra ventas, cobrado, por cobrar, costo de ventas, gastos y utilidades por separado.
- Muestra pedidos, entregas retrasadas, stock bajo y rendiciones pendientes.
- Permite elegir periodo.

### HU-COS-005 — Reportar rentabilidad

Como administrador, quiero analizar rentabilidad por producto, cliente, sucursal y periodo para tomar decisiones comerciales.

**Criterios de aceptación**

- Usa precio vendido y costo histórico.
- Desglosa descuentos y salidas no comerciales.
- Permite rastrear cifras hasta pedidos, lotes y gastos.

### HU-COS-006 — Reportar puntualidad

Como administrador, quiero medir tiempo promedio y retrasos para mejorar la promesa de entrega.

**Criterios de aceptación**

- Compara fecha del pedido, prometida y real.
- Muestra promedio, mediana, puntualidad y pedidos retrasados.

## 9. Usuarios y auditoría

### HU-SEG-001 — Administrar roles y permisos

Como administrador, quiero asignar permisos por función para que cada persona vea y modifique solo lo necesario.

**Criterios de aceptación**

- Incluye perfiles administrador, ventas, producción, repartidor y finanzas.
- Permite combinar perfiles autorizados.
- Los controles se aplican en interfaz y servidor.

### HU-SEG-002 — Consultar auditoría

Como administrador, quiero saber quién cambió estados, precios, recetas, pagos e inventarios para resolver diferencias.

**Criterios de aceptación**

- Muestra usuario, fecha/hora, acción y valores anterior/nuevo.
- Permite filtrar por entidad y periodo.
- Los usuarios operativos no pueden alterar la auditoría.

### HU-SEG-003 — Corregir sin borrar historial

Como administrador, quiero revertir operaciones equivocadas para mantener cifras correctas y trazables.

**Criterios de aceptación**

- Usa anulaciones o movimientos reversos.
- Exige motivo.
- Actualiza saldos afectados de forma atómica.

### HU-SEG-004 — Iniciar sesión

Como usuario interno, quiero iniciar sesión con mi usuario y contraseña para acceder únicamente a las funciones permitidas por mis roles.

**Criterios de aceptación**

- No permite acceder a módulos protegidos sin autenticación.
- Si las credenciales son válidas y la cuenta está activa, crea una sesión segura y dirige al panel correspondiente.
- Si la contraseña es temporal, obliga a cambiarla antes de continuar.
- No revela si falló el usuario o la contraseña.
- Registra el intento sin almacenar la contraseña.
- Después de cinco intentos fallidos bloquea temporalmente la cuenta durante quince minutos, con valores configurables.

### HU-SEG-005 — Cerrar sesión

Como usuario autenticado, quiero cerrar sesión para impedir que otra persona utilice mi cuenta en el mismo dispositivo.

**Criterios de aceptación**

- Invalida la sesión actual.
- Regresa a la pantalla de inicio de sesión.
- Las páginas protegidas dejan de estar disponibles mediante navegación o recarga.

### HU-SEG-006 — Administrar cuentas internas

Como administrador, quiero crear, editar, habilitar y deshabilitar usuarios y asignarles roles para controlar el acceso al sistema.

**Criterios de aceptación**

- No existe una pantalla de registro público.
- Una cuenta nueva recibe una contraseña temporal y debe cambiarla en el primer acceso.
- Solo usuarios autorizados pueden modificar roles.
- Deshabilitar una cuenta impide el acceso y revoca sus sesiones vigentes.
- Los cambios quedan registrados en auditoría.

### HU-SEG-007 — Recuperar o restablecer acceso

Como usuario interno, quiero recuperar mi acceso de forma segura para volver a entrar sin que se exponga mi cuenta.

**Criterios de aceptación**

- Si existe correo verificado, envía un token único, temporal y de un solo uso.
- La respuesta pública no confirma si el usuario existe.
- Si no existe correo verificado, un administrador genera una contraseña temporal.
- Al completar el restablecimiento se invalidan los tokens anteriores y se registran los eventos de seguridad.

## 10. Catálogo público y solicitudes

### HU-CAT-001 — Consultar una portada profesional

Como visitante, quiero conocer la marca, sus productos y sus beneficios desde una portada atractiva para decidir si deseo comprar o contactar a Tentación Sana.

**Criterios de aceptación**

- Puede abrirse sin iniciar sesión.
- Presenta propuesta de valor, productos destacados, fotografías profesionales, beneficios aprobados y llamadas claras a la acción.
- Mantiene navegación visible hacia catálogo, contacto, WhatsApp e inicio de sesión del personal.
- No muestra información interna del negocio.

### HU-CAT-002 — Explorar el catálogo público

Como visitante, quiero buscar y filtrar productos publicados para encontrar rápidamente una opción que me interese.

**Criterios de aceptación**

- Solo muestra productos activos y marcados como públicos.
- Permite consultar nombre, imagen, presentación, descripción y precio público cuando esté autorizado.
- No expone existencias exactas, costos, márgenes ni recetas.
- Si no existen resultados, muestra una alternativa de contacto.

### HU-CAT-003 — Consultar la ficha del producto

Como visitante, quiero abrir una ficha completa para entender el producto antes de solicitarlo.

**Criterios de aceptación**

- Usa una URL legible y única.
- Muestra contenido e imágenes aprobados para publicación.
- Incluye botones visibles para solicitar el producto y contactar por WhatsApp.
- Genera metadatos adecuados para buscadores y vista previa al compartir.

### HU-CAT-004 — Contactar por WhatsApp

Como visitante, quiero abrir WhatsApp con un mensaje preparado para consultar un producto sin copiar manualmente sus datos.

**Criterios de aceptación**

- Utiliza el número comercial configurado, no uno escrito directamente en el componente.
- El mensaje incluye producto, presentación, cantidad inicial editable y enlace de origen.
- Funciona desde celular y computadora.
- Abrir WhatsApp no crea automáticamente una venta ni reserva inventario.

### HU-CAT-005 — Enviar solicitud de productos

Como visitante, quiero indicar los productos y cantidades que me interesan para que Tentación Sana se comunique conmigo.

**Criterios de aceptación**

- Solicita nombre, teléfono, productos, cantidades, observación y medio preferido de contacto.
- Informa y solicita la aceptación necesaria para usar los datos con el fin de responder.
- Protege el formulario con validación, limitación de solicitudes y medidas antispam.
- Al enviarse crea una solicitud con estado “nueva”.
- Un doble clic o reintento no crea solicitudes duplicadas.
- No reserva inventario, no genera deuda y no crea una venta ni pedido confirmado.

### HU-CAT-006 — Gestionar solicitudes públicas

Como usuario de ventas, quiero revisar, contactar, convertir o descartar solicitudes web para transformarlas de manera controlada en pedidos reales.

**Criterios de aceptación**

- Permite filtrar solicitudes nuevas, en revisión, contactadas, convertidas, descartadas y canceladas.
- Conserva fecha, origen, visitante, productos, cantidades y responsable interno.
- Permite registrar manualmente una conversación iniciada en WhatsApp como solicitud con ese origen.
- Convertir crea o vincula cliente y genera un pedido interno todavía sujeto a confirmación.
- Mantiene el vínculo entre solicitud, cliente y pedido.
- Impide convertir dos veces la misma solicitud.

### HU-CAT-007 — Administrar la publicación del catálogo

Como administrador, quiero decidir qué productos, imágenes, textos y precios son públicos para mantener un catálogo profesional sin exponer información interna.

**Criterios de aceptación**

- Permite publicar, ocultar y ordenar productos destacados.
- Permite elegir imagen principal y galería desde Cloudinary.
- Separa el precio público de precios negociados o internos.
- Ocultar un producto no elimina sus pedidos ni historial.
- Los cambios quedan registrados en auditoría.

## 11. Diseño responsive y usabilidad

### HU-UX-001 — Utilizar todo el sistema desde cualquier pantalla

Como usuario del sistema, quiero utilizar todas las funciones desde celular, tableta, laptop o computadora de escritorio para trabajar sin depender de un dispositivo específico.

**Criterios de aceptación**

- Todas las pantallas funcionan correctamente en anchos de 360, 768, 1024 y 1440 píxeles.
- No existe contenido cortado ni desplazamiento horizontal innecesario en la página.
- Menús, paneles, formularios, filtros y ventanas se reorganizan según el espacio disponible.
- Las tablas extensas usan tarjetas, columnas priorizadas o desplazamiento interno controlado.
- Los botones y controles mantienen áreas táctiles adecuadas en celular.
- Ninguna acción esencial desaparece en versiones móviles.
- Los mensajes de error y validación siguen siendo visibles y comprensibles en todos los tamaños.

## 12. Priorización

### MVP (producto mínimo viable)

HU-PED-001 a HU-PED-005; HU-CLI-001 a HU-CLI-004; HU-PRE-001 a HU-PRE-003; HU-ENT-001 a HU-ENT-004; HU-PAG-001 a HU-PAG-005; HU-INV-001 a HU-INV-006; HU-COS-004; HU-SEG-001 a HU-SEG-007; HU-CAT-001 a HU-CAT-007; HU-UX-001.

### Segunda etapa

HU-PED-006; HU-PRE-004; HU-PAG-006; HU-INV-007; HU-REC-001 a HU-REC-008; HU-COS-001 a HU-COS-003, HU-COS-005 y HU-COS-006.
