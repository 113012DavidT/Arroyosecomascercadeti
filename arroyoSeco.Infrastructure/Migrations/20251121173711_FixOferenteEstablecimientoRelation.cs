using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace arroyoSeco.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixOferenteEstablecimientoRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Establecimientos_Oferentes_OferenteId",
                table: "Establecimientos");

            migrationBuilder.AddForeignKey(
                name: "FK_Establecimientos_Oferentes_OferenteId",
                table: "Establecimientos",
                column: "OferenteId",
                principalTable: "Oferentes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Establecimientos_Oferentes_OferenteId",
                table: "Establecimientos");

            migrationBuilder.AddForeignKey(
                name: "FK_Establecimientos_Oferentes_OferenteId",
                table: "Establecimientos",
                column: "OferenteId",
                principalTable: "Oferentes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
