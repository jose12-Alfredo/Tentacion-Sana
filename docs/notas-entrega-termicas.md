# Notas de entrega térmicas

## Uso

Dentro de **Pedidos → detalle → Generar nota de entrega**:

1. Seleccionar la entrega cuando exista más de una. Sin entrega asignada se documentan las unidades pendientes verificables del pedido.
2. Revisar venta, reposición y degustación. La vista previa lleva «VISTA PREVIA» y no consume números ni escribe documentos.
   Revisar **Mostrar pendiente de pago**: se activa automáticamente cuando hay un importe pendiente verificable, incluso antes de confirmar la entrega y sin cuenta por cobrar. Puede desactivarse antes de emitir; esa decisión se respeta al actualizar la vista previa.
3. Pulsar **Emitir nota de entrega** para conservar la emisión definitiva `NE-000001`.
4. Descargar PDF/PNG o compartir con la aplicación Android de impresión.
5. Para reimprimir, seleccionar una nota en **Consultar o reimprimir → Abrir original**. Se descargan los mismos bytes y el mismo número, aunque el pedido haya cambiado.

Si el pedido se canceló después de emitir, **Consultar notas emitidas** permite recuperar sus originales; no ofrece una emisión nueva.

No se confirma la entrega, no se registra pago, no se mueve stock y no se sustituye la foto obligatoria de la boleta. Se mantiene la naturaleza de nota de entrega; el nuevo comprobante no incluye referencias fiscales ni explicaciones de procesos internos. Abrir o compartir un archivo no prueba que la impresora lo haya impreso.

## Alcance

- Venta: cantidades de venta × precio vendido histórico. No se utiliza el precio estándar como precio de cobro.
- Reposición y degustación: aparecen separadas, con sus motivos y observaciones, como **Sin cargo: Bs 0,00**. No se fusionan líneas con motivos diferentes. Publicidad utiliza el motivo ya existente de degustación; no es una nueva categoría comercial.
- Entrega parcial: únicamente cantidades pendientes asignadas. Se excluyen unidades entregadas previamente.
- Entrega terminada: se consultan los originales emitidos. Si no los hay, el documento se identifica como resumen acumulado de entrega registrada, sin inventar recepciones parciales anteriores.
- Envío pendiente: solo la primera salida programada lo incluye si aún no hay ninguna entrega registrada. Las otras no lo repiten. Una emisión con contenido cambiado no vuelve a atribuir un envío ya documentado a otra operación.
- Resumen histórico: con varias entregas completadas se omite la atribución individual del envío porque el esquema comercial no guarda un cargo de envío por entrega. El modal informa esta limitación; no se usa el saldo por cobrar como sustituto del importe documental.
- No se imprimen notas generales administrativas, costos de producción, márgenes ni saldos financieros.

## Configuración

Sección `ThermalNotes` de la aplicación:

| Opción | Valor inicial |
|---|---:|
| `PaperWidthMm` | 58 |
| `PrintableWidthMm` | 48 |
| `PixelWidth` | 384 |
| `LogoPath` | `wwwroot/images/brand/logo_tentacion_sana_termico.png` |
| `ContactPhone` | `78969873` |

48 mm útiles, aproximadamente 203 DPI y 384 puntos son estimaciones iniciales, no especificaciones verificadas de una Knup concreta. El PDF tiene página de 58 mm y márgenes laterales para el área útil; el PNG recorta esos márgenes externos, conserva un pequeño margen de seguridad interno y tiene el ancho digital exacto configurado. No se reduce una hoja A4.

El PNG se codifica como escala de grises **de 1 bit**, sin transparencia, con píxeles exclusivamente negros o blancos. PDF y PNG parten de la misma composición QuestPDF. No se añadieron dependencias gráficas ni una integración Bluetooth.

El pie recupera `ThermalNotes:ContactPhone`, restaurado por instrucción comercial a **78969873**. Es exclusivo de las notas: no modifica `Business:WhatsAppNumber` ni los enlaces de WhatsApp de otras pantallas. Puede configurarse mediante `ThermalNotes__ContactPhone`; si se configura explícitamente vacío, se informa en la vista previa. El nombre histórico se toma de `OrderLine.ProductName`; no se deducen sabores ni se incorpora una presentación actual como si fuese histórica.

