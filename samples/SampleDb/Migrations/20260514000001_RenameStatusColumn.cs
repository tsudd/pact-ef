using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SampleDb.Migrations
{
    /// <inheritdoc />
    public partial class RenameStatusColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Orders",
                newName: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "State",
                table: "Orders",
                newName: "Status");
        }
    }
}
