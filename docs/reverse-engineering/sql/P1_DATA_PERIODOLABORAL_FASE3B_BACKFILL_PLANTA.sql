-- P1-DATA-PERIODOLABORAL / Fase 3B / BACKFILL NIVEL A
-- Requiere que la migracion 20261012151429 ya haya creado asignacion_planta.
-- Solo usa evidencia temporal explicita; Trabajador.PlantaId no decide el valor.

.bail on
PRAGMA foreign_keys=ON;
BEGIN IMMEDIATE;

CREATE TEMP TABLE p1_3b_guard_planta (
  descripcion TEXT NOT NULL,
  ok INTEGER NOT NULL CHECK (ok=1)
);

-- Se bloquea si un periodo tiene mas de una planta explicita que cubra todo el
-- vinculo. Esa ambiguedad debe pasar a MANUAL_REVIEW.
WITH evidencia AS (
  SELECT p.id periodo_id,a.planta_id
  FROM periodo_laboral p
  JOIN asignacion_area aa ON aa.trabajador_id=p.trabajador_id
  JOIN area a ON a.id=aa.area_id
  WHERE aa.fecha_inicio<=p.fecha_ingreso
    AND (p.fecha_cese IS NULL AND aa.fecha_fin IS NULL
         OR p.fecha_cese IS NOT NULL AND COALESCE(aa.fecha_fin,'9999-12-31')>=p.fecha_cese)
  UNION ALL
  SELECT p.id,em.planta_id
  FROM periodo_laboral p
  JOIN asignacion_marcador am ON am.trabajador_id=p.trabajador_id
  JOIN equipo_marcador em ON em.id=am.equipo_id
  WHERE am.fecha_inicio<=p.fecha_ingreso
    AND (p.fecha_cese IS NULL AND am.fecha_fin IS NULL
         OR p.fecha_cese IS NOT NULL AND COALESCE(am.fecha_fin,'9999-12-31')>=p.fecha_cese)
), resumen AS (
  SELECT p.id,COUNT(DISTINCT e.planta_id) plantas
  FROM periodo_laboral p LEFT JOIN evidencia e ON e.periodo_id=p.id
  GROUP BY p.id
)
INSERT INTO p1_3b_guard_planta
SELECT 'todos los periodos tienen evidencia A no ambigua',
       CASE WHEN COUNT(*)=0 THEN 1 ELSE 0 END
FROM resumen WHERE plantas<>1;

-- Idempotencia: solo crea la asignacion ausente. Sus limites son exactamente
-- los del periodo, por lo que la contencion temporal queda satisfecha.
WITH evidencia AS (
  SELECT p.id periodo_id,a.planta_id
  FROM periodo_laboral p
  JOIN asignacion_area aa ON aa.trabajador_id=p.trabajador_id
  JOIN area a ON a.id=aa.area_id
  WHERE aa.fecha_inicio<=p.fecha_ingreso
    AND (p.fecha_cese IS NULL AND aa.fecha_fin IS NULL
         OR p.fecha_cese IS NOT NULL AND COALESCE(aa.fecha_fin,'9999-12-31')>=p.fecha_cese)
  UNION ALL
  SELECT p.id,em.planta_id
  FROM periodo_laboral p
  JOIN asignacion_marcador am ON am.trabajador_id=p.trabajador_id
  JOIN equipo_marcador em ON em.id=am.equipo_id
  WHERE am.fecha_inicio<=p.fecha_ingreso
    AND (p.fecha_cese IS NULL AND am.fecha_fin IS NULL
         OR p.fecha_cese IS NOT NULL AND COALESCE(am.fecha_fin,'9999-12-31')>=p.fecha_cese)
), nivel_a AS (
  SELECT periodo_id,MIN(planta_id) planta_id
  FROM evidencia GROUP BY periodo_id HAVING COUNT(DISTINCT planta_id)=1
)
INSERT INTO asignacion_planta
  (periodo_laboral_id,planta_id,fecha_inicio,fecha_fin,creado_en,creado_por,
   modificado_en,modificado_por,activo)
SELECT p.id,e.planta_id,p.fecha_ingreso,p.fecha_cese,
       strftime('%Y-%m-%d %H:%M:%f','now'),'P1-DATA-PERIODOLABORAL-3B',
       NULL,NULL,1
FROM periodo_laboral p JOIN nivel_a e ON e.periodo_id=p.id
WHERE NOT EXISTS (
  SELECT 1 FROM asignacion_planta ap WHERE ap.periodo_laboral_id=p.id);

COMMIT;

SELECT changes() AS asignaciones_insertadas_ultima_sentencia;