### Diseño comercial v2 y logotipo autorizado

El recurso `wwwroot/images/brand/logo_tentacion_sana_termico.png` se copió **byte por byte** del PNG circular autorizado en `C:\Users\jose1\Downloads\logo_tentacion_sana_termico.png`. No se aplican filtros en el servicio ni se sustituye el logo del resto de la aplicación. SHA256: `fee0b9458ee25e63dd64af1afd165f6f40b561d1ac68e7299536b4dd8d3af394`.

La imagen completa de 256 × 256 se coloca centrada a 248 px de ancho sobre una salida de 384 px, manteniendo proporción, círculo, hojas, letras negras sólidas y fondo blanco. También se verificó que los píxeles de la imagen incrustada en los once PDF coinciden con el original.

La composición sigue la referencia `C:\Users\jose1\Downloads\nota_entrega_tentacion_sana_58mm.pdf` y el orden de contenido solicitado: logo, título, número, datos etiquetados, productos de venta, adicionales, cantidades, importes, estado opcional, observaciones y firmas. El PNG de referencia del comprobante no se encontró como archivo accesible; las imágenes adjuntas corresponden al logo. La referencia PDF renderizada está en `.artifacts/thermal-notes/referencia-proporcionada.png`.

Se usa Lato, texto base de 8 pt, detalles de 7–7,5 pt, título de 11,5 pt e importe total de 12 pt en negrita. Moneda uniforme `Bs 17,00`, subtotales alineados a la derecha, separadores finos y ausencia de bloques negros. Categorías con cero unidades se omiten; se muestran **TOTAL A ENTREGAR** o **TOTAL ENTREGADO**, según el alcance. Las fechas programada y realizada conservan sus etiquetas y fuentes diferentes. Cliente, sucursal y dirección tienen etiquetas; se omiten duplicados exactos ignorando espacios exteriores y mayúsculas.

### Mostrar pendiente de pago — comportamiento actual v2.2

- Apagado: no se imprime estado de pago ni se reserva espacio para su recuadro.
- La selección automática se activa ante una deuda verificable. La ausencia de una cuenta por cobrar **no** significa que el pedido esté cubierto: se compara el importe comercial con los pagos confirmados atribuibles.
- Activado y sin pagos confirmados: **PENDIENTE DE PAGO**, si hay productos cobrables.
- Pedido completo cubierto parcialmente: también **PENDIENTE DE PAGO**. Las nuevas emisiones nunca imprimen `PAGADO` ni `PAGO PARCIAL`.
- Importe completo cubierto o productos exclusivamente sin cargo: se omite el indicador y la selección automática queda apagada.
- Una entrega parcial sin pagos registrados tiene deuda verificable. Si hay cobros al pedido que no permiten determinar qué parte corresponde a esa salida, se omite el estado y se muestra un aviso visible en el modal. No se distribuyen arbitrariamente pagos entre entregas, aunque un pago tenga una entrega como referencia contextual.
- Se consultan `PaymentAllocations` del pedido y sus `Payments` confirmados. Además se reconocen pagos directos antiguos con `Payment.OrderId` atribuible, **solo si no tienen aplicaciones a ningún pedido**, evitando duplicarlos o atribuir al pedido importes aplicados a otros. Si una entrega referenciada no pertenece al pedido, se omite el indicador y se avisa. Se contrasta `Receivable.PaidAmount` cuando existe; una discrepancia impide mostrar un estado no verificable. No se consulta la deuda global del cliente como sustituto del importe de la nota.
- Si no se puede atribuir el pendiente de una entrega parcial, la selección automática queda apagada y se muestra un aviso visible. Una selección manual no puede forzar un estado no verificable.
- La selección automática se aplica mientras el usuario no haya elegido explícitamente. Marcar o desmarcar fija la decisión durante la preparación de la nota, incluida la actualización de vista previa.
- El control no cambia datos financieros. No muestra importes de deuda en el documento ni permite editar el estado real. Al abrir un original, refleja la preferencia guardada y queda deshabilitado; **Actualizar vista previa** vuelve al flujo de nueva emisión.

## Persistencia y migración

