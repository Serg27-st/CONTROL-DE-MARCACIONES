# AGENTS.md — Control de Marcaciones

## Eficiencia

1. Limita la exploración al módulo solicitado. Localiza hasta 5 archivos relevantes inicialmente y amplía solo cuando sea necesario.
2. Lee fragmentos específicos; evita recorrer archivos, documentos históricos o carpetas completas sin justificación.
3. Reutiliza servicios, pruebas y evidencia existentes. No repitas auditorías completadas.
4. Ejecuta pruebas dirigidas primero y la suite completa una sola vez tras cambios productivos, salvo necesidad justificada.
5. Evita documentación redundante, archivos temporales innecesarios y evidencias duplicadas.
6. Guarda resultados detallados en `artifacts/`. Responde con máximo 150 palabras.
7. Si detectas un fallo, investiga primero el componente implicado; amplía el alcance únicamente si la causa lo requiere.

## Integridad y seguridad

8. Preserva las reglas históricas de asistencia, las vigencias, la seguridad por unidad y el aislamiento PMV.
9. No modifiques producción, ejecutes OP-03, despliegues ni hagas commits sin autorización explícita.
10. No alteres código productivo durante una fase exclusivamente de auditoría o QA.
11. No declares PASS sin evidencia ejecutada. Registra los bloqueos y las limitaciones reales.

## Contexto

Consulta `docs/AI_CHECKPOINT.md` para conocer el último estado del proyecto, si existe.

Estas reglas priorizan eficiencia sin sacrificar seguridad, corrección ni cobertura necesaria.
