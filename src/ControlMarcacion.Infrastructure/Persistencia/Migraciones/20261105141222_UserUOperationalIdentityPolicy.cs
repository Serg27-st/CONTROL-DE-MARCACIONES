using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class UserUOperationalIdentityPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "base_confirmacion",
                table: "decision_vinculacion_biometrica",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "Ninguna");

            migrationBuilder.AddColumn<string>(
                name: "diagnosticos",
                table: "decision_vinculacion_biometrica",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "naturaleza_vigencia",
                table: "decision_vinculacion_biometrica",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "NoInformada");

            migrationBuilder.AddColumn<string>(
                name: "formato_respaldo",
                table: "conciliacion_biometrica",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "huella_respaldo",
                table: "conciliacion_biometrica",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "respaldo_leido_en_utc",
                table: "conciliacion_biometrica",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "tamano_respaldo",
                table: "conciliacion_biometrica",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "version_politica_identidad",
                table: "conciliacion_biometrica",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "decision_vinculacion_biometrica" DROP COLUMN "base_confirmacion";
                ALTER TABLE "decision_vinculacion_biometrica" DROP COLUMN "diagnosticos";
                ALTER TABLE "decision_vinculacion_biometrica" DROP COLUMN "naturaleza_vigencia";
                ALTER TABLE "conciliacion_biometrica" DROP COLUMN "formato_respaldo";
                ALTER TABLE "conciliacion_biometrica" DROP COLUMN "huella_respaldo";
                ALTER TABLE "conciliacion_biometrica" DROP COLUMN "respaldo_leido_en_utc";
                ALTER TABLE "conciliacion_biometrica" DROP COLUMN "tamano_respaldo";
                ALTER TABLE "conciliacion_biometrica" DROP COLUMN "version_politica_identidad";
                """);
        }
    }
}
