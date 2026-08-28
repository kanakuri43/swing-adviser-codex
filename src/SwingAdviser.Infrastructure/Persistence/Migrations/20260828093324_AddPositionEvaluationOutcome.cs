using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwingAdviser.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionEvaluationOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "trg_position_evaluations_immutable_update";
                DROP TRIGGER IF EXISTS "trg_position_evaluations_immutable_delete";

                CREATE TABLE "position_evaluations_with_outcome" (
                    "id" TEXT NOT NULL CONSTRAINT "pk_position_evaluations" PRIMARY KEY,
                    "analysis_run_id" TEXT NOT NULL,
                    "position_id" TEXT NOT NULL,
                    "position_evaluation_input_manifest_id" TEXT NOT NULL,
                    "evaluation_bar_date" TEXT NOT NULL,
                    "evaluation_outcome" TEXT NOT NULL,
                    "exit_decision" TEXT NULL,
                    "reason_summary" TEXT NOT NULL,
                    "reasons_json" TEXT NOT NULL,
                    "lot_evaluations_json" TEXT NOT NULL,
                    "current_quantity" TEXT NULL,
                    "price_pnl" TEXT NULL,
                    "confirmed_cost_pnl" TEXT NULL,
                    "estimated_net_pnl" TEXT NULL,
                    "cost_to_r_ratio" TEXT NULL,
                    "partial_exit_quantity" INTEGER NULL,
                    "partial_exit_status" TEXT NOT NULL,
                    "created_at_utc" TEXT NOT NULL,
                    CONSTRAINT "ck_position_evaluations_current_quantity" CHECK (current_quantity IS NULL OR CAST(current_quantity AS NUMERIC) >= 0),
                    CONSTRAINT "ck_position_evaluations_exit_decision" CHECK (exit_decision IS NULL OR exit_decision IN ('Hold', 'TakeProfit', 'StopLoss', 'Exit')),
                    CONSTRAINT "ck_position_evaluations_fail_closed_partial_exit" CHECK (evaluation_outcome = 'Evaluated' OR (partial_exit_status = 'NotApplicable' AND partial_exit_quantity IS NULL)),
                    CONSTRAINT "ck_position_evaluations_outcome" CHECK (evaluation_outcome IN ('Evaluated', 'InsufficientHistory', 'HistoryIncomplete', 'InvalidData', 'PointInTimeUnverified', 'ReconciliationRequired', 'IncompletePositionData', 'IntradaySequenceUnknown', 'Failed')),
                    CONSTRAINT "ck_position_evaluations_outcome_decision" CHECK ((evaluation_outcome = 'Evaluated' AND exit_decision IS NOT NULL) OR (evaluation_outcome <> 'Evaluated' AND exit_decision IS NULL)),
                    CONSTRAINT "ck_position_evaluations_partial_exit_quantity" CHECK ((partial_exit_status = 'Candidate' AND partial_exit_quantity > 0) OR (partial_exit_status IN ('NotApplicable', 'NotFeasible') AND partial_exit_quantity IS NULL)),
                    CONSTRAINT "ck_position_evaluations_partial_exit_status" CHECK (partial_exit_status IN ('NotApplicable', 'Candidate', 'NotFeasible')),
                    CONSTRAINT "fk_position_evaluations_analysis_runs_analysis_run_id" FOREIGN KEY ("analysis_run_id") REFERENCES "analysis_runs" ("id") ON DELETE RESTRICT,
                    CONSTRAINT "fk_position_evaluations_position_evaluation_input_manifests_position_evaluation_input_manifest_id" FOREIGN KEY ("position_evaluation_input_manifest_id") REFERENCES "position_evaluation_input_manifests" ("id") ON DELETE RESTRICT,
                    CONSTRAINT "fk_position_evaluations_positions_position_id" FOREIGN KEY ("position_id") REFERENCES "positions" ("id") ON DELETE RESTRICT
                );

                INSERT INTO "position_evaluations_with_outcome" (
                    "id", "analysis_run_id", "position_id", "position_evaluation_input_manifest_id",
                    "evaluation_bar_date", "evaluation_outcome", "exit_decision", "reason_summary",
                    "reasons_json", "lot_evaluations_json", "current_quantity", "price_pnl",
                    "confirmed_cost_pnl", "estimated_net_pnl", "cost_to_r_ratio",
                    "partial_exit_quantity", "partial_exit_status", "created_at_utc")
                SELECT
                    "id", "analysis_run_id", "position_id", "position_evaluation_input_manifest_id",
                    "evaluation_bar_date", 'Evaluated', "exit_decision", "reason_summary",
                    "reasons_json", "lot_evaluations_json", "current_quantity", "price_pnl",
                    "confirmed_cost_pnl", "estimated_net_pnl", "cost_to_r_ratio",
                    "partial_exit_quantity", "partial_exit_status", "created_at_utc"
                FROM "position_evaluations";

                DROP TABLE "position_evaluations";
                ALTER TABLE "position_evaluations_with_outcome" RENAME TO "position_evaluations";

                CREATE INDEX "ix_position_evaluations_position_id"
                    ON "position_evaluations" ("position_id");
                CREATE UNIQUE INDEX "ux_position_evaluations_analysis_run_id_position_id"
                    ON "position_evaluations" ("analysis_run_id", "position_id");
                CREATE UNIQUE INDEX "ux_position_evaluations_position_evaluation_input_manifest_id"
                    ON "position_evaluations" ("position_evaluation_input_manifest_id");

                CREATE TRIGGER "trg_position_evaluations_immutable_update"
                BEFORE UPDATE ON "position_evaluations"
                BEGIN
                    SELECT RAISE(ABORT, 'position_evaluations is append-only');
                END;
                CREATE TRIGGER "trg_position_evaluations_immutable_delete"
                BEFORE DELETE ON "position_evaluations"
                BEGIN
                    SELECT RAISE(ABORT, 'position_evaluations cannot be deleted');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "trg_position_evaluations_immutable_update";
                DROP TRIGGER IF EXISTS "trg_position_evaluations_immutable_delete";

                CREATE TABLE "position_evaluations_before_outcome" (
                    "id" TEXT NOT NULL CONSTRAINT "pk_position_evaluations" PRIMARY KEY,
                    "analysis_run_id" TEXT NOT NULL,
                    "position_id" TEXT NOT NULL,
                    "position_evaluation_input_manifest_id" TEXT NOT NULL,
                    "evaluation_bar_date" TEXT NOT NULL,
                    "exit_decision" TEXT NOT NULL,
                    "reason_summary" TEXT NOT NULL,
                    "reasons_json" TEXT NOT NULL,
                    "lot_evaluations_json" TEXT NOT NULL,
                    "current_quantity" TEXT NOT NULL,
                    "price_pnl" TEXT NULL,
                    "confirmed_cost_pnl" TEXT NULL,
                    "estimated_net_pnl" TEXT NULL,
                    "cost_to_r_ratio" TEXT NULL,
                    "partial_exit_quantity" INTEGER NULL,
                    "partial_exit_status" TEXT NOT NULL,
                    "created_at_utc" TEXT NOT NULL,
                    CONSTRAINT "ck_position_evaluations_exit_decision" CHECK (exit_decision IN ('Hold', 'TakeProfit', 'StopLoss', 'Exit')),
                    CONSTRAINT "ck_position_evaluations_partial_exit_quantity" CHECK ((partial_exit_status = 'Candidate' AND partial_exit_quantity > 0) OR (partial_exit_status IN ('NotApplicable', 'NotFeasible') AND partial_exit_quantity IS NULL)),
                    CONSTRAINT "ck_position_evaluations_partial_exit_status" CHECK (partial_exit_status IN ('NotApplicable', 'Candidate', 'NotFeasible')),
                    CONSTRAINT "fk_position_evaluations_analysis_runs_analysis_run_id" FOREIGN KEY ("analysis_run_id") REFERENCES "analysis_runs" ("id") ON DELETE RESTRICT,
                    CONSTRAINT "fk_position_evaluations_position_evaluation_input_manifests_position_evaluation_input_manifest_id" FOREIGN KEY ("position_evaluation_input_manifest_id") REFERENCES "position_evaluation_input_manifests" ("id") ON DELETE RESTRICT,
                    CONSTRAINT "fk_position_evaluations_positions_position_id" FOREIGN KEY ("position_id") REFERENCES "positions" ("id") ON DELETE RESTRICT
                );

                INSERT INTO "position_evaluations_before_outcome" (
                    "id", "analysis_run_id", "position_id", "position_evaluation_input_manifest_id",
                    "evaluation_bar_date", "exit_decision", "reason_summary", "reasons_json",
                    "lot_evaluations_json", "current_quantity", "price_pnl", "confirmed_cost_pnl",
                    "estimated_net_pnl", "cost_to_r_ratio", "partial_exit_quantity",
                    "partial_exit_status", "created_at_utc")
                SELECT
                    "id", "analysis_run_id", "position_id", "position_evaluation_input_manifest_id",
                    "evaluation_bar_date", "exit_decision", "reason_summary", "reasons_json",
                    "lot_evaluations_json", "current_quantity", "price_pnl", "confirmed_cost_pnl",
                    "estimated_net_pnl", "cost_to_r_ratio", "partial_exit_quantity",
                    "partial_exit_status", "created_at_utc"
                FROM "position_evaluations";

                DROP TABLE "position_evaluations";
                ALTER TABLE "position_evaluations_before_outcome" RENAME TO "position_evaluations";

                CREATE INDEX "ix_position_evaluations_position_id"
                    ON "position_evaluations" ("position_id");
                CREATE UNIQUE INDEX "ux_position_evaluations_analysis_run_id_position_id"
                    ON "position_evaluations" ("analysis_run_id", "position_id");
                CREATE UNIQUE INDEX "ux_position_evaluations_position_evaluation_input_manifest_id"
                    ON "position_evaluations" ("position_evaluation_input_manifest_id");

                CREATE TRIGGER "trg_position_evaluations_immutable_update"
                BEFORE UPDATE ON "position_evaluations"
                BEGIN
                    SELECT RAISE(ABORT, 'position_evaluations is append-only');
                END;
                CREATE TRIGGER "trg_position_evaluations_immutable_delete"
                BEFORE DELETE ON "position_evaluations"
                BEGIN
                    SELECT RAISE(ABORT, 'position_evaluations cannot be deleted');
                END;
                """);
        }

    }
}
