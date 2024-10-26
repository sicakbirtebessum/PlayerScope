using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayerScope.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountIdToPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.AddColumn<ulong>(
            //    name: "AccountId",
            //    table: "Players",
            //    type: "INTEGER",
            //    nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Players");
        }
    }
}