`20261126003356_AddThermalDeliveryNotes` añade exclusivamente:

- Secuencia PostgreSQL `DeliveryNoteNumberSequence`.
- Tabla `DeliveryNotes`, con referencia a pedido/entrega, usuario/fecha, snapshot JSON, versión de plantilla, PDF y PNG originales.
- Restricciones únicas para número, solicitud idempotente y huella de contenido por pedido.

La numeración usa `nextval`, nunca `Count + 1`. Puede tener huecos por transacciones revertidas; eso no implica una emisión existente. Dos solicitudes concurrentes del mismo contenido recuperan una única nota. Cambiar el pedido no altera archivos ya emitidos. La emisión añade únicamente documento y auditoría documental.

El rediseño **no requiere otra migración**. Se reutiliza `SnapshotJson` para `ShowPaymentStatus`, `PaymentLabel`, `PaymentWarning` y `TemplateVersion`. El rediseño inicial se identificó como `58mm-v2`; el ajuste compacto usa `58mm-v2.1`; la política de pendiente único y contacto restaurado usa `58mm-v2.2`. La huella incluye la preferencia, el resultado y la versión de diseño; si el resultado impreso cambia entre vista previa y emisión, se solicita actualizar la vista previa. No se reescriben filas ni archivos históricos. Los snapshots v1 sin estos campos se leen con pago oculto y versión v1. La reimpresión devuelve los bytes guardados, sin renderizar de nuevo, incluso después de un pago o un cambio de contacto. Una nota antigua que ya incluyera otra leyenda conserva exactamente su original; la política nueva se aplica solo a nuevas emisiones.

El servicio usa contextos propios de lectura y escritura documental. Requiere usuario habilitado, sin cambio de contraseña pendiente, con política equivalente a ventas/administración. No crea nuevas entradas en el menú.

**No se aplicó esta migración a producción.** Se probó en PostgreSQL real temporal en loopback. El arranque existente de `Program.cs` aplica migraciones pendientes: revisar el rollout y la migración antes de cualquier despliegue autorizado.

Para el tooling EF, `ApplicationDbContextDesignFactory` exige `TENTACION_DESIGN_CONNECTION`; no carga secretos ni arranca la aplicación. Para pruebas reales, `TENTACION_THERMAL_TEST_CONNECTION` debe apuntar a PostgreSQL local de pruebas (loopback). Los tests crean bases aleatorias con prefijo `thermal_note_test_` y las eliminan al terminar; nunca aceptan un servidor remoto.

## Verificación

```powershell
dotnet build TentacionSana.slnx --configuration Release
dotnet test TentacionSana.slnx --configuration Release --no-build
```

Definir `TENTACION_THERMAL_TEST_CONNECTION` únicamente con la conexión del clúster aislado para ejecutar los tests PostgreSQL; sin ella esos tests se declaran omitidos, no aprobados. Definir `TENTACION_THERMAL_SAMPLES` con una carpeta de pruebas para escribir muestras PDF/PNG anonimizadas. Las muestras y capturas de esta implementación se guardan en `.artifacts/thermal-notes/`, excluido de Git.

### Resultado de la verificación local del rediseño

- Compilación de solución Release: sin errores ni advertencias.
- Suite completa: **146 pruebas unitarias y 65 de integración aprobadas (211), sin omisiones**. Incluye las reglas anteriores y PostgreSQL real, rollback/reaplicación de migración, concurrencia, permisos, recuperación tras cancelación, compatibilidad v1 y ausencia de cambios comerciales/financieros por operaciones documentales.
- Casos nuevos: pago oculto, pendiente, parcial, cobertura completa sin indicador de pago completado, solo sin cargo incluso con envío existente, entregas parciales verificables/no atribuibles, cambio de preferencia, pago posterior a la vista previa y a la emisión, original visible/oculto conservado, logo exacto, altura dinámica sin recuadro y salida PDF/PNG.
- Once muestras regeneradas en PDF/PNG: `normal`, `degustaciones`, `reposiciones`, `publicidad`, `adicionales`, `motivos-extensos`, `solo-sin-cargo`, `entrega-parcial`, `pago-pendiente`, `pago-parcial` y `sin-informacion-pago`.
- Validación adicional de los once PDF con PyMuPDF, instalado solo en el directorio temporal de QA: ancho de 58 mm, límites del texto, total de 12 pt, estado correcto, ausencia de textos fiscales/internos e igualdad de píxeles del logo incrustado. PNG de 384 px y 1 bit; el test de ancho configurable también comprueba 576 px. Informe en `.artifacts/thermal-notes/verificacion-grafica.json`.
- Edge con Playwright y datos ficticios: cambio de casilla con regeneración inmediata, preferencia bloqueada en el original, vista previa, emisión, descarga PDF/PNG, apertura del PDF, alternativa de descarga al compartir, reimpresión con número y bytes idénticos, pantalla móvil de 390 px y teclado. Los PDF descargados también se inspeccionan para comprobar presencia/ausencia del estado. La activación de Web Share se verificó con un simulador de la API; no equivale a probar una aplicación Android física.
- Revisión visual de capturas y documentos, y detector Impeccable sin hallazgos.

