using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwingAdviser.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestoreRiskBasisSnapshotTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TRIGGER "trg_risk_basis_snapshots_immutable_update"
                BEFORE UPDATE ON "risk_basis_snapshots"
                BEGIN
                    SELECT RAISE(ABORT, 'risk_basis_snapshots is append-only');
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER "trg_risk_basis_snapshots_immutable_delete"
                BEFORE DELETE ON "risk_basis_snapshots"
                BEGIN
                    SELECT RAISE(ABORT, 'risk_basis_snapshots cannot be deleted');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"trg_risk_basis_snapshots_immutable_update\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"trg_risk_basis_snapshots_immutable_delete\";");
        }
    }
}
