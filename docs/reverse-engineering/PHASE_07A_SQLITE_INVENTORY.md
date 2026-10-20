# Fase 7A — Inventario SQLite y artefactos

Fecha: 2026-10-09  
Modo de inspección: SHA-256 + SQLite `immutable=1`, sin escrituras.  
Estado de retiro: **ejecutado después de copia y verificación 76/76, más 5/5 hallazgos
posteriores registrados en addenda**.

Las rutas de las tablas siguientes son las rutas originales preservadas en el manifiesto; los
archivos ya no permanecen en el repositorio operativo.

`Filas personales` suma filas de las tablas personales presentes entre `trabajador`,
`marcacion_cruda` y `asp_net_users`; se usa solo como señal de sensibilidad, no como métrica
funcional. No se extrajo ni documentó ningún dato personal.

## Bases y backups primarios

| Ruta original | Bytes | SHA-256 | Git al inventariar | Ignorado en contención | App | Filas personales | Clasificación |
|---|---:|---|---|---|---|---:|---|
| `artifacts/op02-rot-desc-20261007/ControlMarcacion_ROT_DESC_DOWN.db` | 3751936 | `B1254AD8E7D409E76B0773E0638B2E3349ECEA7AF984400B1CEC9E7B5C1D465E` | No | Sí | Sí | 353 | Ensayo temporal |
| `artifacts/op02-rot-desc-20261007/ControlMarcacion_ROT_DESC_PILOT.db` | 3907584 | `EAE952937546F829028C519CBC79F2F5EA3CEFB3FC3D5E42D2FDA38F0060308B` | No | Sí | Sí | 384 | Ensayo temporal |
| `artifacts/op02b-rot-desc-20261007/backup-pre-migration/ControlMarcacion_OP02B_SOURCE.db` | 3739648 | `3C38C81BCEBBB58C161D55E0CA65A5137EDD3B142C8FE5C1A825EB99A485BB2B` | No | Sí | Sí | 355 | Backup/evidencia |
| `artifacts/op02b-rot-desc-20261007/ControlMarcacion_OP02B_SOURCE_ROT_DESC_OP02B.db` | 3747840 | `01D4E164CD4A7BB0127355A8FEA761016EA77F485B605A5F6C9358117E19C982` | No | Sí | Sí | 355 | Backup/evidencia |
| `artifacts/op02b-rot-desc-20261007/ControlMarcacion_OP02B_SOURCE.db` | 3739648 | `3C38C81BCEBBB58C161D55E0CA65A5137EDD3B142C8FE5C1A825EB99A485BB2B` | No | Sí | Sí | 355 | Backup/evidencia |
| `artifacts/op02b-rot-desc-20261007/restore-drill/ControlMarcacion_OP02B_RESTORE.db` | 3739648 | `3C38C81BCEBBB58C161D55E0CA65A5137EDD3B142C8FE5C1A825EB99A485BB2B` | No | Sí | Sí | 355 | Backup/evidencia |
| `artifacts/op03a-rot-desc-20261009/backup-storage/restore-backup.db` | 8192 | `85406D0B2C3DEA9CB98EFFE97AD57FC308138B4520CA9293B280FE67B039DE74` | No | Sí | No | 0 | Sintético de restore |
| `artifacts/op03a-rot-desc-20261009/backup-storage/restore-restored.db` | 8192 | `85406D0B2C3DEA9CB98EFFE97AD57FC308138B4520CA9293B280FE67B039DE74` | No | Sí | No | 0 | Sintético de restore |
| `artifacts/op03a-rot-desc-20261009/backup-storage/restore-source.db` | 8192 | `9CD81F7BA292CF3874F04E44BA50A6ED233673AF846C14F30192F126B04B10FE` | No | Sí | No | 0 | Sintético de restore |
| `artifacts/r-web-rot-desc-20261007/canonical_accidental_migrated.db` | 3747840 | `072FFAF40BF69BF88251A35A360D0905F545D6D1C4EEEF11439A2F804F3F3020` | No | Sí | Sí | 353 | Backup/evidencia |
| `artifacts/r-web-rot-desc-20261007/canonical_restore_candidate.db` | 3751936 | `642550DA9E8697A607391ED5D40E576710FED60AE191DD579202A03FBAB9F928` | No | Sí | Sí | 353 | Backup/evidencia |
| `artifacts/r-web-rot-desc-20261007/ControlMarcacion_RWEB_BASELINE.db` | 3747840 | `F33F9FF340AFA11A4AC6E23D9B7D200A17C9A5812DE1A2C00A8FEDDE460D4084` | No | Sí | Sí | 355 | Backup/evidencia |
| `artifacts/r-web-rot-desc-20261007/ControlMarcacion_RWEB_SMOKE_FINAL.db` | 3747840 | `7B219003A84B9BDCDC92A1E979BA53FE98357774F113A0302E02AC78EA035028` | No | Sí | Sí | 355 | Ensayo temporal |
| `despliegue/respaldos/antes_sim2_20260910_092716.db` | 4096 | `52A371445CE0812CA930AEA418E7D7E9D6459F1778A6E14CB56592F08F5A08AF` | No | Sí | No | 0 | Backup vacío/incompleto |
| `despliegue/respaldos/antes_simulacion_20260910_091753.db` | 602112 | `EC1A2760CE0CB5A51F54407A8021A069C656350220854E4CE7C4F38077A4B023` | No | Sí | Sí | 65 | Backup/evidencia |
| `despliegue/respaldos/antes-atribucion-al-calcular-20260921-1448/ControlMarcacion.db` | 688128 | `5A3D4F08AEC64CE927783D35180DBBDD4E2FC09AAD056459AB13D01F0FDC774B` | No | Sí | Sí | 39 | Backup/evidencia |
| `despliegue/respaldos/antes-borrar-operativos-20260923-0933/ControlMarcacion.db` | 2183168 | `3F062AC3F41597F8CDB389CA182F0B836F55417CC997D6431564AB2BD1D8430E` | No | Sí | Sí | 332 | Backup/evidencia |
| `despliegue/respaldos/antes-de-limpieza-20260910-151636/ControlMarcacion.db` | 729088 | `A36692C32977A79550556CE55A289BADED80FE54DACAAFE47F415D01E766BABB` | No | Sí | Sí | 121 | Backup/evidencia |
| `despliegue/respaldos/antes-de-limpieza-20260910-151636/produccion-ControlMarcacion.db` | 573440 | `497F673FCEF86092A04B2A6206CD23CDC77FF787D53C787B9F1725B1D00861B9` | No | Sí | Sí | 33 | Backup rotulado producción; canonicidad no confirmada |
| `despliegue/respaldos/antes-padrones-18set-20260921/ControlMarcacion.db` | 622592 | `4E72FE32182F33F1037E8DE3B6F198D1F59A119DBD8FDB7AE09A31185CA3A35D` | No | Sí | Sí | 2 | Backup/evidencia |
| `despliegue/respaldos/antes-puestos-generales-20260921/ControlMarcacion.db` | 622592 | `47592CBFEF5534C854BDB1161DA043E828BC5E9E3DD578EEC0DD5B0B2624CD60` | No | Sí | Sí | 2 | Backup/evidencia |
| `despliegue/respaldos/ControlMarcacion_antes_de_limpiar_20260901_150046.db` | 528384 | `430DC9FC82389681552D57CF13B4630A332BC805B3008ACC90DEF12F46B1836A` | No | Sí | Sí | 7 | Backup/evidencia |
| `despliegue/respaldos/dev-con-simulacion.db` | 729088 | `F05815779C596DF28D40491B96D92B323FAB4E02AEB3ED56102170E8D78BAA64` | No | Sí | Sí | 34 | Desarrollo con simulación |
| `despliegue/respaldos/dev-limpia-referencia.db` | 729088 | `C37687089402161C58019C15FD5BF75DFE618C793D5CE09EBEC3FAE0F0BD1763` | No | Sí | Sí | 1 | Referencia de desarrollo |
| `src/ControlMarcacion.Web/ControlMarcacion_backup_antes_pmv.db` | 729088 | `FC6EDAA64BC9AB276771D2B71481F94C4566AC137BBF1F38A68A6F4DE67ADABA` | No | Sí desde 7A | Sí | 1 | Backup local; no estaba ignorado al inicio |
| `src/ControlMarcacion.Web/ControlMarcacion_pre_limpieza_20260826_085933.db.bak` | 630784 | `51EF14EC86AC59B55FA3E64941D544EEDD7ED14C95671BF2A67F4CAF941DD390` | **Sí** | No mientras siga rastreado | Sí | 17 | Backup histórico versionado |
| `src/ControlMarcacion.Web/ControlMarcacion.db` | 3751936 | `642550DA9E8697A607391ED5D40E576710FED60AE191DD579202A03FBAB9F928` | **Sí** | No mientras siga rastreado | Sí | 353 | Base local del árbol; no productiva confirmada |