Las capturas son `vista-previa-desktop.png`, `nota-emitida-desktop.png`, `configuracion-movil.png` y `vista-previa-movil.png`. Los archivos anteriores se conservaron en `anterior-v1/`; `comparacion-anterior-nuevo.png` muestra ambas versiones con el mismo ancho. El host/script de navegador es temporal, externo al repositorio, en `C:\Users\jose1\AppData\Local\Temp\opencode\thermal-ui`; el verificador PDF está en `C:\Users\jose1\AppData\Local\Temp\opencode\review-thermal-notes.py`. Las pruebas .NET están incluidas en el repositorio.

Archivos de código del rediseño: `IDeliveryNoteService.cs`, `DeliveryNotePaymentProjection.cs` (nuevo), `DeliveryNote.cs`, `DeliveryNoteService.cs`, `ThermalDeliveryNoteDocument.cs`, `DeliveryNoteDialog.razor/.razor.css`, `thermal-notes.js`, el recurso térmico PNG y las pruebas `DeliveryNotePaymentProjectionTests.cs` (nuevo), `DeliveryNotePostgresTests.cs` y `ThermalDeliveryNoteDocumentTests.cs`.

## Ajuste final compacto — v2.1

Se revisó `C:\Users\jose1\Downloads\NE-000003.pdf` como referencia aprobada. El archivo, el recurso del logo, su tamaño de 248 px, la proporción circular, las fuentes y los tamaños de texto se conservan. No cambia la interfaz ni la consulta del estado de pago.

Cambios exclusivamente de presentación:

- Se elimina el nombre del cliente repetido al comienzo de la sucursal. Por ejemplo, `Cliente: AESGYM`, `Sucursal: AESGYM BOLIVAR`, `Dirección: Bolivar` se presenta como `Cliente: AESGYM` y `Sucursal: BOLIVAR`. La comparación usa palabras completas, ignorando mayúsculas y acentos. Una dirección con datos adicionales, como `Calle Bolívar 123`, se conserva. No se modifican los datos almacenados.
- Sin degustación/reposición se imprime únicamente **TOTAL A ENTREGAR**, omitiendo el renglón de unidades de venta. Con adicionales se conserva el desglose.
- Sin envío se imprime directamente **TOTAL**, omitiendo el subtotal repetido y su separador extra. Con envío se conserva el desglose monetario completo.
- Espaciado general de 3 a 2,5 pt; dos renglones de observaciones de 12 a 10 pt; espacios de firma de 23 a 20 pt. Se mantienen dos espacios independientes para firmar y las dos líneas para observaciones. No se acortan nombres de productos, motivos ni observaciones de adicionales.
- Las fechas conservan sus fuentes: fecha de la entrega seleccionada, fecha efectiva solo en resumen registrado y fecha de emisión independiente. No se reemplazan fechas antiguas por la fecha actual ni se inventa una recepción para una salida pendiente.

### Muestras finales y longitud física

Ubicación: `.artifacts/thermal-notes/ajuste-final/`. Cada nombre tiene su archivo `.pdf` y `.png`; son muestras de prueba, no nuevas emisiones comerciales.

