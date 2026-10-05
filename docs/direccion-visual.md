# Dirección visual del sitio público

## Sujeto, audiencia y objetivo

- **Sujeto:** productos horneados de Tentación Sana, presentados como una opción deliciosa, cuidada y premium.
- **Audiencia inicial:** personas de Bolivia que descubren la marca desde el celular y quieren conocer productos o consultar disponibilidad por WhatsApp.
- **Objetivo principal:** llevar al visitante desde la propuesta de valor hasta el catálogo o una consulta, sin simular una compra confirmada.

## Sistema visual

### Color

| Token | Valor | Uso |
|---|---:|---|
| Cacao profundo | `#16140F` | Fondo principal y contraste del logotipo |
| Avena clara | `#F3EBD8` | Superficies de lectura |
| Hoja viva | `#78E000` | Acción primaria y señales de marca |
| Pistacho suave | `#C8F59A` | Énfasis secundario y estados suaves |
| Frambuesa | `#C84963` | Acento puntual para sabor y calidez |
| Tinta | `#25221B` | Texto sobre superficies claras |

El verde proviene del activo oficial. Se evita convertir toda la página en negro con verde; las superficies claras aportan una sensación de producto horneado y mantienen la marca legible.

### Tipografía

- **Titulares:** `Bahnschrift Condensed`, con respaldo `Arial Narrow`, usada en mayúsculas cortas y grandes.
- **Texto:** `Aptos`, con respaldo `Segoe UI` y fuentes sans-serif del sistema.
- **Datos y etiquetas:** `Consolas`, con respaldo monoespaciado, solo para cantidades, etiquetas breves y metadatos.

Antes del lanzamiento se evaluará incorporar archivos web de una familia tipográfica con licencia adecuada para garantizar la misma apariencia en todos los dispositivos.

### Layout

La portada alterna una apertura oscura con secciones claras y amplias. En móvil, propuesta, producto y llamada a la acción aparecen antes del primer desplazamiento largo. En escritorio, el hero usa una composición asimétrica 7/5.

```text
Móvil                          Escritorio
┌────────────────────┐        ┌────────────────────────────────┐
│ Logo        Menú   │        │ Logo     Catálogo Contacto Login│
├────────────────────┤        ├───────────────────┬────────────┤
│ Etiqueta           │        │ Propuesta         │ Corte      │
│ Propuesta          │        │ Texto + acciones  │ diagonal   │
│ Texto              │        │                   │ de marca   │
│ [Ver catálogo]     │        ├───────────────────┴────────────┤
│ [WhatsApp]         │        │ Beneficios verificables        │
├────────────────────┤        ├────────────────────────────────┤
│ Beneficios         │        │ Productos destacados           │
├────────────────────┤        └────────────────────────────────┘
│ Productos          │
└────────────────────┘
```

### Elemento distintivo

El corte diagonal es el gesto propio de la interfaz. Evoca simultáneamente la inclinación del logotipo y una rebanada de budín. Se usa una sola vez con protagonismo en el hero y de forma discreta en separadores; no se repite como adorno en todas las tarjetas.

## Revisión contra el brief

- El fondo oscuro está justificado por el logotipo oficial, no por una tendencia visual genérica.
- El verde ácido se limita a acciones y señales; la avena y frambuesa evitan una estética tecnológica.
- No se usarán contadores, urgencia falsa, calificaciones inventadas ni beneficios de salud sin aprobación.
- La página no utilizará fotografías genéricas como si fueran productos reales.
- El movimiento se limitará a una entrada breve del hero y estados de interacción, respetando `prefers-reduced-motion`.
