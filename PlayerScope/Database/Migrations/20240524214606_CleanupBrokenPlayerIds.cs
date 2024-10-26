using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayerScope.Database.Migrations
{
    /// <inheritdoc />
    public partial class CleanupBrokenPlayerIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM Players WHERE LocalContentId < 18014398509481984");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
