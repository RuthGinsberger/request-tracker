using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequestTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CoveringCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_CreatedAt",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CreatedAt",
                table: "Requests",
                column: "CreatedAt")
                .Annotation("SqlServer:Include", new[] { "Title", "OrganizationName", "Status", "Priority", "AssignedTo", "UpdatedAt", "RowVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_CreatedAt",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CreatedAt",
                table: "Requests",
                column: "CreatedAt");
        }
    }
}