| Escenario / archivo base | Longitud PDF | Alto PNG |
|---|---:|---:|
| `normal-sin-adicionales` | 171,4 mm | 1373 px |
| `degustacion-publicidad` | 214,8 mm | 1720 px |
| `reposicion` | 211,6 mm | 1695 px |
| `multiples-adicionales` | 243,9 mm | 1955 px |
| `pendiente-visible` | 180,6 mm | 1447 px |
| `pendiente-oculto` | 171,4 mm | 1373 px |
| `pago-parcial` | 180,6 mm | 1447 px |
| `completamente-pagado` | 171,4 mm | 1373 px |
| `entrega-parcial` | 240,8 mm | 1930 px |

La longitud se mide sobre la página física PDF de 58 mm, incluidos los márgenes. No incluye el avance adicional que aplique la impresora. El PNG tiene ancho útil de 384 px: su longitud depende de conservar la escala configurada de 48 mm útiles en la aplicación Android. La resolución y el ancho útil siguen pendientes de confirmación física.

Comparación con **el mismo contenido** de la muestra normal anterior, incluido su envío: **203,7 → 189,6 mm**, ahorro de **14,1 mm (6,9 %)**. No se compara ese porcentaje con los 171,4 mm del escenario nuevo sin envío. Archivos anteriores conservados en `anterior-v2/`; comparación visual en `ajuste-final/comparacion-longitud.png`.

### Verificación del ajuste final

- Solución Release compilada sin errores ni advertencias.
- **225 pruebas aprobadas: 146 unitarias y 79 de integración, sin omisiones**, con PostgreSQL real aislado. Incluyen reimpresión histórica, pagos posteriores, integridad comercial y financiera, deduplicación de datos sin perder números de calle, las nueve muestras y el resumen histórico.
- Nueve parejas PDF/PNG inspeccionadas visualmente. Verificación adicional con PyMuPDF/Pillow: ancho de 58 mm, longitud física, ausencia de subtotal y cantidades redundantes, texto dentro del área imprimible, total de 12 pt, motivos/observaciones completos y logo con los mismos píxeles, tamaño y proporciones.
- La muestra parcial contiene únicamente **3 venta + 1 reposición + 1 degustación = 5 unidades**, Bs 51,00; excluye las unidades ya entregadas y las de la otra entrega. Muestra `Entrega programada: 24/11/2026` y no inventa una fecha efectiva.
- Verificación histórica adicional: programación 28/10/2026, realización 29/10/2026 y emisión 25/11/2026; **TOTAL ENTREGADO: 7 unidades**. Archivos `verificacion-historica.pdf/.png`.
- La nota pendiente con estado oculto y la nota completamente cubierta generan exactamente la misma imagen que el escenario normal equivalente: sin indicador de pago ni hueco de recuadro. La pendiente visible y el pago parcial muestran únicamente sus indicadores permitidos. No hay referencias fiscales.

Mediciones e informe: `ajuste-final/LONGITUDES.md` y `ajuste-final/longitudes-y-verificacion.json`. Verificador temporal: `C:\Users\jose1\AppData\Local\Temp\opencode\review-compact-notes.py`. Este ajuste modifica la plantilla, su identificación de versión en servicio/entidad documental y las pruebas; no modifica reglas de negocio ni documentos emitidos. No se desplegó ni se ejecutó migración en producción.

## Revisión v2.2 — pendiente único antes y después de confirmar

Referencia visual aprobada: `NE-000004.pdf`. Se conserva el recurso PNG circular original, su escala de 248 px, proporción, tipografía, márgenes, espacios y composición. El único ajuste de la plantilla restringe el recuadro a `PENDIENTE DE PAGO`; se restaura el contacto por configuración, sin rediseñar el pie.

### Resultados reales

