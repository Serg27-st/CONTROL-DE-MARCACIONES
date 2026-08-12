using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AnulacionImportacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "anulada",
                table: "importacion_marcador",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "anulada_en",
                table: "importacion_marcador",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "anulada_por_usuario",
                table: "importacion_marcador",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_anulacion",
                table: "importacion_marcador",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_importacion_marcador_anulada",
                table: "importacion_marcador",
                column: "anulada");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_importacion_marcador_anulada",
                table: "importacion_marcador");

            migrationBuilder.DropColumn(
                name: "anulada",
                table: "importacion_marcador");

            migrationBuilder.DropColumn(
                name: "anulada_en",
                table: "importacion_marcador");

            migrationBuilder.DropColumn(
                name: "anulada_por_usuario",
                table: "importacion_marcador");

            migrationBuilder.DropColumn(
                name: "motivo_anulacion",
                table: "importacion_marcador");
        }
    }
}
