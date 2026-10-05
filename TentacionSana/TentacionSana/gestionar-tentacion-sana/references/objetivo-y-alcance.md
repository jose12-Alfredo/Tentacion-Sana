# Objetivo y alcance del sistema

## Índice

1. Objetivo general
2. Problema que resuelve
3. Objetivos específicos
4. Actores
5. Módulos
6. Flujo principal
7. Alcance inicial y evolución
8. Indicadores

## 1. Objetivo general

Desarrollar una plataforma web completamente responsive (adaptable) con dos áreas integradas: un sitio público profesional para presentar Tentación Sana, mostrar productos y recibir solicitudes o contactos por WhatsApp; y un sistema interno protegido para controlar la operación desde que se confirma un pedido hasta que se produce, entrega, cobra y analiza su rentabilidad. Todas las pantallas y funciones deben conservar su legibilidad, jerarquía y facilidad de uso en cualquier tamaño de pantalla. La parte interna debe reemplazar la dispersión entre Excel, WhatsApp y avisos verbales por registros trazables, simples y confiables.

El sistema se implementará con C# sobre la versión LTS vigente de .NET, ASP.NET Core y Blazor Web App. La solución será un monolito modular (una sola aplicación organizada por módulos), utilizará Entity Framework Core para acceso a datos y PostgreSQL como base de datos principal.

La aplicación se desplegará inicialmente en Render usando su subdominio gratuito, PostgreSQL se alojará en Neon y las imágenes se almacenarán en Cloudinary. El sistema no dependerá del disco local de Render para conservar archivos y no guardará imágenes binarias dentro de PostgreSQL.

El sistema debe permitir saber en cualquier momento:

- qué pidió cada cliente y cuándo se le prometió la entrega;
- qué debe producirse y cuánto producto hay disponible o reservado;
- quién tiene cada entrega y si fue completada;
- quién pagó, cuánto pagó, quién recibió el dinero y cuánto falta cobrar o rendir;
- qué cliente, sucursal o responsable de pago mantiene una deuda;
- cuánto se vendió, cobró, descontó y gastó;
- cuál es el costo estándar estimado e histórico de cada producto o lote;
- qué utilidad bruta y neta estimada genera el negocio.

## 2. Problema que resuelve

Actualmente Alfredo y Alison agendan pedidos en una hoja de cálculo. Lennon informa las entregas mediante fotografías en un grupo de WhatsApp y, en algunos casos, recibe pagos en efectivo o QR sin registrarlos inmediatamente. Alfredo y Alison deben interpretar mensajes, actualizar entregas y luego cobrar a los clientes. Esto genera riesgo de pedidos sin actualizar, cobros duplicados u olvidados, efectivo pendiente de rendición, inventario incierto y rentabilidad desconocida.

La fotografía no debe ser el mecanismo para enterarse de una entrega. Lennon debe marcarla directamente en el sistema; la fotografía será evidencia opcional u obligatoria según configuración.

## 3. Objetivos específicos

1. Centralizar pedidos, fechas, productos, cantidades, precios y observaciones.
2. Informar la fecha prometida al cliente y medir el tiempo y los retrasos reales.
3. Administrar empresas, clientes individuales, sucursales, franquicias, puntos de entrega y pagadores.
4. Facilitar al repartidor una vista móvil de sus entregas asignadas.
5. Controlar pagos completos, parciales, anticipados y pendientes.
6. Diferenciar dinero cobrado al cliente de dinero rendido a caja.
7. Mantener inventario de producto terminado por unidades, reservas, lotes y motivos de salida.
8. Administrar ingredientes, compras y existencias teóricas de materia prima.
9. Calcular costos estándar con recetas validadas y versionadas por fecha.
10. Conservar el costo histórico del lote aunque cambien recetas o precios.
11. Registrar degustaciones, consumo interno, mermas, reposiciones y donaciones como salidas con costo.
12. Calcular ventas, costo de ventas, gastos y utilidad sin confundir compras con consumo.
13. Proporcionar paneles y reportes útiles para la gestión diaria.
14. Publicar una portada y catálogo visualmente profesionales con fichas de producto, fotografías, beneficios y llamadas claras a solicitar o contactar por WhatsApp.
15. Recibir solicitudes públicas sin convertirlas automáticamente en ventas, reservas o cuentas por cobrar.

## 4. Actores

| Actor | Responsabilidad principal |
|---|---|
| Administrador | Configuración completa, usuarios, recetas, costos, correcciones y reportes |
| Ventas | Clientes, pedidos, precios autorizados, seguimiento y cobros |
| Producción | Consulta de demanda y registro de unidades o lotes producidos |
| Repartidor | Consulta de entregas asignadas, evidencia, entrega y pago recibido |
| Finanzas/Caja | Pagos, cuentas por cobrar, rendiciones, compras, gastos y reportes |