- Compilación de la solución Release: sin errores ni advertencias.
- **231 pruebas aprobadas: 147 unitarias y 84 de integración, sin omisiones**. PostgreSQL real aislado; incluye ausencia de cuenta por cobrar, abonos y cobertura completa antes de confirmar, pagos directos antiguos sin aplicaciones, ausencia de doble cómputo, cuenta por cobrar después de confirmar, importes no atribuibles, emisión con selección automática apagada y reimpresión histórica intacta.
- Se compararon snapshots de pedidos, inventario, ventas, pagos y cuentas por cobrar para verificar que vista previa/emisión no modifican datos comerciales ni financieros.
- Edge/Playwright: casilla activada automáticamente antes de confirmar; desactivación conservada después de **dos** actualizaciones; regeneración inmediata al marcar, emisión, original bloqueado, reimpresión con los mismos bytes, PDF/PNG, compartir/fallback, móvil y teclado. Web Share utiliza un simulador de API; no prueba una impresión Android física.
- Ocho parejas PDF/PNG generadas desde el servicio documental con PostgreSQL, no solo desde una plantilla sintética. Las cuatro muestras anteriores a confirmar se generaron **sin filas de Receivables**. Las posteriores usan pedido/entrega completados y cuenta por cobrar de prueba.
- Inspección visual y verificador PDF/Pillow: logo original sin alteraciones, ancho 58 mm, PNG 384 px de 1 bit, total 12 pt, textos dentro del área útil, cantidades/motivos/observaciones completos, fechas según alcance, contacto **78969873**, sin referencias fiscales ni las otras leyendas de pago. El PNG con abono es idéntico al pendiente sin abono; oculto y cubierto son idénticos entre sí y no reservan espacio para el recuadro.
- Detector de interfaz sin hallazgos. No se modificó la arquitectura financiera, no hay migración adicional ni despliegue.

### Archivos finales

Carpeta: `.artifacts/thermal-notes/revision-v2.2/`. Son vistas previas de datos ficticios, no documentos comerciales nuevos.

| Archivo base (.pdf y .png) | Cuenta por cobrar | Indicador | Longitud PDF |
|---|---|---|---:|
| `antes-confirmar-visible` | No existe | Pendiente | 246,7 mm |
| `antes-confirmar-oculto` | No existe | Oculto por elección | 237,4 mm |
| `antes-confirmar-abono` | No existe | Pendiente | 246,7 mm |
| `antes-confirmar-cubierto` | No existe | Omitido por cobertura completa | 237,4 mm |
| `despues-confirmar-visible` | Registrada | Pendiente | 254,8 mm |
| `despues-confirmar-oculto` | Registrada | Oculto por elección | 245,5 mm |
| `despues-confirmar-abono` | Registrada | Pendiente | 254,8 mm |
| `despues-confirmar-cubierto` | Registrada | Omitido por cobertura completa | 245,5 mm |

Todas conservan 15 unidades de venta + 1 reposición + 2 degustaciones, subtotal Bs 255,00 y envío Bs 10,00. Las diferencias de longitud corresponden al recuadro y a los datos de recepción del alcance completado. Las medidas no incluyen avance adicional de la impresora.

Capturas: `vista-previa-desktop.png`, `configuracion-movil.png` y `vista-previa-movil.png`. Informe: `verificacion-pendiente.json`. Verificador temporal externo al repositorio: `C:\Users\jose1\AppData\Local\Temp\opencode\review-pending-only.py`. El script de navegador está en el host temporal ya documentado. Las muestras anteriores siguen disponibles en sus carpetas previas.

## Prueba desde Android

1. Comprobar `ThermalNotes:ContactPhone` (**78969873**) y mantener la copia térmica circular autorizada.
2. Instalar y revisar primero la migración en un entorno de prueba.
3. Abrir un pedido de prueba confirmado y generar/emitir su nota.
4. Pulsar **Compartir PNG** y elegir la aplicación Android que ya se utiliza con la Knup. Si Web Share no admite archivos, el sistema descarga el PNG para abrirlo desde esa aplicación.
5. Seleccionar el papel de 58 mm en la aplicación y comprobar ancho, márgenes, acentos, círculo completo, letras sólidas, total y espacios de firma; evitar que la aplicación vuelva a ajustar una imagen ya preparada al ancho útil. Comparar una nota sin pago, una pendiente y una parcial. Verificar que el recuadro ausente no deja un hueco.
6. Probar también el PDF continuo si esa aplicación lo admite. **Abrir PDF / Imprimir** utiliza el visor del navegador; la disponibilidad del servicio de impresión depende del teléfono.

No se implementó Web Bluetooth, ESC/POS, SPP ni BLE. La impresión física solo podrá declararse validada después de probarla con la impresora real.
