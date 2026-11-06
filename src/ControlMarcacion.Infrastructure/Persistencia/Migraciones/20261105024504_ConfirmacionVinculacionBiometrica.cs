using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones;

public partial class ConfirmacionVinculacionBiometrica : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE "conciliacion_biometrica" (
                "id" INTEGER NOT NULL CONSTRAINT "pk_conciliacion_biometrica" PRIMARY KEY AUTOINCREMENT,
                "identificador_publico" TEXT NOT NULL,
                "equipo_id" INTEGER NOT NULL,
                "planta_id" INTEGER NOT NULL,
                "version" INTEGER NOT NULL,
                "huella_estado" TEXT NOT NULL,
                "expira_en" TEXT NOT NULL,
                "estado" TEXT NOT NULL,
                "creado_en" TEXT NOT NULL,
                "creado_por" TEXT NULL,
                "modificado_en" TEXT NULL,
                "modificado_por" TEXT NULL,
                "activo" INTEGER NOT NULL,
                CONSTRAINT "fk_conciliacion_biometrica_equipo_marcador_equipo_id"
                    FOREIGN KEY ("equipo_id") REFERENCES "equipo_marcador" ("id") ON DELETE RESTRICT,
                CONSTRAINT "fk_conciliacion_biometrica_planta_planta_id"
                    FOREIGN KEY ("planta_id") REFERENCES "planta" ("id") ON DELETE RESTRICT
            );
            CREATE INDEX "ix_conciliacion_biometrica_equipo_id_expira_en"
                ON "conciliacion_biometrica" ("equipo_id", "expira_en");
            CREATE UNIQUE INDEX "ix_conciliacion_biometrica_identificador_publico"
                ON "conciliacion_biometrica" ("identificador_publico");
            CREATE INDEX "ix_conciliacion_biometrica_planta_id"
                ON "conciliacion_biometrica" ("planta_id");

            CREATE TABLE "decision_vinculacion_biometrica" (
                "id" INTEGER NOT NULL CONSTRAINT "pk_decision_vinculacion_biometrica" PRIMARY KEY AUTOINCREMENT,
                "conciliacion_id" INTEGER NOT NULL,
                "indice_registro" INTEGER NOT NULL,
                "uid_registro" INTEGER NULL,
                "trabajador_id" INTEGER NULL,
                "equipo_id" INTEGER NOT NULL,
                "pin" TEXT NOT NULL,
                "fecha_inicio" TEXT NOT NULL,
                "fecha_fin" TEXT NULL,
                "categoria_evidencia" TEXT NOT NULL,
                "referencia_evidencia" TEXT NOT NULL,
                "huella_evidencia" TEXT NOT NULL,
                "evidencia_verificada_en" TEXT NOT NULL,
                "evidencia_expira_en" TEXT NULL,
                "huella_estado" TEXT NOT NULL,
                "clave_idempotencia" TEXT NOT NULL,
                "resultado" TEXT NOT NULL,
                "motivo_resultado" TEXT NULL,
                "confirmada_por" TEXT NULL,
                "confirmada_en" TEXT NULL,
                "asignacion_marcador_id" INTEGER NULL,
                "creado_en" TEXT NOT NULL,
                "creado_por" TEXT NULL,
                "modificado_en" TEXT NULL,
                "modificado_por" TEXT NULL,
                "activo" INTEGER NOT NULL,
                CONSTRAINT "CK_decision_vigencia"
                    CHECK ([fecha_fin] IS NULL OR [fecha_fin] >= [fecha_inicio]),
                CONSTRAINT "fk_decision_vinculacion_biometrica_asignacion_marcador_asignacion_marcador_id"
                    FOREIGN KEY ("asignacion_marcador_id") REFERENCES "asignacion_marcador" ("id") ON DELETE RESTRICT,
                CONSTRAINT "fk_decision_vinculacion_biometrica_conciliacion_biometrica_conciliacion_id"
                    FOREIGN KEY ("conciliacion_id") REFERENCES "conciliacion_biometrica" ("id") ON DELETE RESTRICT,
                CONSTRAINT "fk_decision_vinculacion_biometrica_equipo_marcador_equipo_id"
                    FOREIGN KEY ("equipo_id") REFERENCES "equipo_marcador" ("id") ON DELETE RESTRICT,
                CONSTRAINT "fk_decision_vinculacion_biometrica_trabajador_trabajador_id"
                    FOREIGN KEY ("trabajador_id") REFERENCES "trabajador" ("id") ON DELETE RESTRICT
            );
            CREATE INDEX "ix_decision_vinculacion_biometrica_asignacion_marcador_id"
                ON "decision_vinculacion_biometrica" ("asignacion_marcador_id");
            CREATE UNIQUE INDEX "ix_decision_vinculacion_biometrica_clave_idempotencia"
                ON "decision_vinculacion_biometrica" ("clave_idempotencia");
            CREATE UNIQUE INDEX "ix_decision_vinculacion_biometrica_conciliacion_id_indice_registro"
                ON "decision_vinculacion_biometrica" ("conciliacion_id", "indice_registro");
            CREATE INDEX "ix_decision_vinculacion_biometrica_equipo_id"
                ON "decision_vinculacion_biometrica" ("equipo_id");
            CREATE INDEX "ix_decision_vinculacion_biometrica_trabajador_id"
                ON "decision_vinculacion_biometrica" ("trabajador_id");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "decision_vinculacion_biometrica");
        migrationBuilder.DropTable(name: "conciliacion_biometrica");
    }
}