Asignación inicial sugerida: Alfredo como administrador; Alison como administradora/ventas/producción; Lennon como repartidor. Los permisos deben configurarse, no quedar ligados permanentemente a nombres.

## 5. Módulos

| Módulo | Propósito |
|---|---|
| Dashboard | Resumir ventas, cobros, deuda, pedidos, inventario, rendiciones y utilidad |
| Sitio público | Presentar marca, propuesta de valor, productos y medios de contacto |
| Catálogo público | Mostrar únicamente productos y contenido aprobados para publicación |
| Solicitudes web | Recibir, revisar, contactar, convertir o descartar solicitudes de visitantes |
| Pedidos | Registrar y seguir pedidos, fechas, detalle, precios y estados |
| Clientes | Administrar personas, empresas y datos comerciales |
| Sucursales/puntos | Administrar lugares de entrega y pagadores por punto |
| Productos y precios | Catálogo, precio estándar, precios vendidos y descuentos |
| Entregas | Asignación, ruta, confirmación, evidencia y recepción de pago |
| Pagos | Registrar pagos y aplicarlos a pedidos o saldos |
| Cuentas por cobrar | Mostrar saldos, antigüedad, vencimientos y responsables de pago |
| Rendiciones | Controlar dinero recibido por repartidores y entregado a caja |
| Producción | Registrar lotes y unidades buenas, mermas y receta aplicada |
| Inventarios | Materia prima, reservas, producto terminado y movimientos |
| Ingredientes y compras | Presentaciones, unidades, costos y costo promedio ponderado |
| Recetas y costos | Fórmulas, rendimiento, validación, vigencia y costo estándar |
| Gastos | Delivery, publicidad y demás egresos operativos |
| Reportes | Rentabilidad, ventas, descuentos, tiempos, deuda y pérdidas |
| Usuarios y permisos | Roles, acceso y auditoría |

## 6. Flujo principal

1. Un visitante consulta la portada, el catálogo o una ficha pública sin iniciar sesión.
2. El visitante solicita productos mediante formulario o abre WhatsApp con un mensaje preparado.
3. La solicitud queda pendiente y no modifica inventario, ventas ni cuentas por cobrar.
4. Ventas revisa la solicitud, contacta al interesado y, si existe confirmación, la convierte en cliente y pedido interno.
5. Al confirmar el pedido, el sistema reserva producto disponible y muestra faltantes a producir.
6. Producción registra las unidades fabricadas; el sistema asigna la receta vigente y crea el lote.
7. Se asigna la entrega a Lennon u otro repartidor.
8. El repartidor consulta dirección, contacto, productos, importe y observaciones desde el celular.
9. Al entregar, registra evidencia, receptor y si hubo pago completo, parcial o ninguno.
10. El sistema descuenta existencias, actualiza la cuenta por cobrar y, si el repartidor recibió dinero, abre una rendición pendiente.
11. Caja confirma la rendición sin alterar que el cliente ya pagó.
12. Los reportes usan precios y costos históricos del pedido y de los lotes consumidos.

## 7. Alcance inicial y evolución

### MVP (producto mínimo viable)

- pedidos y fechas;
- portada, catálogo público, fichas de productos y contacto por WhatsApp;
- solicitudes web y conversión manual a pedido;
- clientes, sucursales y pagadores;
- productos, precio estándar, precio vendido y descuentos;
- entregas móviles y evidencia;
- pagos, saldos y rendiciones;
- producción e inventario de producto terminado en unidades;
- salidas por venta, degustación, consumo interno, merma y reposición;
- panel operativo y reportes básicos.

### Segunda etapa

- ingredientes y presentaciones;
- compras y existencias teóricas;
- recetas versionadas y costo promedio ponderado;
- consumo teórico automático por producción;
- rentabilidad histórica por lote, producto, cliente y periodo.

### Fuera del alcance confirmado

No asumir facturación tributaria, integración bancaria, optimización automática de rutas, tienda pública ni conexión automática con WhatsApp. Incorporarlas solo con una nueva decisión de negocio.

## 8. Indicadores

- ventas registradas, cobrado y por cobrar;
- pedidos por estado, entregas puntuales y retrasadas;
- tiempo promedio desde pedido hasta entrega;
- stock físico, reservado y disponible;
- unidades producidas, vendidas y retiradas por motivo;
- descuentos otorgados y sus motivos;
- dinero recibido por cada repartidor y pendiente de rendición;
- costo estándar, costo de ventas y utilidad bruta estimada;
- gastos y utilidad neta estimada;
- clientes y pagadores con saldo vencido.
