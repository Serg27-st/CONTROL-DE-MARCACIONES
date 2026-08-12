using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class RolesYUsuarioAmpliado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "apellido_materno",
                table: "asp_net_users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "apellido_paterno",
                table: "asp_net_users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "empresa_id",
                table: "asp_net_users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nombres",
                table: "asp_net_users",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_documento",
                table: "asp_net_users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "tipo_documento_id",
                table: "asp_net_users",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "usuario_planta",
                columns: table => new
                {
                    usuario_id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    planta_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_planta", x => new { x.usuario_id, x.planta_id });
                    table.ForeignKey(
                        name: "fk_usuario_planta_asp_net_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_usuario_planta_planta_planta_id",
                        column: x => x.planta_id,
                        principalTable: "planta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "asp_net_roles",
                columns: new[] { "id", "concurrency_stamp", "name", "normalized_name" },
                values: new object[,]
                {
                    { "00000000-0000-0000-0000-000000000005", "00000000-0000-0000-0000-000000000005", "COORDINADOR", "COORDINADOR" },
                    { "00000000-0000-0000-0000-000000000006", "00000000-0000-0000-0000-000000000006", "GERENCIA", "GERENCIA" },
                    { "00000000-0000-0000-0000-000000000007", "00000000-0000-0000-0000-000000000007", "TRABAJADOR_SOCIAL", "TRABAJADOR_SOCIAL" }
                });

            // Migra las cuentas ADMINISTRADOR existentes a COORDINADOR: el rol ADMINISTRADOR
            // (mismo Id, 00000000-0000-0000-0000-000000000001) cambia de "superadministrador"
            // a "gestiona el tareo", así que quien hoy tiene acceso total debe conservarlo
            // bajo el nuevo rol en vez de perderlo. No se toca la cuenta: mismo correo,
            // mismo hash de contraseña.
            migrationBuilder.Sql(
                """
                UPDATE asp_net_user_roles
                SET role_id = '00000000-0000-0000-0000-000000000005'
                WHERE role_id = '00000000-0000-0000-0000-000000000001';
                """);

            // RRHH/SUPERVISOR/CONSULTA desaparecen con esta migración. De las cuentas
            // existentes, la única con uno de esos tres roles es 'rrhh@gca.local', una
            // cuenta de prueba creada para verificar la autenticación (no es de GCA). Se
            // borra por correo exacto — nunca un DELETE sin condición sobre asp_net_users.
            migrationBuilder.Sql(
                """
                DELETE FROM asp_net_user_roles
                WHERE user_id IN (SELECT id FROM asp_net_users WHERE email = 'rrhh@gca.local');

                DELETE FROM asp_net_user_claims
                WHERE user_id IN (SELECT id FROM asp_net_users WHERE email = 'rrhh@gca.local');

                DELETE FROM asp_net_user_logins
                WHERE user_id IN (SELECT id FROM asp_net_users WHERE email = 'rrhh@gca.local');

                DELETE FROM asp_net_user_tokens
                WHERE user_id IN (SELECT id FROM asp_net_users WHERE email = 'rrhh@gca.local');

                DELETE FROM asp_net_users WHERE email = 'rrhh@gca.local';
                """);

            migrationBuilder.DeleteData(
                table: "asp_net_roles",
                keyColumn: "id",
                keyValue: "00000000-0000-0000-0000-000000000002");

            migrationBuilder.DeleteData(
                table: "asp_net_roles",
                keyColumn: "id",
                keyValue: "00000000-0000-0000-0000-000000000003");

            migrationBuilder.DeleteData(
                table: "asp_net_roles",
                keyColumn: "id",
                keyValue: "00000000-0000-0000-0000-000000000004");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_planta_planta_id",
                table: "usuario_planta",
                column: "planta_id");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Revierte el esquema y la reasignación de rol (COORDINADOR vuelve a ADMINISTRADOR).
        /// Lo único que NO vuelve es la cuenta de prueba <c>rrhh@gca.local</c> que borró
        /// <c>Up</c> — una cuenta no se reconstruye desde una migración porque su contraseña
        /// ya no está en ningún lado.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usuario_planta");

            // Devuelve a ADMINISTRADOR a quien Up() migró a COORDINADOR. Tiene que ir antes
            // del DeleteData de abajo: si queda una fila de asp_net_user_roles apuntando al
            // rol COORDINADOR, el DeleteData viola la FK.
            migrationBuilder.Sql(
                """
                UPDATE asp_net_user_roles
                SET role_id = '00000000-0000-0000-0000-000000000001'
                WHERE role_id = '00000000-0000-0000-0000-000000000005';
                """);

            migrationBuilder.DeleteData(
                table: "asp_net_roles",
                keyColumn: "id",
                keyValue: "00000000-0000-0000-0000-000000000005");

            migrationBuilder.DeleteData(
                table: "asp_net_roles",
                keyColumn: "id",
                keyValue: "00000000-0000-0000-0000-000000000006");

            migrationBuilder.DeleteData(
                table: "asp_net_roles",
                keyColumn: "id",
                keyValue: "00000000-0000-0000-0000-000000000007");

            migrationBuilder.DropColumn(
                name: "apellido_materno",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "apellido_paterno",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "empresa_id",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "nombres",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "numero_documento",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "tipo_documento_id",
                table: "asp_net_users");

            migrationBuilder.InsertData(
                table: "asp_net_roles",
                columns: new[] { "id", "concurrency_stamp", "name", "normalized_name" },
                values: new object[,]
                {
                    { "00000000-0000-0000-0000-000000000002", "00000000-0000-0000-0000-000000000002", "RRHH", "RRHH" },
                    { "00000000-0000-0000-0000-000000000003", "00000000-0000-0000-0000-000000000003", "SUPERVISOR", "SUPERVISOR" },
                    { "00000000-0000-0000-0000-000000000004", "00000000-0000-0000-0000-000000000004", "CONSULTA", "CONSULTA" }
                });
        }
    }
}
