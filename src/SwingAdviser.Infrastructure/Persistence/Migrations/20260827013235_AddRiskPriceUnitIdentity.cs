using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwingAdviser.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskPriceUnitIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DropRiskBasisTriggers(migrationBuilder);

            migrationBuilder.AddColumn<string>(
                name: "price_currency",
                table: "risk_basis_snapshots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "price_unit_basis_sha256",
                table: "risk_basis_snapshots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_risk_basis_snapshots_price_currency",
                table: "risk_basis_snapshots",
                sql: "price_currency IS NULL OR length(price_currency) = 3");

            migrationBuilder.AddCheckConstraint(
                name: "ck_risk_basis_snapshots_price_unit_basis_sha256",
                table: "risk_basis_snapshots",
                sql: "price_unit_basis_sha256 IS NULL OR (length(price_unit_basis_sha256) = 64 AND price_unit_basis_sha256 NOT GLOB '*[^0-9a-f]*')");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropRiskBasisTriggers(migrationBuilder);

            migrationBuilder.DropCheckConstraint(
                name: "ck_risk_basis_snapshots_price_currency",
                table: "risk_basis_snapshots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_risk_basis_snapshots_price_unit_basis_sha256",
                table: "risk_basis_snapshots");

            migrationBuilder.DropColumn(
                name: "price_currency",
                table: "risk_basis_snapshots");

            migrationBuilder.DropColumn(
                name: "price_unit_basis_sha256",
                table: "risk_basis_snapshots");

        }

        private static void DropRiskBasisTriggers(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"trg_risk_basis_snapshots_immutable_update\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"trg_risk_basis_snapshots_immutable_delete\";");
        }

    }
}
