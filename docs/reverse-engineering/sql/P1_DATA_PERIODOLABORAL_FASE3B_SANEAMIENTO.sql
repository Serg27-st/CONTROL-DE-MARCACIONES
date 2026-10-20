-- P1-DATA-PERIODOLABORAL / Fase 3B / AUTO_FIX
-- Aplicar SOLO sobre una copia verificada y despues de ejecutar el dry-run.
-- MANUAL_REVIEW queda expresamente fuera de este script.
-- Idempotente: una segunda ejecucion valida guardas y elimina cero filas.

.bail on
PRAGMA foreign_keys=ON;
BEGIN IMMEDIATE;

CREATE TEMP TABLE p1_3b_guard (
  descripcion TEXT NOT NULL,
  ok INTEGER NOT NULL CHECK (ok=1)
);

-- No continuar con fechas imposibles.
INSERT INTO p1_3b_guard
SELECT 'sin fechas invalidas', CASE WHEN COUNT(*)=0 THEN 1 ELSE 0 END
FROM periodo_laboral
WHERE fecha_cese IS NOT NULL AND fecha_cese<fecha_ingreso;

-- La auditoria textual debe conservar una evidencia de insercion por cada fila
-- redundante. No se borra registro_auditoria.
WITH ranked AS (
  SELECT p.id,
         ROW_NUMBER() OVER (
           PARTITION BY trabajador_id,empresa_id,fecha_ingreso,
                        COALESCE(fecha_cese,''),COALESCE(motivo_cese,''),
                        COALESCE(TRIM(observacion_cese),''),activo
           ORDER BY creado_en,id) rn
  FROM periodo_laboral p
)
INSERT INTO p1_3b_guard
SELECT 'auditoria unica para cada redundante',
       CASE WHEN COUNT(*)=0 THEN 1 ELSE 0 END
FROM ranked r
WHERE r.rn>1 AND (
  SELECT COUNT(*) FROM registro_auditoria a
  WHERE a.tabla='periodo_laboral'
    AND a.clave_registro=CAST(r.id AS TEXT)
    AND a.operacion='Insercion')<>1;

-- Ninguna FK operacional puede apuntar a los IDs que se retiraran. La tabla
-- de auditoria es evidencia textual y no declara FK.
WITH refs AS (
  SELECT m.name tabla_hija, fk."from" columna_hija
  FROM sqlite_master m, pragma_foreign_key_list(m.name) fk
  WHERE m.type='table' AND fk."table"='periodo_laboral'
), ranked AS (
  SELECT p.id,
         ROW_NUMBER() OVER (
           PARTITION BY trabajador_id,empresa_id,fecha_ingreso,
                        COALESCE(fecha_cese,''),COALESCE(motivo_cese,''),
                        COALESCE(TRIM(observacion_cese),''),activo
           ORDER BY creado_en,id) rn
  FROM periodo_laboral p
)
INSERT INTO p1_3b_guard
SELECT 'esquema sin FK operacional inesperada', CASE WHEN COUNT(*)=0 THEN 1 ELSE 0 END
FROM refs;

-- Tras retirar exclusivamente copias semanticas exactas no puede quedar ningun
-- solapamiento. Si aparece, es un caso humano y se aborta toda la transaccion.
WITH ranked AS (
  SELECT p.*,
         ROW_NUMBER() OVER (
           PARTITION BY trabajador_id,empresa_id,fecha_ingreso,
                        COALESCE(fecha_cese,''),COALESCE(motivo_cese,''),
                        COALESCE(TRIM(observacion_cese),''),activo
           ORDER BY creado_en,id) rn
  FROM periodo_laboral p
), canon AS (SELECT * FROM ranked WHERE rn=1)
INSERT INTO p1_3b_guard
SELECT 'sin solapamientos materiales', CASE WHEN COUNT(*)=0 THEN 1 ELSE 0 END
FROM canon a JOIN canon b
  ON b.trabajador_id=a.trabajador_id AND b.id>a.id
WHERE a.fecha_ingreso<=COALESCE(b.fecha_cese,'9999-12-31')
  AND b.fecha_ingreso<=COALESCE(a.fecha_cese,'9999-12-31');

-- Sobrevive la fila original: menor creado_en y, como desempate estable, menor id.
WITH ranked AS (
  SELECT p.id,
         ROW_NUMBER() OVER (
           PARTITION BY trabajador_id,empresa_id,fecha_ingreso,
                        COALESCE(fecha_cese,''),COALESCE(motivo_cese,''),
                        COALESCE(TRIM(observacion_cese),''),activo
           ORDER BY creado_en,id) rn
  FROM periodo_laboral p
)
DELETE FROM periodo_laboral WHERE id IN (SELECT id FROM ranked WHERE rn>1);

COMMIT;

SELECT changes() AS filas_eliminadas_ultima_sentencia;

