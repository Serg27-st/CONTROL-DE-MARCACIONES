# ADR 0001 — La marcación se atribuye al calcular, no al cargar

- **Estado:** aceptada
- **Fecha:** 21/09/2026
- **Migración:** `AtribucionDeMarcacionesAlCalcular`

## Contexto

Hasta esta decisión, la ingesta resolvía cada línea del archivo del marcador —aparato,
número, hora— al trabajador que tenía ese número asignado en esa fecha, y **guardaba el
resultado** en `marcacion_cruda.trabajador_id`. Las líneas de un número sin dueño ese día no
se guardaban: se reportaban como «ID sin asignar» y se descartaban.

Con el padrón real eso se volvió un problema diario:

- El personal se da de alta antes de conocer su número en el aparato (los padrones de planta
  no lo traen; se consigue después de la lista de usuarios del equipo). Toda marcación
  cargada entretanto se perdía, y la única salida era **volver a subir los archivos** después
  de asignar los números.
- Un número asignado **mal** dejaba sus marcaciones grabadas a nombre de otra persona. Como la
  marcación cruda es inmutable, **no había corrección posible**: corregir la asignación no
  movía nada de lo ya cargado.

El dueño de una marcación no es un dato del aparato: es un dato del padrón, y el padrón se
corrige. Guardarlo en la fila inmutable mezclaba las dos cosas.

## Decisión

1. `MarcacionCruda` guarda solo lo que dijo el aparato: **equipo, número y hora** (más el
   archivo de origen). La columna `trabajador_id` desaparece.
2. **Toda** marcación del archivo se guarda, tenga o no dueño en ese momento.
3. El dueño se decide **al calcular el tareo**, con `AtribucionMarcaciones` (función pura en
   `Domain`): la marcación es de quien tenía ese número **en ese aparato en la fecha de la
   marcación**. Es la única regla de atribución; la usan el motor, el aviso de la ingesta y la
   lista de números sin asignar, para que no puedan contradecirse.
4. Asignar o corregir un número en la ficha y **recalcular** basta para que las marcaciones
   pasen a la persona correcta. Por eso `EstadoCalculoTareo` cuenta un cambio de asignación de
   número como algo que deja el tareo desactualizado.
5. La ficha del trabajador admite una fecha «rige desde» para el número: igual o anterior al
   inicio del vigente es una **corrección en sitio** (auditada por el interceptor, que guarda
   el valor anterior); posterior es un **cambio** que cierra el anterior el día previo.

## Invariantes que se mantienen

- **Marcaciones inmutables (1):** reforzado. La fila ya no lleva un dato derivado que pudiera
  quedar equivocado.
- **Tareo reconstruible (2):** reforzado. Recalcular con el padrón corregido da el tareo
  correcto sin tocar marcaciones.
- **Periodo cerrado (3):** sin cambios. Un periodo cerrado no se recalcula, así que corregir un
  número no altera tareos ya cerrados; si se reabre, el recálculo usa el padrón vigente.
- **ID con vigencia por fecha (5):** la atribución usa la asignación vigente **en la fecha de la
  marcación**, nunca la de hoy.

## Consecuencias

- La deduplicación de la ingesta y el colapso de doble fichada operan por
  **(equipo, número)**, no por trabajador. El índice único `(equipo, número, instante)` ya era
  la clave real.
- El calendario de cobertura cuenta personas como números distintos por aparato.
- Aparece la lista viva de **números sin asignar** en Marcaciones: marcaron, están guardados y
  esperan a que alguien tenga ese número.
- El motor consulta las marcaciones por `(equipo, número, rango)` sobre el índice único, en
  lugar de por `(trabajador, rango)`.
- Una marcación de un número que nunca se asigna (el administrador del aparato, por ejemplo)
  queda guardada y no entra a ningún tareo, igual que antes.