## Sidecars

Se inventariaron 48 archivos `*.db-wal`/`*.db-shm`. Dos pertenecientes a
`src/ControlMarcacion.Web/ControlMarcacion.db` estaban versionados. Todos se copiaron y
verificaron junto con su base antes de retirar el original. Ruta y SHA-256 están en los
manifiestos de cuarentena.

Una revisión posterior corrigió dos huecos del inventario por extensión:

- dos sidecars regenerados cuyo nombre terminaba en `.db.bak-shm/.db.bak-wal`;
- la base SQLite sin extensión `release/path-drift-evidence/CONTROL` y sus dos sidecars.

Los cinco archivos adicionales fueron copiados, verificados y retirados. Los manifiestos
addendum preservan ruta, tamaño, clasificación y hashes.

## ZIP

| Ruta | Bytes | SHA-256 | Clasificación | Resultado |
|---|---:|---|---|---|
| `artifacts/op03a-rot-desc-20261009/release/ControlMarcacion_ROT_DESC_20261009_op03a01.zip` | 51894099 | `29B8F78BD7041A6C82F649799DD034EBBE29352447DC98E3C7712532DCA4C1DE` | Release no desplegado | Sin DB; no coincide con la credencial expuesta |
| `despliegue/ControlMarcaciones.zip` | 51533162 | `10120A42A30F5716E5F5EFFA8B580D14A9451A1D61D2B50E1DB697883A1AE994` | Release histórico ignorado | **Dos appsettings contienen la credencial expuesta; retiro pendiente** |

