# Guía de carga masiva de personal

Use `Unidad` para la sede operativa del trabajador. `Planta` sigue aceptándose como nombre
de columna por compatibilidad, pero la interfaz muestra únicamente `Unidad`.

Para una unidad ya registrada basta escribir su código o nombre. Para crear una nueva,
complete también `codigo_unidad`. El análisis la mostrará como **Nuevo propuesto** y no la
guardará hasta confirmar.

Si la fila contiene `id_marcador`, identifique el aparato con `equipo`. Para proponer un
aparato nuevo complete `codigo_equipo` y `nombre_equipo`; `serie_equipo` es opcional y debe
dejarse vacía si no se conoce. Nunca use una serie inventada.

El proceso recomendado es:

1. Descargue la plantilla actualizada.
2. Suba el Excel o CSV y seleccione **Analizar plantilla**.
3. Revise unidades, equipos, marcadores, puestos y filas rechazadas.
4. Corrija cualquier estado ambiguo o pendiente.
5. Confirme solo cuando el resumen coincida con el cambio esperado.

Cancelar o limitarse al análisis no deja registros. Una fila rechazada tampoco deja
unidades, equipos o marcadores huérfanos. Las demás filas válidas sí pueden confirmarse.

