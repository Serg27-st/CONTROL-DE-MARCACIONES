# Guía de importación de turnos y descansos

Use datos autorizados. La aplicación no deduce horarios desde marcaciones. Ingrese a
**Programación laboral → Importar turnos y descansos** y descargue la plantilla.

## Identificación y fechas

Complete tipo y número de documento, empresa y unidad. El documento es texto y conserva ceros
iniciales. Use una fecha específica o un intervalo completo; no mezcle ambas modalidades.
El intervalo se expande y aparece día por día antes de confirmar.

## Trabajo

Seleccione `TRABAJO` y declare el código del turno. Si existe, las horas informadas deben
coincidir. Para proponer uno nuevo son obligatorios nombre, entrada, salida y refrigerio.
Marque `Salida al día siguiente = SI` cuando corresponda. La jornada pertenece a su fecha de
inicio.

## Descanso

Seleccione `DESCANSO` y deje vacíos código, nombre, horas y refrigerio. Un domingo no se
convierte automáticamente en descanso y una fecha sin marcaciones tampoco.

## Previsualización

- `Existente`: definición o programación compatible.
- `NuevoPropuesto`: turno completo que se creará al confirmar.
- `Descanso`: descanso explícito que se programará.
- `Incompleto`: faltan datos o el trabajador no fue identificado.
- `Conflicto`: contradicción, solapamiento, periodo cerrado o falta de autorización.

Cancelar no guarda datos. La confirmación vuelve a validar el archivo y la base. Una
reimportación idéntica no duplica programaciones. Al finalizar, consulte **Preparación para
tareo**: un turno importado no compensa otras dependencias faltantes.
