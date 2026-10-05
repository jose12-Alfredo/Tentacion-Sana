---
name: gestionar-tentacion-sana
description: Diseñar, implementar, revisar o ampliar la plataforma responsive de Tentación Sana con catálogo público profesional y sistema interno protegido, usando C#, ASP.NET Core, Blazor, PostgreSQL en Neon, imágenes en Cloudinary y despliegue en Render. Incluye productos públicos, solicitudes web y contacto por WhatsApp; además de pedidos, clientes, entregas, pagos, cuentas por cobrar, rendiciones, precios, recetas, compras, inventarios, producción, costos, gastos, rentabilidad, usuarios, permisos y reportes. Usar cuando se pidan requisitos, reglas de negocio, historias de usuario, criterios de aceptación, arquitectura .NET, almacenamiento, despliegue, modelo de datos, API, diseño adaptable, interfaz, pruebas o código relacionado con esta plataforma.
---

# Gestionar Tentación Sana

Construir el sistema como fuente única de información del negocio. Mantener conectados pedido, producción, inventario, entrega, cobro y costo; no reproducir la dependencia actual de Excel, mensajes verbales o fotografías en WhatsApp.

## Cargar el contexto necesario

- Leer `references/objetivo-y-alcance.md` antes de proponer arquitectura, módulos, pantallas o prioridades.
- Leer `references/reglas-de-negocio.md` antes de implementar, modificar o validar lógica del dominio.
- Leer `references/historias-de-usuario.md` cuando se preparen backlog, criterios de aceptación, pruebas, pantallas, endpoints o tareas de desarrollo.
- Leer `references/arquitectura-tecnica.md` antes de proponer estructura del proyecto, tecnologías, base de datos, autenticación, componentes Blazor o código.
- Leer `references/identidad-visual.md` antes de diseñar la portada, el catálogo, el inicio de sesión, el panel interno, piezas publicitarias, iconos o cualquier interfaz visible de la marca.
- Leer `references/experiencia-catalogo-y-conversion.md` antes de diseñar o revisar la portada, el catálogo, las fichas, los textos comerciales, las animaciones, los testimonios o las llamadas a la acción.

## Principios obligatorios

1. Usar bolivianos (`Bs`) como moneda inicial y guardar importes en decimal, nunca en coma flotante.
2. Separar cliente comercial, sucursal o punto de entrega, responsable de pago y persona que recibe.
3. Separar estado operativo del pedido, estado de entrega, estado de pago y estado de rendición.
4. Guardar instantáneas históricas de precio estándar, precio vendido, descuento y costo; no recalcular operaciones antiguas con valores actuales.
5. Elegir automáticamente la receta activa según producto y fecha de producción. No mostrar recetas históricas como selector normal al registrar producción.
6. Mantener una sola receta activa por producto y fecha. Una nueva versión no modifica la anterior.
7. Tratar fotografías como evidencia, no como fuente del estado de entrega o pago.
8. Reservar existencias al confirmar el pedido y descontarlas físicamente al entregar o registrar otra salida válida.
9. Registrar todo movimiento de inventario, pago, rendición, cambio de estado y excepción con usuario, fecha y motivo.
10. No llamar “costo exacto” al cálculo basado solo en receta teórica. Denominarlo costo estándar estimado; el costo real requiere consumo o conteo real.
11. Diseñar todo el sistema como responsive (adaptable): cada pantalla, tabla, formulario, menú, panel, ventana y acción debe funcionar correctamente en celulares, tabletas, laptops y monitores de escritorio, sin contenido cortado ni desplazamiento horizontal innecesario.
12. Implementar el sistema con C# sobre la versión LTS vigente de .NET, ASP.NET Core y Blazor Web App. Mantener la interfaz en componentes Razor y usar JavaScript solamente mediante interoperabilidad cuando Blazor o las APIs web no resuelvan la necesidad directamente.
13. Construir un monolito modular (una aplicación desplegable organizada por módulos), no microservicios. Usar Entity Framework Core y PostgreSQL como persistencia principal.
14. Desplegar la aplicación en Render, alojar PostgreSQL en Neon y almacenar imágenes en Cloudinary. No usar PostgreSQL ni el sistema de archivos temporal de Render como almacenamiento de imágenes.
15. Guardar en PostgreSQL únicamente identificadores, metadatos y relaciones de los archivos de Cloudinary. Mantener públicas las imágenes de catálogo y restringidas mediante acceso firmado las evidencias de entrega y los comprobantes.
16. Separar la plataforma en un sitio público y un sistema interno. Permitir acceso anónimo solamente a portada, catálogo, fichas públicas, contacto, solicitud de productos, inicio de sesión y recuperación. Exigir sesión para administración, contabilidad y toda operación interna.
17. No permitir registro público de cuentas internas: solo un administrador autorizado puede crear, habilitar, deshabilitar o asignar roles a usuarios.
18. Tratar una solicitud enviada desde el catálogo como prospecto pendiente, no como venta ni pedido confirmado. No reservar inventario ni generar cuentas por cobrar hasta que un usuario interno la revise y confirme.
19. Usar `assets/logo-oficial-tentacion-sana.png` como logotipo oficial. No redibujarlo, sustituir su tipografía, recolorearlo, deformarlo, recortarlo ni alterar la relación entre el texto y las hojas.
20. Diseñar el catálogo para comunicar delicioso, saludable y premium, usando jerarquía visual y principios de comportamiento de compra de forma ética. Prohibir urgencia falsa, escasez falsa, testimonios inventados, afirmaciones nutricionales no demostradas y cualquier patrón engañoso.

## Flujo de trabajo al responder o construir

1. Identificar módulo, actor y evento de negocio afectado.
2. Consultar las reglas con identificador `RN-*` aplicables.
3. Vincular la solución con las historias `HU-*` correspondientes.
4. Señalar contradicciones con las reglas en vez de aceptarlas silenciosamente.
5. Proponer el flujo más corto para el usuario operativo, especialmente en móvil para reparto y producción.
6. Incluir validaciones, permisos, auditoría y efectos contables o de inventario.
7. Mantener los cambios dentro del alcance solicitado. Marcar como propuesta cualquier regla nueva.
8. Al implementar, crear pruebas para los criterios de aceptación y para los casos límite financieros e históricos.

## Prioridad recomendada

Implementar por etapas:

1. Pedidos, clientes, sucursales, entregas, pagos y cuentas por cobrar.
2. Productos terminados, producción en unidades, reservas, salidas y lotes.
3. Ingredientes, compras, recetas, versiones y costo estándar.
4. Rendiciones, gastos, rentabilidad, reportes y alertas.

No bloquear el primer lanzamiento por intentar una contabilidad de inventario perfecta. Mantener trazabilidad suficiente para mejorar sin perder el historial.

## Criterio de calidad

Una función está incompleta si solo cambia una pantalla. Verificar también permisos, reglas, historial, inventario, cuentas por cobrar, rendiciones, reportes y auditoría que puedan verse afectados.
