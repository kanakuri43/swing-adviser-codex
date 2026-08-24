using Microsoft.EntityFrameworkCore.Migrations;

namespace SwingAdviser.Infrastructure.Persistence.Migrations;

public partial class AddBusinessSchema
{
    private static readonly string[] ImmutableTables =
    [
        "instruments",
        "instrument_identifiers",
        "instrument_identifier_revisions",
        "instrument_master_revisions",
        "market_calendar_versions",
        "market_calendar_days",
        "source_artifacts",
        "data_update_items",
        "data_update_failures",
        "daily_prices",
        "daily_price_revisions",
        "price_history_assessments",
        "price_revision_sets",
        "price_revision_set_changes",
        "corporate_actions",
        "corporate_action_revisions",
        "margin_eligibility_records",
        "margin_eligibility_revisions",
        "published_margin_costs",
        "published_margin_cost_revisions",
        "fundamental_records",
        "fundamental_revisions",
        "strategy_parameter_snapshots",
        "analysis_input_manifests",
        "analysis_action_applications",
        "technical_analysis_results",
        "indicator_results",
        "candidate_results",
        "candidate_score_components",
        "positions",
        "position_state_revisions",
        "trade_executions",
        "trade_execution_revisions",
        "margin_lots",
        "margin_lot_contract_revisions",
        "lot_allocation_revisions",
        "position_adjustments",
        "risk_basis_snapshots",
        "risk_plan_revisions",
        "position_evaluation_input_manifests",
        "position_evaluations",
        "margin_cost_items",
        "margin_cost_observations",
        "margin_cost_amount_components",
        "prompt_template_snapshots",
        "ai_profile_snapshots",
        "ai_check_jobs",
        "ai_job_request_events",
        "ai_attempt_events",
        "ai_results",
        "ai_result_sources",
    ];

    private static void CreateBusinessTriggers(MigrationBuilder migrationBuilder)
    {
        foreach (var table in ImmutableTables)
        {
            migrationBuilder.Sql($$"""
                CREATE TRIGGER "trg_{{table}}_immutable_update"
                BEFORE UPDATE ON "{{table}}"
                BEGIN
                    SELECT RAISE(ABORT, '{{table}} is append-only');
                END;
                """);
            migrationBuilder.Sql($$"""
                CREATE TRIGGER "trg_{{table}}_immutable_delete"
                BEFORE DELETE ON "{{table}}"
                BEGIN
                    SELECT RAISE(ABORT, '{{table}} cannot be deleted');
                END;
                """);
        }

        CreateOperationalTrigger(
            migrationBuilder,
            "data_update_runs",
            ["id", "dataset_kind", "provider", "requested_at_utc", "requested_count", "configuration_snapshot_json", "configuration_sha256"],
            ["Succeeded", "PartiallySucceeded", "Failed", "Cancelled"],
            "(OLD.status = NEW.status) OR " +
            "(OLD.status = 'Queued' AND NEW.status IN ('Running', 'Failed', 'Cancelled')) OR " +
            "(OLD.status = 'Running' AND NEW.status IN ('Succeeded', 'PartiallySucceeded', 'Failed', 'Cancelled'))");

        CreateOperationalTrigger(
            migrationBuilder,
            "analysis_runs",
            [
                "id", "evaluation_bar_date", "analyzed_at_utc", "recorded_cutoff_at_utc", "run_mode",
                "strategy_parameter_snapshot_id", "point_in_time_status", "price_selector_version",
                "adjustment_engine_version", "indicator_engine_version", "candidate_engine_version",
                "market_calendar_version_id", "application_version",
            ],
            ["Succeeded", "PartiallySucceeded", "Failed", "Cancelled"],
            "(OLD.status = NEW.status) OR " +
            "(OLD.status = 'Queued' AND NEW.status IN ('Running', 'Failed', 'Cancelled')) OR " +
            "(OLD.status = 'Running' AND NEW.status IN ('Succeeded', 'PartiallySucceeded', 'Failed', 'Cancelled'))");

        CreateOperationalTrigger(
            migrationBuilder,
            "ai_attempts",
            [
                "id", "ai_check_job_id", "attempt_no", "attempt_kind", "request_origin",
                "requested_at_utc", "priority_at_queue", "queued_at_utc", "timeout_seconds", "arguments_json",
            ],
            ["Succeeded", "Failed", "TimedOut", "InsufficientInformation", "Cancelled"],
            "(OLD.status = NEW.status) OR " +
            "(OLD.status = 'Queued' AND NEW.status IN ('Running', 'Failed', 'Cancelled')) OR " +
            "(OLD.status = 'Running' AND NEW.status IN ('Succeeded', 'Failed', 'TimedOut', 'InsufficientInformation', 'Cancelled'))");

        migrationBuilder.Sql("""
            CREATE TRIGGER "trg_margin_cost_observations_validate_reconciliation_insert"
            BEFORE INSERT ON "margin_cost_observations"
            WHEN NEW.reconciles_estimate_id IS NOT NULL
                 AND NOT EXISTS (
                     SELECT 1
                     FROM margin_cost_observations AS estimate
                     WHERE estimate.id = NEW.reconciles_estimate_id
                       AND estimate.margin_cost_item_id = NEW.margin_cost_item_id
                       AND estimate.valuation_kind = 'Estimate'
                       AND NOT EXISTS (
                           SELECT 1
                           FROM margin_cost_observations AS successor
                           WHERE successor.supersedes_id = estimate.id))
            BEGIN
                SELECT RAISE(ABORT, 'reconciles_estimate_id must reference the current estimate leaf of the same cost item');
            END;
            """);
    }

    private static void CreateOperationalTrigger(
        MigrationBuilder migrationBuilder,
        string table,
        IReadOnlyCollection<string> immutableColumns,
        IReadOnlyCollection<string> terminalStatuses,
        string validTransitionSql)
    {
        var immutableChangeSql = string.Join(
            " OR ",
            immutableColumns.Select(column => $"OLD.\"{column}\" IS NOT NEW.\"{column}\""));
        var terminalStatusSql = string.Join(
            ", ",
            terminalStatuses.Select(status => $"'{status}'"));

        migrationBuilder.Sql($$"""
            CREATE TRIGGER "trg_{{table}}_operational_update"
            BEFORE UPDATE ON "{{table}}"
            WHEN OLD.status IN ({{terminalStatusSql}})
                 OR {{immutableChangeSql}}
                 OR NOT ({{validTransitionSql}})
            BEGIN
                SELECT RAISE(ABORT, '{{table}} update violates its operational state contract');
            END;
            """);
        migrationBuilder.Sql($$"""
            CREATE TRIGGER "trg_{{table}}_operational_delete"
            BEFORE DELETE ON "{{table}}"
            BEGIN
                SELECT RAISE(ABORT, '{{table}} cannot be deleted');
            END;
            """);
    }

    private static void DropBusinessTriggers(MigrationBuilder migrationBuilder)
    {
        foreach (var table in ImmutableTables)
        {
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS \"trg_{table}_immutable_update\";");
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS \"trg_{table}_immutable_delete\";");
        }

        foreach (var table in new[] { "data_update_runs", "analysis_runs", "ai_attempts" })
        {
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS \"trg_{table}_operational_update\";");
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS \"trg_{table}_operational_delete\";");
        }

        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS \"trg_margin_cost_observations_validate_reconciliation_insert\";");
    }
}
