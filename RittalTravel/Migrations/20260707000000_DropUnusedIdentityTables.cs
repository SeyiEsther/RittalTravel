using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RittalTravel.Migrations
{
    /// <inheritdoc />
    public partial class DropUnusedIdentityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[AspNetUserTokens]', N'U') IS NOT NULL DROP TABLE [AspNetUserTokens];
IF OBJECT_ID(N'[AspNetUserRoles]', N'U') IS NOT NULL DROP TABLE [AspNetUserRoles];
IF OBJECT_ID(N'[AspNetUserLogins]', N'U') IS NOT NULL DROP TABLE [AspNetUserLogins];
IF OBJECT_ID(N'[AspNetUserClaims]', N'U') IS NOT NULL DROP TABLE [AspNetUserClaims];
IF OBJECT_ID(N'[AspNetRoleClaims]', N'U') IS NOT NULL DROP TABLE [AspNetRoleClaims];
IF OBJECT_ID(N'[AspNetUsers]', N'U') IS NOT NULL DROP TABLE [AspNetUsers];
IF OBJECT_ID(N'[AspNetRoles]', N'U') IS NOT NULL DROP TABLE [AspNetRoles];
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Identity was never wired up in this application; no rollback.
        }
    }
}
