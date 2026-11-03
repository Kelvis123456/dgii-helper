using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DgiiHelper.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NcfRanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    TypeCode = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: false),
                    First = table.Column<long>(type: "bigint", nullable: false),
                    Last = table.Column<long>(type: "bigint", nullable: false),
                    Next = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NcfRanges", x => x.Id);
                    table.CheckConstraint("CK_NcfRanges_Bounds", "[First] >= 1 AND [Last] >= [First]");
                    table.CheckConstraint("CK_NcfRanges_Next", "[Next] >= [First] AND [Next] <= [Last] + 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NcfRanges");
        }
    }
}
