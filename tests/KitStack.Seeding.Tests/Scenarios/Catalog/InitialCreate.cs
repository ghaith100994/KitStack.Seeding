using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KitStack.Seeding.Tests.Scenarios.Catalog;

/// <summary>A hand-written migration, so the scenario exercises <c>DatabaseCreation.Migrate</c> with a pending migration.</summary>
[DbContext(typeof(CatalogDbContext))]
[Migration("20260101000000_InitialCreate")]
public class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Categories",
            columns: t => new
            {
                Id = t.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                Code = t.Column<string>(nullable: false),
                Name = t.Column<string>(nullable: false),
                IsActive = t.Column<bool>(nullable: false),
                IsDeleted = t.Column<bool>(nullable: false),
                ParentId = t.Column<int>(nullable: true),
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_Categories", x => x.Id);
                t.ForeignKey("FK_Categories_Categories_ParentId", x => x.ParentId, "Categories", "Id");
            });

        migrationBuilder.CreateTable(
            name: "Currencies",
            columns: t => new
            {
                Id = t.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                Code = t.Column<string>(nullable: false),
                Name = t.Column<string>(nullable: false),
                DecimalPlaces = t.Column<int>(nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Currencies", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Products",
            columns: t => new
            {
                Id = t.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = t.Column<string>(nullable: false),
                CategoryId = t.Column<int>(nullable: false),
                CurrencyId = t.Column<int>(nullable: false),
                Price = t.Column<decimal>(type: "TEXT", nullable: false),
                ListedOn = t.Column<DateOnly>(type: "TEXT", nullable: false),
                IsDeleted = t.Column<bool>(nullable: false),
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_Products", x => x.Id);
                t.ForeignKey("FK_Products_Categories_CategoryId", x => x.CategoryId, "Categories", "Id");
                t.ForeignKey("FK_Products_Currencies_CurrencyId", x => x.CurrencyId, "Currencies", "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProductTag",
            columns: t => new
            {
                Id = t.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                ProductId = t.Column<int>(nullable: false),
                Value = t.Column<string>(nullable: false),
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_ProductTag", x => x.Id);
                t.ForeignKey("FK_ProductTag_Products_ProductId", x => x.ProductId, "Products", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_Categories_Code", "Categories", "Code", unique: true);
        migrationBuilder.CreateIndex("IX_Currencies_Code", "Currencies", "Code", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ProductTag");
        migrationBuilder.DropTable("Products");
        migrationBuilder.DropTable("Currencies");
        migrationBuilder.DropTable("Categories");
    }
}