Los ZIP de 22 bytes bajo `bin/**/EmptyFiles` son fixtures vacíos de una dependencia de test y
se regeneran con el build; no contienen configuración ni datos de la aplicación.

## Cuarentena ejecutada

```text
C:\Users\stafur\Documents\ControlMarcacion-Private-Quarantine\phase7a-20261009
```

Conjunto propuesto:

```text
75 archivos SQLite/sidecar/backup
+ despliegue\ControlMarcaciones.zip
= 76 archivos
```

Resultado:

```text
copiados = 76
hashes coincidentes = 76
originales retirados = 76
bloqueados = 0
sensibles restantes en working tree = 0

hallazgos posteriores copiados = 5
hashes posteriores coincidentes = 5
originales posteriores retirados = 5
```

Manifiestos:

```text
C:\Users\stafur\Documents\ControlMarcacion-Private-Quarantine\phase7a-20261009\manifest.csv
C:\Users\stafur\Documents\ControlMarcacion-Private-Quarantine\phase7a-20261009\manifest.json
C:\Users\stafur\Documents\ControlMarcacion-Private-Quarantine\phase7a-20261009\manifest-addendum-regenerated-sidecars-20261009.csv
C:\Users\stafur\Documents\ControlMarcacion-Private-Quarantine\phase7a-20261009\manifest-addendum-regenerated-sidecars-20261009.json
C:\Users\stafur\Documents\ControlMarcacion-Private-Quarantine\phase7a-20261009\manifest-addendum-path-drift-evidence-20261009.csv
C:\Users\stafur\Documents\ControlMarcacion-Private-Quarantine\phase7a-20261009\manifest-addendum-path-drift-evidence-20261009.json
```

El ZIP se conserva con el nombre
`HISTORICO_COMPROMETIDO_NO_UTILIZAR_PARA_DESPLIEGUE__ControlMarcaciones.zip`.
