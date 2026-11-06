using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class NormalizacionTurnosDiuNoc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 1L,
                column: "hora_fin",
                value: new TimeOnly(17, 22, 0));

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 2L,
                column: "hora_fin",
                value: new TimeOnly(5, 22, 0));

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 3L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 4L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 5L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "turno_planta",
                keyColumn: "id",
                keyValue: 1L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 1L,
                column: "hora_fin",
                value: new TimeOnly(17, 22, 0));

            migrationBuilder.UpdateData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 2L,
                column: "hora_fin",
                value: new TimeOnly(5, 22, 0));

            migrationBuilder.UpdateData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 3L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 4L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 5L,
                column: "activo",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 1L,
                column: "hora_fin",
                value: new TimeOnly(19, 0, 0));

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 2L,
                column: "hora_fin",
                value: new TimeOnly(7, 0, 0));

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 3L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 4L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 5L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "turno_planta",
                keyColumn: "id",
                keyValue: 1L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 1L,
                column: "hora_fin",
                value: new TimeOnly(19, 0, 0));

            migrationBuilder.UpdateData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 2L,
                column: "hora_fin",
                value: new TimeOnly(7, 0, 0));

            migrationBuilder.UpdateData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 3L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 4L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 5L,
                column: "activo",
                value: true);
        }
    }
}
