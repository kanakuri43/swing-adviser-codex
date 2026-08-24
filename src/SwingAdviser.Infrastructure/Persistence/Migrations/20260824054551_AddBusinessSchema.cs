using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwingAdviser.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_profile_snapshots",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    profile_name = table.Column<string>(type: "TEXT", nullable: false),
                    executable_identity = table.Column<string>(type: "TEXT", nullable: false),
                    requested_model = table.Column<string>(type: "TEXT", nullable: true),
                    timeout_seconds = table.Column<int>(type: "INTEGER", nullable: false),
                    arguments_json = table.Column<string>(type: "TEXT", nullable: false),
                    configuration_json = table.Column<string>(type: "TEXT", nullable: false),
                    profile_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_profile_snapshots", x => x.id);
                    table.CheckConstraint("ck_ai_profile_snapshots_profile_sha256", "length(profile_sha256) = 64 AND profile_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_ai_profile_snapshots_timeout_seconds", "timeout_seconds > 0");
                });

            migrationBuilder.CreateTable(
                name: "data_update_runs",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    dataset_kind = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    requested_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    started_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    completed_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    requested_count = table.Column<long>(type: "INTEGER", nullable: true),
                    success_count = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    failure_count = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    unchanged_count = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    configuration_snapshot_json = table.Column<string>(type: "TEXT", nullable: false),
                    configuration_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_update_runs", x => x.id);
                    table.CheckConstraint("ck_data_update_runs_counts", "success_count >= 0 AND failure_count >= 0 AND unchanged_count >= 0");
                    table.CheckConstraint("ck_data_update_runs_requested_count", "requested_count IS NULL OR requested_count >= 0");
                    table.CheckConstraint("ck_data_update_runs_sha256", "length(configuration_sha256) = 64 AND configuration_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_data_update_runs_status", "status IN ('Queued', 'Running', 'Succeeded', 'PartiallySucceeded', 'Failed', 'Cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "instruments",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instruments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "prompt_template_snapshots",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    template_version = table.Column<string>(type: "TEXT", nullable: false),
                    template_text = table.Column<string>(type: "TEXT", nullable: false),
                    template_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prompt_template_snapshots", x => x.id);
                    table.CheckConstraint("ck_prompt_template_snapshots_template_sha256", "length(template_sha256) = 64 AND template_sha256 NOT GLOB '*[^0-9a-f]*'");
                });

            migrationBuilder.CreateTable(
                name: "source_artifacts",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    dataset_kind = table.Column<string>(type: "TEXT", nullable: false),
                    source_uri = table.Column<string>(type: "TEXT", nullable: true),
                    retrieved_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    source_published_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    available_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    availability_status = table.Column<string>(type: "TEXT", nullable: false),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    media_type = table.Column<string>(type: "TEXT", nullable: true),
                    retention_status = table.Column<string>(type: "TEXT", nullable: false),
                    content_blob = table.Column<byte[]>(type: "BLOB", nullable: true),
                    external_location = table.Column<string>(type: "TEXT", nullable: true),
                    content_encoding = table.Column<string>(type: "TEXT", nullable: true),
                    metadata_json = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_source_artifacts", x => x.id);
                    table.CheckConstraint("ck_source_artifacts_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
                    table.CheckConstraint("ck_source_artifacts_retention_payload", "(retention_status = 'RetainedInline' AND content_blob IS NOT NULL AND external_location IS NULL) OR (retention_status = 'RetainedExternal' AND content_blob IS NULL AND external_location IS NOT NULL) OR (retention_status = 'HashOnly' AND content_blob IS NULL AND external_location IS NULL)");
                    table.CheckConstraint("ck_source_artifacts_retention_status", "retention_status IN ('RetainedInline', 'RetainedExternal', 'HashOnly')");
                    table.CheckConstraint("ck_source_artifacts_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                });

            migrationBuilder.CreateTable(
                name: "strategy_parameter_snapshots",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    strategy_key = table.Column<string>(type: "TEXT", nullable: false),
                    strategy_version = table.Column<string>(type: "TEXT", nullable: false),
                    schema_version = table.Column<string>(type: "TEXT", nullable: false),
                    algorithm_version = table.Column<string>(type: "TEXT", nullable: false),
                    parameters_json = table.Column<string>(type: "TEXT", nullable: false),
                    parameters_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    captured_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    source_description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_strategy_parameter_snapshots", x => x.id);
                    table.CheckConstraint("ck_strategy_parameter_snapshots_sha256", "length(parameters_sha256) = 64 AND parameters_sha256 NOT GLOB '*[^0-9a-f]*'");
                });

            migrationBuilder.CreateTable(
                name: "corporate_actions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    source_event_id = table.Column<string>(type: "TEXT", nullable: true),
                    derived_event_key = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_corporate_actions", x => x.id);
                    table.ForeignKey(
                        name: "fk_corporate_actions_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "daily_prices",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                    bar_date = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_prices", x => x.id);
                    table.ForeignKey(
                        name: "fk_daily_prices_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fundamental_records",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    source_record_key = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fundamental_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_fundamental_records_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "instrument_identifiers",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                    scheme = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instrument_identifiers", x => x.id);
                    table.ForeignKey(
                        name: "fk_instrument_identifiers_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "margin_eligibility_records",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    source_record_key = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_margin_eligibility_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_margin_eligibility_records_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_revision_sets",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    parent_set_id = table.Column<string>(type: "TEXT", nullable: true),
                    first_bar_date = table.Column<string>(type: "TEXT", nullable: true),
                    last_bar_date = table.Column<string>(type: "TEXT", nullable: true),
                    bar_count = table.Column<long>(type: "INTEGER", nullable: false),
                    set_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    selector_version = table.Column<string>(type: "TEXT", nullable: false),
                    selected_available_cutoff_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    selected_recorded_cutoff_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    point_in_time_status = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_revision_sets", x => x.id);
                    table.CheckConstraint("ck_price_revision_sets_bar_count", "bar_count >= 0");
                    table.CheckConstraint("ck_price_revision_sets_bar_range", "last_bar_date IS NULL OR first_bar_date IS NULL OR last_bar_date >= first_bar_date");
                    table.CheckConstraint("ck_price_revision_sets_empty_range", "(bar_count = 0 AND first_bar_date IS NULL AND last_bar_date IS NULL) OR (bar_count > 0 AND first_bar_date IS NOT NULL AND last_bar_date IS NOT NULL)");
                    table.CheckConstraint("ck_price_revision_sets_point_in_time_status", "point_in_time_status IN ('Verified', 'Unverified')");
                    table.CheckConstraint("ck_price_revision_sets_sha256", "length(set_sha256) = 64 AND set_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.ForeignKey(
                        name: "fk_price_revision_sets_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_revision_sets_price_revision_sets_parent_set_id",
                        column: x => x.parent_set_id,
                        principalTable: "price_revision_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "published_margin_costs",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    cost_type = table.Column<string>(type: "TEXT", nullable: false),
                    source_record_key = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_published_margin_costs", x => x.id);
                    table.CheckConstraint("ck_published_margin_costs_cost_type", "cost_type IN ('Backwardation')");
                    table.ForeignKey(
                        name: "fk_published_margin_costs_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "data_update_items",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    data_update_run_id = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: true),
                    item_key = table.Column<string>(type: "TEXT", nullable: false),
                    item_attempt_no = table.Column<long>(type: "INTEGER", nullable: false),
                    outcome = table.Column<string>(type: "TEXT", nullable: false),
                    resolved_entity_type = table.Column<string>(type: "TEXT", nullable: true),
                    resolved_revision_id = table.Column<string>(type: "TEXT", nullable: true),
                    observed_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_update_items", x => x.id);
                    table.CheckConstraint("ck_data_update_items_attempt_no", "item_attempt_no >= 1");
                    table.CheckConstraint("ck_data_update_items_outcome", "outcome IN ('Inserted', 'Corrected', 'Unchanged', 'Skipped', 'Failed')");
                    table.ForeignKey(
                        name: "fk_data_update_items_data_update_runs_data_update_run_id",
                        column: x => x.data_update_run_id,
                        principalTable: "data_update_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_data_update_items_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_data_update_items_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "instrument_master_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    available_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    availability_status = table.Column<string>(type: "TEXT", nullable: false),
                    first_observed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    effective_from_date = table.Column<string>(type: "TEXT", nullable: false),
                    effective_to_date = table.Column<string>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    exchange_code = table.Column<string>(type: "TEXT", nullable: false),
                    market_segment = table.Column<string>(type: "TEXT", nullable: false),
                    security_type = table.Column<string>(type: "TEXT", nullable: false),
                    trading_unit = table.Column<long>(type: "INTEGER", nullable: true),
                    currency = table.Column<string>(type: "TEXT", nullable: false),
                    listing_date = table.Column<string>(type: "TEXT", nullable: true),
                    delisting_date = table.Column<string>(type: "TEXT", nullable: true),
                    listing_status = table.Column<string>(type: "TEXT", nullable: false),
                    scan_eligibility = table.Column<string>(type: "TEXT", nullable: false),
                    exclusion_reason = table.Column<string>(type: "TEXT", nullable: true),
                    change_kind = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instrument_master_revisions", x => x.id);
                    table.CheckConstraint("ck_instrument_master_revisions_availability", "(availability_status IN ('Known', 'Estimated') AND available_at_utc IS NOT NULL AND available_at_utc <= first_observed_at_utc) OR (availability_status = 'Unknown' AND available_at_utc IS NULL)");
                    table.CheckConstraint("ck_instrument_master_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
                    table.CheckConstraint("ck_instrument_master_revisions_change_kind", "change_kind IN ('EffectiveSnapshot', 'Correction', 'Cancellation')");
                    table.CheckConstraint("ck_instrument_master_revisions_currency", "length(currency) = 3 AND currency = upper(currency)");
                    table.CheckConstraint("ck_instrument_master_revisions_effective_range", "effective_to_date IS NULL OR effective_to_date >= effective_from_date");
                    table.CheckConstraint("ck_instrument_master_revisions_exclusion_reason", "(scan_eligibility = 'Excluded' AND exclusion_reason IS NOT NULL) OR (scan_eligibility <> 'Excluded')");
                    table.CheckConstraint("ck_instrument_master_revisions_listing_range", "delisting_date IS NULL OR listing_date IS NULL OR delisting_date >= listing_date");
                    table.CheckConstraint("ck_instrument_master_revisions_listing_status", "listing_status IN ('Listed', 'DelistingScheduled', 'Delisted', 'Unknown')");
                    table.CheckConstraint("ck_instrument_master_revisions_observation_recording_order", "first_observed_at_utc <= recorded_at_utc");
                    table.CheckConstraint("ck_instrument_master_revisions_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
                    table.CheckConstraint("ck_instrument_master_revisions_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_instrument_master_revisions_scan_eligibility", "scan_eligibility IN ('Eligible', 'Excluded', 'Unknown')");
                    table.CheckConstraint("ck_instrument_master_revisions_security_type", "security_type IN ('DomesticCommonStock', 'ETF', 'ETN', 'REIT', 'Preferred', 'Foreign', 'Other', 'Unknown')");
                    table.CheckConstraint("ck_instrument_master_revisions_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_instrument_master_revisions_trading_unit", "trading_unit IS NULL OR trading_unit > 0");
                    table.ForeignKey(
                        name: "fk_instrument_master_revisions_instrument_master_revisions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "instrument_master_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_instrument_master_revisions_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_instrument_master_revisions_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "market_calendar_versions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    market_code = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    version_name = table.Column<string>(type: "TEXT", nullable: false),
                    time_zone_id = table.Column<string>(type: "TEXT", nullable: false),
                    algorithm_version = table.Column<string>(type: "TEXT", nullable: false),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_calendar_versions", x => x.id);
                    table.CheckConstraint("ck_market_calendar_versions_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.ForeignKey(
                        name: "fk_market_calendar_versions_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_history_assessments",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    first_valid_bar_date = table.Column<string>(type: "TEXT", nullable: true),
                    last_valid_bar_date = table.Column<string>(type: "TEXT", nullable: true),
                    valid_bar_count = table.Column<long>(type: "INTEGER", nullable: false),
                    completeness_status = table.Column<string>(type: "TEXT", nullable: false),
                    listing_date_evidence = table.Column<string>(type: "TEXT", nullable: true),
                    reason = table.Column<string>(type: "TEXT", nullable: true),
                    assessed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    algorithm_version = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_history_assessments", x => x.id);
                    table.CheckConstraint("ck_price_history_assessments_bar_range", "last_valid_bar_date IS NULL OR first_valid_bar_date IS NULL OR last_valid_bar_date >= first_valid_bar_date");
                    table.CheckConstraint("ck_price_history_assessments_completeness_status", "completeness_status IN ('CompleteFromListing', 'Incomplete', 'Unverified', 'Invalid')");
                    table.CheckConstraint("ck_price_history_assessments_reason", "completeness_status = 'CompleteFromListing' OR reason IS NOT NULL");
                    table.CheckConstraint("ck_price_history_assessments_valid_bar_count", "valid_bar_count >= 0");
                    table.ForeignKey(
                        name: "fk_price_history_assessments_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_history_assessments_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "corporate_action_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    available_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    availability_status = table.Column<string>(type: "TEXT", nullable: false),
                    first_observed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    corporate_action_id = table.Column<string>(type: "TEXT", nullable: false),
                    action_type = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    effective_date = table.Column<string>(type: "TEXT", nullable: false),
                    announced_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    ratio_numerator = table.Column<long>(type: "INTEGER", nullable: true),
                    ratio_denominator = table.Column<long>(type: "INTEGER", nullable: true),
                    cash_amount_per_share = table.Column<string>(type: "TEXT", nullable: true),
                    currency = table.Column<string>(type: "TEXT", nullable: true),
                    point_in_time_status = table.Column<string>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_corporate_action_revisions", x => x.id);
                    table.CheckConstraint("ck_corporate_action_revisions_action_type", "action_type IN ('Split', 'Consolidation', 'CashDividend', 'Unsupported')");
                    table.CheckConstraint("ck_corporate_action_revisions_availability", "(availability_status IN ('Known', 'Estimated') AND available_at_utc IS NOT NULL AND available_at_utc <= first_observed_at_utc) OR (availability_status = 'Unknown' AND available_at_utc IS NULL)");
                    table.CheckConstraint("ck_corporate_action_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
                    table.CheckConstraint("ck_corporate_action_revisions_currency", "currency IS NULL OR (length(currency) = 3 AND currency = upper(currency))");
                    table.CheckConstraint("ck_corporate_action_revisions_details", "(action_type IN ('Split', 'Consolidation') AND ratio_numerator > 0 AND ratio_denominator > 0 AND cash_amount_per_share IS NULL AND currency IS NULL) OR (action_type = 'CashDividend' AND ratio_numerator IS NULL AND ratio_denominator IS NULL AND cash_amount_per_share IS NOT NULL AND CAST(cash_amount_per_share AS NUMERIC) >= 0 AND currency IS NOT NULL) OR (action_type = 'Unsupported' AND ratio_numerator IS NULL AND ratio_denominator IS NULL AND cash_amount_per_share IS NULL AND currency IS NULL)");
                    table.CheckConstraint("ck_corporate_action_revisions_observation_recording_order", "first_observed_at_utc <= recorded_at_utc");
                    table.CheckConstraint("ck_corporate_action_revisions_point_in_time_status", "point_in_time_status IN ('Verified', 'Unverified')");
                    table.CheckConstraint("ck_corporate_action_revisions_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
                    table.CheckConstraint("ck_corporate_action_revisions_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_corporate_action_revisions_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_corporate_action_revisions_status", "status IN ('Announced', 'Confirmed', 'Corrected', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_corporate_action_revisions_corporate_action_revisions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "corporate_action_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_corporate_action_revisions_corporate_actions_corporate_action_id",
                        column: x => x.corporate_action_id,
                        principalTable: "corporate_actions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_corporate_action_revisions_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "daily_price_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    available_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    availability_status = table.Column<string>(type: "TEXT", nullable: false),
                    first_observed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    daily_price_id = table.Column<string>(type: "TEXT", nullable: false),
                    provider_symbol = table.Column<string>(type: "TEXT", nullable: false),
                    open = table.Column<string>(type: "TEXT", nullable: false),
                    high = table.Column<string>(type: "TEXT", nullable: false),
                    low = table.Column<string>(type: "TEXT", nullable: false),
                    close = table.Column<string>(type: "TEXT", nullable: false),
                    volume = table.Column<long>(type: "INTEGER", nullable: false),
                    provider_adjclose = table.Column<string>(type: "TEXT", nullable: true),
                    currency = table.Column<string>(type: "TEXT", nullable: false),
                    bar_status = table.Column<string>(type: "TEXT", nullable: false),
                    provider_event_id = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_price_revisions", x => x.id);
                    table.CheckConstraint("ck_daily_price_revisions_availability", "(availability_status IN ('Known', 'Estimated') AND available_at_utc IS NOT NULL AND available_at_utc <= first_observed_at_utc) OR (availability_status = 'Unknown' AND available_at_utc IS NULL)");
                    table.CheckConstraint("ck_daily_price_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
                    table.CheckConstraint("ck_daily_price_revisions_bar_status", "bar_status IN ('Provisional', 'Confirmed', 'Corrected', 'Invalid')");
                    table.CheckConstraint("ck_daily_price_revisions_currency", "length(currency) = 3 AND currency = upper(currency)");
                    table.CheckConstraint("ck_daily_price_revisions_observation_recording_order", "first_observed_at_utc <= recorded_at_utc");
                    table.CheckConstraint("ck_daily_price_revisions_ohlc_range", "CAST(high AS NUMERIC) >= CAST(open AS NUMERIC) AND CAST(high AS NUMERIC) >= CAST(close AS NUMERIC) AND CAST(high AS NUMERIC) >= CAST(low AS NUMERIC) AND CAST(low AS NUMERIC) <= CAST(open AS NUMERIC) AND CAST(low AS NUMERIC) <= CAST(close AS NUMERIC)");
                    table.CheckConstraint("ck_daily_price_revisions_prices_positive", "CAST(open AS NUMERIC) > 0 AND CAST(high AS NUMERIC) > 0 AND CAST(low AS NUMERIC) > 0 AND CAST(close AS NUMERIC) > 0");
                    table.CheckConstraint("ck_daily_price_revisions_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
                    table.CheckConstraint("ck_daily_price_revisions_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_daily_price_revisions_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_daily_price_revisions_volume", "volume >= 0");
                    table.ForeignKey(
                        name: "fk_daily_price_revisions_daily_price_revisions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "daily_price_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_daily_price_revisions_daily_prices_daily_price_id",
                        column: x => x.daily_price_id,
                        principalTable: "daily_prices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_daily_price_revisions_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fundamental_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    available_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    availability_status = table.Column<string>(type: "TEXT", nullable: false),
                    first_observed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    fundamental_record_id = table.Column<string>(type: "TEXT", nullable: false),
                    as_of_date = table.Column<string>(type: "TEXT", nullable: false),
                    fiscal_period_end_date = table.Column<string>(type: "TEXT", nullable: true),
                    per = table.Column<string>(type: "TEXT", nullable: true),
                    pbr = table.Column<string>(type: "TEXT", nullable: true),
                    market_cap = table.Column<string>(type: "TEXT", nullable: true),
                    currency = table.Column<string>(type: "TEXT", nullable: true),
                    missing_fields_json = table.Column<string>(type: "TEXT", nullable: false),
                    payload_json = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fundamental_revisions", x => x.id);
                    table.CheckConstraint("ck_fundamental_revisions_availability", "(availability_status IN ('Known', 'Estimated') AND available_at_utc IS NOT NULL AND available_at_utc <= first_observed_at_utc) OR (availability_status = 'Unknown' AND available_at_utc IS NULL)");
                    table.CheckConstraint("ck_fundamental_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
                    table.CheckConstraint("ck_fundamental_revisions_currency", "(market_cap IS NULL AND currency IS NULL) OR (market_cap IS NOT NULL AND currency IS NOT NULL AND length(currency) = 3 AND currency = upper(currency))");
                    table.CheckConstraint("ck_fundamental_revisions_market_cap", "market_cap IS NULL OR CAST(market_cap AS NUMERIC) >= 0");
                    table.CheckConstraint("ck_fundamental_revisions_observation_recording_order", "first_observed_at_utc <= recorded_at_utc");
                    table.CheckConstraint("ck_fundamental_revisions_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
                    table.CheckConstraint("ck_fundamental_revisions_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_fundamental_revisions_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.ForeignKey(
                        name: "fk_fundamental_revisions_fundamental_records_fundamental_record_id",
                        column: x => x.fundamental_record_id,
                        principalTable: "fundamental_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fundamental_revisions_fundamental_revisions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "fundamental_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fundamental_revisions_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "instrument_identifier_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    available_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    availability_status = table.Column<string>(type: "TEXT", nullable: false),
                    first_observed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    instrument_identifier_id = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                    valid_from_date = table.Column<string>(type: "TEXT", nullable: true),
                    valid_to_date = table.Column<string>(type: "TEXT", nullable: true),
                    record_disposition = table.Column<string>(type: "TEXT", nullable: false),
                    change_kind = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instrument_identifier_revisions", x => x.id);
                    table.CheckConstraint("ck_instrument_identifier_revisions_availability", "(availability_status IN ('Known', 'Estimated') AND available_at_utc IS NOT NULL AND available_at_utc <= first_observed_at_utc) OR (availability_status = 'Unknown' AND available_at_utc IS NULL)");
                    table.CheckConstraint("ck_instrument_identifier_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
                    table.CheckConstraint("ck_instrument_identifier_revisions_change_kind", "change_kind IN ('Initial', 'ValidityChange', 'Correction', 'Void')");
                    table.CheckConstraint("ck_instrument_identifier_revisions_observation_recording_order", "first_observed_at_utc <= recorded_at_utc");
                    table.CheckConstraint("ck_instrument_identifier_revisions_record_disposition", "record_disposition IN ('Effective', 'Voided')");
                    table.CheckConstraint("ck_instrument_identifier_revisions_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
                    table.CheckConstraint("ck_instrument_identifier_revisions_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_instrument_identifier_revisions_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_instrument_identifier_revisions_valid_range", "valid_to_date IS NULL OR valid_from_date IS NULL OR valid_to_date >= valid_from_date");
                    table.ForeignKey(
                        name: "fk_instrument_identifier_revisions_instrument_identifier_revisions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "instrument_identifier_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_instrument_identifier_revisions_instrument_identifiers_instrument_identifier_id",
                        column: x => x.instrument_identifier_id,
                        principalTable: "instrument_identifiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_instrument_identifier_revisions_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "margin_eligibility_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    available_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    availability_status = table.Column<string>(type: "TEXT", nullable: false),
                    first_observed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    margin_eligibility_record_id = table.Column<string>(type: "TEXT", nullable: false),
                    effective_from_date = table.Column<string>(type: "TEXT", nullable: false),
                    effective_to_date = table.Column<string>(type: "TEXT", nullable: true),
                    standardized_margin_status = table.Column<string>(type: "TEXT", nullable: false),
                    loan_stock_status = table.Column<string>(type: "TEXT", nullable: false),
                    long_open_status = table.Column<string>(type: "TEXT", nullable: false),
                    short_open_status = table.Column<string>(type: "TEXT", nullable: false),
                    regulation_codes_json = table.Column<string>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_margin_eligibility_revisions", x => x.id);
                    table.CheckConstraint("ck_margin_eligibility_revisions_availability", "(availability_status IN ('Known', 'Estimated') AND available_at_utc IS NOT NULL AND available_at_utc <= first_observed_at_utc) OR (availability_status = 'Unknown' AND available_at_utc IS NULL)");
                    table.CheckConstraint("ck_margin_eligibility_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
                    table.CheckConstraint("ck_margin_eligibility_revisions_effective_range", "effective_to_date IS NULL OR effective_to_date >= effective_from_date");
                    table.CheckConstraint("ck_margin_eligibility_revisions_loan_stock_status", "loan_stock_status IN ('Eligible', 'Ineligible', 'Restricted', 'Unknown')");
                    table.CheckConstraint("ck_margin_eligibility_revisions_long_open_status", "long_open_status IN ('Allowed', 'Prohibited', 'Restricted', 'Unknown')");
                    table.CheckConstraint("ck_margin_eligibility_revisions_observation_recording_order", "first_observed_at_utc <= recorded_at_utc");
                    table.CheckConstraint("ck_margin_eligibility_revisions_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
                    table.CheckConstraint("ck_margin_eligibility_revisions_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_margin_eligibility_revisions_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_margin_eligibility_revisions_short_open_status", "short_open_status IN ('Allowed', 'Prohibited', 'Restricted', 'Unknown')");
                    table.CheckConstraint("ck_margin_eligibility_revisions_standardized_margin_status", "standardized_margin_status IN ('Eligible', 'Ineligible', 'Restricted', 'Unknown')");
                    table.ForeignKey(
                        name: "fk_margin_eligibility_revisions_margin_eligibility_records_margin_eligibility_record_id",
                        column: x => x.margin_eligibility_record_id,
                        principalTable: "margin_eligibility_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_margin_eligibility_revisions_margin_eligibility_revisions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "margin_eligibility_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_margin_eligibility_revisions_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "published_margin_cost_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    available_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    availability_status = table.Column<string>(type: "TEXT", nullable: false),
                    first_observed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    published_margin_cost_id = table.Column<string>(type: "TEXT", nullable: false),
                    application_date = table.Column<string>(type: "TEXT", nullable: false),
                    period_start_date = table.Column<string>(type: "TEXT", nullable: true),
                    period_end_date = table.Column<string>(type: "TEXT", nullable: true),
                    included_days = table.Column<long>(type: "INTEGER", nullable: true),
                    publication_status = table.Column<string>(type: "TEXT", nullable: false),
                    amount_per_share = table.Column<string>(type: "TEXT", nullable: true),
                    currency = table.Column<string>(type: "TEXT", nullable: true),
                    published_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    unit = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_published_margin_cost_revisions", x => x.id);
                    table.CheckConstraint("ck_published_margin_cost_revisions_amount", "(publication_status = 'KnownAmount' AND amount_per_share IS NOT NULL AND CAST(amount_per_share AS NUMERIC) > 0 AND currency IS NOT NULL) OR (publication_status = 'KnownZero' AND amount_per_share IS NOT NULL AND CAST(amount_per_share AS NUMERIC) = 0 AND currency IS NOT NULL) OR (publication_status NOT IN ('KnownAmount', 'KnownZero') AND amount_per_share IS NULL AND currency IS NULL)");
                    table.CheckConstraint("ck_published_margin_cost_revisions_availability", "(availability_status IN ('Known', 'Estimated') AND available_at_utc IS NOT NULL AND available_at_utc <= first_observed_at_utc) OR (availability_status = 'Unknown' AND available_at_utc IS NULL)");
                    table.CheckConstraint("ck_published_margin_cost_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
                    table.CheckConstraint("ck_published_margin_cost_revisions_currency", "currency IS NULL OR (length(currency) = 3 AND currency = upper(currency))");
                    table.CheckConstraint("ck_published_margin_cost_revisions_included_days", "included_days IS NULL OR included_days >= 0");
                    table.CheckConstraint("ck_published_margin_cost_revisions_observation_recording_order", "first_observed_at_utc <= recorded_at_utc");
                    table.CheckConstraint("ck_published_margin_cost_revisions_period", "period_end_date IS NULL OR period_start_date IS NULL OR period_end_date >= period_start_date");
                    table.CheckConstraint("ck_published_margin_cost_revisions_publication_status", "publication_status IN ('KnownAmount', 'KnownZero', 'NotOccurred', 'Unpublished', 'FetchFailed', 'Unknown')");
                    table.CheckConstraint("ck_published_margin_cost_revisions_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
                    table.CheckConstraint("ck_published_margin_cost_revisions_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_published_margin_cost_revisions_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.ForeignKey(
                        name: "fk_published_margin_cost_revisions_published_margin_cost_revisions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "published_margin_cost_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_published_margin_cost_revisions_published_margin_costs_published_margin_cost_id",
                        column: x => x.published_margin_cost_id,
                        principalTable: "published_margin_costs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_published_margin_cost_revisions_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "data_update_failures",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    data_update_run_id = table.Column<string>(type: "TEXT", nullable: false),
                    data_update_item_id = table.Column<string>(type: "TEXT", nullable: true),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: true),
                    item_key = table.Column<string>(type: "TEXT", nullable: true),
                    error_kind = table.Column<string>(type: "TEXT", nullable: false),
                    message = table.Column<string>(type: "TEXT", nullable: false),
                    occurred_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_update_failures", x => x.id);
                    table.CheckConstraint("ck_data_update_failures_error_kind", "error_kind IN ('Http', 'RateLimit', 'Timeout', 'InvalidData', 'MissingData', 'ProviderChanged', 'Cancelled', 'DatabaseLocked', 'Unknown')");
                    table.ForeignKey(
                        name: "fk_data_update_failures_data_update_items_data_update_item_id",
                        column: x => x.data_update_item_id,
                        principalTable: "data_update_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_data_update_failures_data_update_runs_data_update_run_id",
                        column: x => x.data_update_run_id,
                        principalTable: "data_update_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_data_update_failures_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "analysis_runs",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    evaluation_bar_date = table.Column<string>(type: "TEXT", nullable: false),
                    analyzed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_cutoff_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    run_mode = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    strategy_parameter_snapshot_id = table.Column<string>(type: "TEXT", nullable: false),
                    point_in_time_status = table.Column<string>(type: "TEXT", nullable: false),
                    price_selector_version = table.Column<string>(type: "TEXT", nullable: false),
                    adjustment_engine_version = table.Column<string>(type: "TEXT", nullable: false),
                    indicator_engine_version = table.Column<string>(type: "TEXT", nullable: false),
                    candidate_engine_version = table.Column<string>(type: "TEXT", nullable: false),
                    market_calendar_version_id = table.Column<string>(type: "TEXT", nullable: false),
                    application_version = table.Column<string>(type: "TEXT", nullable: false),
                    started_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    completed_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    total_count = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    success_count = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    failure_count = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    summary = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analysis_runs", x => x.id);
                    table.CheckConstraint("ck_analysis_runs_counts", "total_count >= 0 AND success_count >= 0 AND failure_count >= 0");
                    table.CheckConstraint("ck_analysis_runs_point_in_time_status", "point_in_time_status IN ('Verified', 'Unverified')");
                    table.CheckConstraint("ck_analysis_runs_run_mode", "run_mode IN ('Daily', 'Manual', 'Backtest')");
                    table.CheckConstraint("ck_analysis_runs_status", "status IN ('Queued', 'Running', 'Succeeded', 'PartiallySucceeded', 'Failed', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_analysis_runs_market_calendar_versions_market_calendar_version_id",
                        column: x => x.market_calendar_version_id,
                        principalTable: "market_calendar_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_analysis_runs_strategy_parameter_snapshots_strategy_parameter_snapshot_id",
                        column: x => x.strategy_parameter_snapshot_id,
                        principalTable: "strategy_parameter_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "market_calendar_days",
                columns: table => new
                {
                    trading_date = table.Column<string>(type: "TEXT", nullable: false),
                    market_calendar_version_id = table.Column<string>(type: "TEXT", nullable: false),
                    session_status = table.Column<string>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", nullable: true),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_calendar_days", x => new { x.market_calendar_version_id, x.trading_date });
                    table.CheckConstraint("ck_market_calendar_days_session_status", "session_status IN ('Open', 'Closed', 'HalfDay', 'UnscheduledClosure', 'Unknown')");
                    table.ForeignKey(
                        name: "fk_market_calendar_days_market_calendar_versions_market_calendar_version_id",
                        column: x => x.market_calendar_version_id,
                        principalTable: "market_calendar_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_market_calendar_days_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_revision_set_changes",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    price_revision_set_id = table.Column<string>(type: "TEXT", nullable: false),
                    operation = table.Column<string>(type: "TEXT", nullable: false),
                    daily_price_revision_id = table.Column<string>(type: "TEXT", nullable: true),
                    replaced_daily_price_revision_id = table.Column<string>(type: "TEXT", nullable: true),
                    bar_date = table.Column<string>(type: "TEXT", nullable: false),
                    ordinal = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_revision_set_changes", x => x.id);
                    table.CheckConstraint("ck_price_revision_set_changes_operation", "operation IN ('Add', 'Replace', 'Remove')");
                    table.CheckConstraint("ck_price_revision_set_changes_ordinal", "ordinal >= 0");
                    table.CheckConstraint("ck_price_revision_set_changes_revisions", "(operation = 'Add' AND daily_price_revision_id IS NOT NULL AND replaced_daily_price_revision_id IS NULL) OR (operation = 'Replace' AND daily_price_revision_id IS NOT NULL AND replaced_daily_price_revision_id IS NOT NULL) OR (operation = 'Remove' AND daily_price_revision_id IS NULL AND replaced_daily_price_revision_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_price_revision_set_changes_daily_price_revisions_daily_price_revision_id",
                        column: x => x.daily_price_revision_id,
                        principalTable: "daily_price_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_revision_set_changes_daily_price_revisions_replaced_daily_price_revision_id",
                        column: x => x.replaced_daily_price_revision_id,
                        principalTable: "daily_price_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_revision_set_changes_price_revision_sets_price_revision_set_id",
                        column: x => x.price_revision_set_id,
                        principalTable: "price_revision_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "analysis_input_manifests",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    analysis_run_id = table.Column<string>(type: "TEXT", nullable: false),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                    price_provider = table.Column<string>(type: "TEXT", nullable: false),
                    price_revision_set_id = table.Column<string>(type: "TEXT", nullable: false),
                    first_bar_date = table.Column<string>(type: "TEXT", nullable: true),
                    last_bar_date = table.Column<string>(type: "TEXT", nullable: true),
                    bar_count = table.Column<long>(type: "INTEGER", nullable: false),
                    required_bar_count = table.Column<long>(type: "INTEGER", nullable: false),
                    history_status = table.Column<string>(type: "TEXT", nullable: false),
                    point_in_time_status = table.Column<string>(type: "TEXT", nullable: false),
                    selection_basis = table.Column<string>(type: "TEXT", nullable: false),
                    selection_rule_version = table.Column<string>(type: "TEXT", nullable: false),
                    selected_recorded_cutoff_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    selected_available_cutoff_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    price_revision_set_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    corporate_action_set_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    manifest_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analysis_input_manifests", x => x.id);
                    table.CheckConstraint("ck_analysis_input_manifests_action_set_sha256", "length(corporate_action_set_sha256) = 64 AND corporate_action_set_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_analysis_input_manifests_bar_counts", "bar_count >= 0 AND required_bar_count >= 0");
                    table.CheckConstraint("ck_analysis_input_manifests_bar_range", "last_bar_date IS NULL OR first_bar_date IS NULL OR last_bar_date >= first_bar_date");
                    table.CheckConstraint("ck_analysis_input_manifests_empty_range", "(bar_count = 0 AND first_bar_date IS NULL AND last_bar_date IS NULL) OR (bar_count > 0 AND first_bar_date IS NOT NULL AND last_bar_date IS NOT NULL)");
                    table.CheckConstraint("ck_analysis_input_manifests_history_status", "history_status IN ('Complete', 'InsufficientHistory', 'HistoryIncomplete', 'Invalid')");
                    table.CheckConstraint("ck_analysis_input_manifests_manifest_sha256", "length(manifest_sha256) = 64 AND manifest_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_analysis_input_manifests_point_in_time_status", "point_in_time_status IN ('Verified', 'Unverified')");
                    table.CheckConstraint("ck_analysis_input_manifests_price_set_sha256", "length(price_revision_set_sha256) = 64 AND price_revision_set_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_analysis_input_manifests_selection_basis", "selection_basis IN ('ObservedAt', 'SourceAvailableAt')");
                    table.ForeignKey(
                        name: "fk_analysis_input_manifests_analysis_runs_analysis_run_id",
                        column: x => x.analysis_run_id,
                        principalTable: "analysis_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_analysis_input_manifests_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_analysis_input_manifests_price_revision_sets_price_revision_set_id",
                        column: x => x.price_revision_set_id,
                        principalTable: "price_revision_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "analysis_action_applications",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    analysis_input_manifest_id = table.Column<string>(type: "TEXT", nullable: false),
                    corporate_action_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                    application_status = table.Column<string>(type: "TEXT", nullable: false),
                    reference_price_revision_id = table.Column<string>(type: "TEXT", nullable: true),
                    price_factor = table.Column<string>(type: "TEXT", nullable: true),
                    volume_factor = table.Column<string>(type: "TEXT", nullable: true),
                    cumulative_price_factor = table.Column<string>(type: "TEXT", nullable: true),
                    cumulative_volume_factor = table.Column<string>(type: "TEXT", nullable: true),
                    reason = table.Column<string>(type: "TEXT", nullable: false),
                    ordinal = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analysis_action_applications", x => x.id);
                    table.CheckConstraint("ck_analysis_action_applications_factors", "(price_factor IS NULL OR CAST(price_factor AS NUMERIC) > 0) AND (volume_factor IS NULL OR CAST(volume_factor AS NUMERIC) > 0) AND (cumulative_price_factor IS NULL OR CAST(cumulative_price_factor AS NUMERIC) > 0) AND (cumulative_volume_factor IS NULL OR CAST(cumulative_volume_factor AS NUMERIC) > 0)");
                    table.CheckConstraint("ck_analysis_action_applications_ordinal", "ordinal >= 0");
                    table.CheckConstraint("ck_analysis_action_applications_status", "application_status IN ('Applied', 'ExcludedNotEffective', 'ExcludedUnavailable', 'Unsupported', 'ReconciliationRequired')");
                    table.ForeignKey(
                        name: "fk_analysis_action_applications_analysis_input_manifests_analysis_input_manifest_id",
                        column: x => x.analysis_input_manifest_id,
                        principalTable: "analysis_input_manifests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_analysis_action_applications_corporate_action_revisions_corporate_action_revision_id",
                        column: x => x.corporate_action_revision_id,
                        principalTable: "corporate_action_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_analysis_action_applications_daily_price_revisions_reference_price_revision_id",
                        column: x => x.reference_price_revision_id,
                        principalTable: "daily_price_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "technical_analysis_results",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    analysis_run_id = table.Column<string>(type: "TEXT", nullable: false),
                    analysis_input_manifest_id = table.Column<string>(type: "TEXT", nullable: false),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                    position_side = table.Column<string>(type: "TEXT", nullable: false),
                    signal_purpose = table.Column<string>(type: "TEXT", nullable: false),
                    outcome = table.Column<string>(type: "TEXT", nullable: false),
                    reason_summary = table.Column<string>(type: "TEXT", nullable: false),
                    reasons_json = table.Column<string>(type: "TEXT", nullable: false),
                    calculation_start_bar_date = table.Column<string>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technical_analysis_results", x => x.id);
                    table.CheckConstraint("ck_technical_analysis_results_outcome", "outcome IN ('Candidate', 'NotCandidate', 'InsufficientHistory', 'HistoryIncomplete', 'InvalidData', 'PointInTimeUnverified', 'ReconciliationRequired', 'Failed')");
                    table.CheckConstraint("ck_technical_analysis_results_position_side", "position_side IN ('Long', 'Short')");
                    table.CheckConstraint("ck_technical_analysis_results_signal_purpose", "signal_purpose = 'Entry'");
                    table.ForeignKey(
                        name: "fk_technical_analysis_results_analysis_input_manifests_analysis_input_manifest_id",
                        column: x => x.analysis_input_manifest_id,
                        principalTable: "analysis_input_manifests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_technical_analysis_results_analysis_runs_analysis_run_id",
                        column: x => x.analysis_run_id,
                        principalTable: "analysis_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_technical_analysis_results_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate_results",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    technical_analysis_result_id = table.Column<string>(type: "TEXT", nullable: false),
                    score = table.Column<long>(type: "INTEGER", nullable: false),
                    confidence = table.Column<string>(type: "TEXT", nullable: false),
                    primary_reason = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_results", x => x.id);
                    table.CheckConstraint("ck_candidate_results_confidence", "confidence IN ('High', 'Medium', 'Low')");
                    table.CheckConstraint("ck_candidate_results_score", "score BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_candidate_results_technical_analysis_results_technical_analysis_result_id",
                        column: x => x.technical_analysis_result_id,
                        principalTable: "technical_analysis_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "indicator_results",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    technical_analysis_result_id = table.Column<string>(type: "TEXT", nullable: false),
                    indicator_key = table.Column<string>(type: "TEXT", nullable: false),
                    algorithm_id = table.Column<string>(type: "TEXT", nullable: false),
                    parameters_json = table.Column<string>(type: "TEXT", nullable: false),
                    values_json = table.Column<string>(type: "TEXT", nullable: false),
                    calculation_start_bar_date = table.Column<string>(type: "TEXT", nullable: false),
                    input_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    ordinal = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_indicator_results", x => x.id);
                    table.CheckConstraint("ck_indicator_results_input_sha256", "length(input_sha256) = 64 AND input_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_indicator_results_ordinal", "ordinal >= 0");
                    table.ForeignKey(
                        name: "fk_indicator_results_technical_analysis_results_technical_analysis_result_id",
                        column: x => x.technical_analysis_result_id,
                        principalTable: "technical_analysis_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_check_jobs",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    candidate_result_id = table.Column<string>(type: "TEXT", nullable: false),
                    request_origin = table.Column<string>(type: "TEXT", nullable: false),
                    priority = table.Column<long>(type: "INTEGER", nullable: false),
                    candidate_side = table.Column<string>(type: "TEXT", nullable: false),
                    evaluation_bar_date = table.Column<string>(type: "TEXT", nullable: false),
                    input_snapshot_json = table.Column<string>(type: "TEXT", nullable: false),
                    input_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    technical_manifest_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    strategy_snapshot_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    prompt_template_snapshot_id = table.Column<string>(type: "TEXT", nullable: false),
                    ai_profile_snapshot_id = table.Column<string>(type: "TEXT", nullable: false),
                    automatic_selection_rank = table.Column<long>(type: "INTEGER", nullable: true),
                    selection_policy_version = table.Column<string>(type: "TEXT", nullable: true),
                    automatic_configuration_json = table.Column<string>(type: "TEXT", nullable: true),
                    automatic_configuration_sha256 = table.Column<string>(type: "TEXT", nullable: true),
                    requested_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_check_jobs", x => x.id);
                    table.CheckConstraint("ck_ai_check_jobs_automatic_configuration_sha256", "automatic_configuration_sha256 IS NULL OR (length(automatic_configuration_sha256) = 64 AND automatic_configuration_sha256 NOT GLOB '*[^0-9a-f]*')");
                    table.CheckConstraint("ck_ai_check_jobs_automatic_fields", "(request_origin = 'Automatic' AND automatic_selection_rank > 0 AND selection_policy_version IS NOT NULL AND automatic_configuration_json IS NOT NULL AND automatic_configuration_sha256 IS NOT NULL) OR (request_origin = 'User' AND automatic_selection_rank IS NULL AND selection_policy_version IS NULL AND automatic_configuration_json IS NULL AND automatic_configuration_sha256 IS NULL)");
                    table.CheckConstraint("ck_ai_check_jobs_candidate_side", "candidate_side IN ('Long', 'Short')");
                    table.CheckConstraint("ck_ai_check_jobs_input_sha256", "length(input_sha256) = 64 AND input_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_ai_check_jobs_request_origin", "request_origin IN ('User', 'Automatic')");
                    table.CheckConstraint("ck_ai_check_jobs_strategy_snapshot_sha256", "length(strategy_snapshot_sha256) = 64 AND strategy_snapshot_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_ai_check_jobs_technical_manifest_sha256", "length(technical_manifest_sha256) = 64 AND technical_manifest_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.ForeignKey(
                        name: "fk_ai_check_jobs_ai_profile_snapshots_ai_profile_snapshot_id",
                        column: x => x.ai_profile_snapshot_id,
                        principalTable: "ai_profile_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ai_check_jobs_candidate_results_candidate_result_id",
                        column: x => x.candidate_result_id,
                        principalTable: "candidate_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ai_check_jobs_prompt_template_snapshots_prompt_template_snapshot_id",
                        column: x => x.prompt_template_snapshot_id,
                        principalTable: "prompt_template_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate_score_components",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    candidate_result_id = table.Column<string>(type: "TEXT", nullable: false),
                    component_key = table.Column<string>(type: "TEXT", nullable: false),
                    matched = table.Column<bool>(type: "INTEGER", nullable: false),
                    raw_value_json = table.Column<string>(type: "TEXT", nullable: false),
                    weight = table.Column<string>(type: "TEXT", nullable: false),
                    awarded_score = table.Column<string>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", nullable: false),
                    ordinal = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_score_components", x => x.id);
                    table.CheckConstraint("ck_candidate_score_components_matched", "matched IN (0, 1)");
                    table.CheckConstraint("ck_candidate_score_components_ordinal", "ordinal >= 0");
                    table.ForeignKey(
                        name: "fk_candidate_score_components_candidate_results_candidate_result_id",
                        column: x => x.candidate_result_id,
                        principalTable: "candidate_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                    position_side = table.Column<string>(type: "TEXT", nullable: false),
                    strategy_parameter_snapshot_id = table.Column<string>(type: "TEXT", nullable: true),
                    origin_candidate_result_id = table.Column<string>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions", x => x.id);
                    table.CheckConstraint("ck_positions_position_side", "position_side IN ('Long', 'Short')");
                    table.ForeignKey(
                        name: "fk_positions_candidate_results_origin_candidate_result_id",
                        column: x => x.origin_candidate_result_id,
                        principalTable: "candidate_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_positions_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_positions_strategy_parameter_snapshots_strategy_parameter_snapshot_id",
                        column: x => x.strategy_parameter_snapshot_id,
                        principalTable: "strategy_parameter_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_attempts",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    ai_check_job_id = table.Column<string>(type: "TEXT", nullable: false),
                    attempt_no = table.Column<long>(type: "INTEGER", nullable: false),
                    attempt_kind = table.Column<string>(type: "TEXT", nullable: false),
                    request_origin = table.Column<string>(type: "TEXT", nullable: false),
                    requested_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    priority_at_queue = table.Column<long>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    queued_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    started_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    completed_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    cli_version = table.Column<string>(type: "TEXT", nullable: true),
                    actual_model = table.Column<string>(type: "TEXT", nullable: true),
                    timeout_seconds = table.Column<int>(type: "INTEGER", nullable: false),
                    arguments_json = table.Column<string>(type: "TEXT", nullable: false),
                    exit_code = table.Column<int>(type: "INTEGER", nullable: true),
                    error_kind = table.Column<string>(type: "TEXT", nullable: true),
                    error_message = table.Column<string>(type: "TEXT", nullable: true),
                    sanitized_stderr = table.Column<string>(type: "TEXT", nullable: true),
                    raw_response_sha256 = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_attempts", x => x.id);
                    table.CheckConstraint("ck_ai_attempts_attempt_kind", "attempt_kind IN ('Initial', 'Retry', 'Recheck')");
                    table.CheckConstraint("ck_ai_attempts_attempt_no", "attempt_no > 0");
                    table.CheckConstraint("ck_ai_attempts_error_kind", "error_kind IS NULL OR error_kind IN ('CliFailure', 'Timeout', 'Cancelled', 'Interrupted', 'InvalidResponse', 'ParseFailure', 'Unknown')");
                    table.CheckConstraint("ck_ai_attempts_initial_kind", "(attempt_no = 1 AND attempt_kind = 'Initial') OR (attempt_no > 1 AND attempt_kind IN ('Retry', 'Recheck'))");
                    table.CheckConstraint("ck_ai_attempts_raw_response_sha256", "raw_response_sha256 IS NULL OR (length(raw_response_sha256) = 64 AND raw_response_sha256 NOT GLOB '*[^0-9a-f]*')");
                    table.CheckConstraint("ck_ai_attempts_request_origin", "request_origin IN ('User', 'Automatic')");
                    table.CheckConstraint("ck_ai_attempts_status", "status IN ('Queued', 'Running', 'Succeeded', 'Failed', 'TimedOut', 'InsufficientInformation', 'Cancelled')");
                    table.CheckConstraint("ck_ai_attempts_timeout_seconds", "timeout_seconds > 0");
                    table.ForeignKey(
                        name: "fk_ai_attempts_ai_check_jobs_ai_check_job_id",
                        column: x => x.ai_check_job_id,
                        principalTable: "ai_check_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_job_request_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    ai_check_job_id = table.Column<string>(type: "TEXT", nullable: false),
                    event_kind = table.Column<string>(type: "TEXT", nullable: false),
                    request_origin = table.Column<string>(type: "TEXT", nullable: false),
                    requested_priority = table.Column<long>(type: "INTEGER", nullable: false),
                    requested_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    ordinal = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_job_request_events", x => x.id);
                    table.CheckConstraint("ck_ai_job_request_events_event_kind", "event_kind IN ('InitialRequest', 'PriorityPromotion', 'RetryRequest', 'RecheckRequest')");
                    table.CheckConstraint("ck_ai_job_request_events_initial_kind", "(ordinal = 1 AND event_kind = 'InitialRequest') OR (ordinal > 1 AND event_kind <> 'InitialRequest')");
                    table.CheckConstraint("ck_ai_job_request_events_ordinal", "ordinal > 0");
                    table.CheckConstraint("ck_ai_job_request_events_request_origin", "request_origin IN ('User', 'Automatic')");
                    table.ForeignKey(
                        name: "fk_ai_job_request_events_ai_check_jobs_ai_check_job_id",
                        column: x => x.ai_check_job_id,
                        principalTable: "ai_check_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_evaluation_input_manifests",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    analysis_run_id = table.Column<string>(type: "TEXT", nullable: false),
                    position_id = table.Column<string>(type: "TEXT", nullable: false),
                    analysis_input_manifest_id = table.Column<string>(type: "TEXT", nullable: false),
                    current_price_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                    trade_execution_revision_ids_json = table.Column<string>(type: "TEXT", nullable: false),
                    lot_allocation_revision_ids_json = table.Column<string>(type: "TEXT", nullable: false),
                    position_adjustment_ids_json = table.Column<string>(type: "TEXT", nullable: false),
                    contract_revision_ids_json = table.Column<string>(type: "TEXT", nullable: false),
                    risk_basis_snapshot_ids_json = table.Column<string>(type: "TEXT", nullable: false),
                    risk_plan_revision_ids_json = table.Column<string>(type: "TEXT", nullable: false),
                    margin_cost_observation_ids_json = table.Column<string>(type: "TEXT", nullable: false),
                    projection_version = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_cutoff_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    manifest_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_evaluation_input_manifests", x => x.id);
                    table.CheckConstraint("ck_position_evaluation_input_manifests_manifest_sha256", "length(manifest_sha256) = 64 AND manifest_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.ForeignKey(
                        name: "fk_position_evaluation_input_manifests_analysis_input_manifests_analysis_input_manifest_id",
                        column: x => x.analysis_input_manifest_id,
                        principalTable: "analysis_input_manifests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_evaluation_input_manifests_analysis_runs_analysis_run_id",
                        column: x => x.analysis_run_id,
                        principalTable: "analysis_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_evaluation_input_manifests_daily_price_revisions_current_price_revision_id",
                        column: x => x.current_price_revision_id,
                        principalTable: "daily_price_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_evaluation_input_manifests_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_state_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    position_id = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    reconciliation_status = table.Column<string>(type: "TEXT", nullable: false),
                    effective_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    memo = table.Column<string>(type: "TEXT", nullable: true),
                    reason = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_state_revisions", x => x.id);
                    table.CheckConstraint("ck_position_state_revisions_content_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_position_state_revisions_reconciliation_status", "reconciliation_status IN ('Clear', 'Required', 'InProgress', 'Resolved')");
                    table.CheckConstraint("ck_position_state_revisions_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
                    table.CheckConstraint("ck_position_state_revisions_revision_no", "revision_no > 0");
                    table.CheckConstraint("ck_position_state_revisions_status", "status IN ('Open', 'Closed', 'Archived')");
                    table.ForeignKey(
                        name: "fk_position_state_revisions_position_state_revisions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "position_state_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_state_revisions_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trade_executions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    position_id = table.Column<string>(type: "TEXT", nullable: false),
                    execution_kind = table.Column<string>(type: "TEXT", nullable: false),
                    origin = table.Column<string>(type: "TEXT", nullable: false),
                    candidate_context_id = table.Column<string>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trade_executions", x => x.id);
                    table.CheckConstraint("ck_trade_executions_execution_kind", "execution_kind IN ('Open', 'Close')");
                    table.CheckConstraint("ck_trade_executions_origin", "origin = 'UserConfirmed'");
                    table.ForeignKey(
                        name: "fk_trade_executions_candidate_results_candidate_context_id",
                        column: x => x.candidate_context_id,
                        principalTable: "candidate_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_executions_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_attempt_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    ai_attempt_id = table.Column<string>(type: "TEXT", nullable: false),
                    from_status = table.Column<string>(type: "TEXT", nullable: true),
                    to_status = table.Column<string>(type: "TEXT", nullable: false),
                    occurred_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", nullable: true),
                    ordinal = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_attempt_events", x => x.id);
                    table.CheckConstraint("ck_ai_attempt_events_from_status", "from_status IS NULL OR from_status IN ('Queued', 'Running', 'Succeeded', 'Failed', 'TimedOut', 'InsufficientInformation', 'Cancelled')");
                    table.CheckConstraint("ck_ai_attempt_events_initial", "(ordinal = 1 AND from_status IS NULL AND to_status = 'Queued') OR (ordinal > 1 AND from_status IS NOT NULL)");
                    table.CheckConstraint("ck_ai_attempt_events_ordinal", "ordinal > 0");
                    table.CheckConstraint("ck_ai_attempt_events_to_status", "to_status IN ('Queued', 'Running', 'Succeeded', 'Failed', 'TimedOut', 'InsufficientInformation', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_ai_attempt_events_ai_attempts_ai_attempt_id",
                        column: x => x.ai_attempt_id,
                        principalTable: "ai_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_results",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    ai_attempt_id = table.Column<string>(type: "TEXT", nullable: false),
                    schema_version = table.Column<string>(type: "TEXT", nullable: false),
                    parser_version = table.Column<string>(type: "TEXT", nullable: false),
                    verdict = table.Column<string>(type: "TEXT", nullable: true),
                    confidence = table.Column<string>(type: "TEXT", nullable: true),
                    summary = table.Column<string>(type: "TEXT", nullable: true),
                    technical_view = table.Column<string>(type: "TEXT", nullable: true),
                    fundamental_view = table.Column<string>(type: "TEXT", nullable: true),
                    positive_factors_json = table.Column<string>(type: "TEXT", nullable: false),
                    risk_factors_json = table.Column<string>(type: "TEXT", nullable: false),
                    invalidation_conditions_json = table.Column<string>(type: "TEXT", nullable: false),
                    checked_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    structured_result_json = table.Column<string>(type: "TEXT", nullable: false),
                    structured_result_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_results", x => x.id);
                    table.CheckConstraint("ck_ai_results_confidence", "confidence IS NULL OR confidence IN ('High', 'Medium', 'Low')");
                    table.CheckConstraint("ck_ai_results_structured_result_sha256", "length(structured_result_sha256) = 64 AND structured_result_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_ai_results_verdict", "verdict IS NULL OR verdict IN ('Bullish', 'Neutral', 'Bearish')");
                    table.ForeignKey(
                        name: "fk_ai_results_ai_attempts_ai_attempt_id",
                        column: x => x.ai_attempt_id,
                        principalTable: "ai_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_evaluations",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    analysis_run_id = table.Column<string>(type: "TEXT", nullable: false),
                    position_id = table.Column<string>(type: "TEXT", nullable: false),
                    position_evaluation_input_manifest_id = table.Column<string>(type: "TEXT", nullable: false),
                    evaluation_bar_date = table.Column<string>(type: "TEXT", nullable: false),
                    exit_decision = table.Column<string>(type: "TEXT", nullable: false),
                    reason_summary = table.Column<string>(type: "TEXT", nullable: false),
                    reasons_json = table.Column<string>(type: "TEXT", nullable: false),
                    lot_evaluations_json = table.Column<string>(type: "TEXT", nullable: false),
                    current_quantity = table.Column<string>(type: "TEXT", nullable: false),
                    price_pnl = table.Column<string>(type: "TEXT", nullable: true),
                    confirmed_cost_pnl = table.Column<string>(type: "TEXT", nullable: true),
                    estimated_net_pnl = table.Column<string>(type: "TEXT", nullable: true),
                    cost_to_r_ratio = table.Column<string>(type: "TEXT", nullable: true),
                    partial_exit_quantity = table.Column<long>(type: "INTEGER", nullable: true),
                    partial_exit_status = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_evaluations", x => x.id);
                    table.CheckConstraint("ck_position_evaluations_exit_decision", "exit_decision IN ('Hold', 'TakeProfit', 'StopLoss', 'Exit')");
                    table.CheckConstraint("ck_position_evaluations_partial_exit_quantity", "(partial_exit_status = 'Candidate' AND partial_exit_quantity > 0) OR (partial_exit_status IN ('NotApplicable', 'NotFeasible') AND partial_exit_quantity IS NULL)");
                    table.CheckConstraint("ck_position_evaluations_partial_exit_status", "partial_exit_status IN ('NotApplicable', 'Candidate', 'NotFeasible')");
                    table.ForeignKey(
                        name: "fk_position_evaluations_analysis_runs_analysis_run_id",
                        column: x => x.analysis_run_id,
                        principalTable: "analysis_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_evaluations_position_evaluation_input_manifests_position_evaluation_input_manifest_id",
                        column: x => x.position_evaluation_input_manifest_id,
                        principalTable: "position_evaluation_input_manifests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_evaluations_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trade_execution_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    trade_execution_id = table.Column<string>(type: "TEXT", nullable: false),
                    executed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    price = table.Column<string>(type: "TEXT", nullable: false),
                    quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    currency = table.Column<string>(type: "TEXT", nullable: false),
                    record_disposition = table.Column<string>(type: "TEXT", nullable: false),
                    change_kind = table.Column<string>(type: "TEXT", nullable: false),
                    broker = table.Column<string>(type: "TEXT", nullable: true),
                    external_reference = table.Column<string>(type: "TEXT", nullable: true),
                    user_note = table.Column<string>(type: "TEXT", nullable: true),
                    user_confirmed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    correction_reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trade_execution_revisions", x => x.id);
                    table.CheckConstraint("ck_trade_execution_revisions_change_kind", "change_kind IN ('Initial', 'Correction', 'Void')");
                    table.CheckConstraint("ck_trade_execution_revisions_content_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_trade_execution_revisions_correction_reason", "(revision_no = 1 AND record_disposition = 'Effective' AND change_kind = 'Initial') OR (revision_no > 1 AND correction_reason IS NOT NULL)");
                    table.CheckConstraint("ck_trade_execution_revisions_currency", "length(currency) = 3 AND currency = upper(currency)");
                    table.CheckConstraint("ck_trade_execution_revisions_disposition_change", "(change_kind = 'Void' AND record_disposition = 'Voided') OR (change_kind IN ('Initial', 'Correction') AND record_disposition = 'Effective')");
                    table.CheckConstraint("ck_trade_execution_revisions_price", "CAST(price AS NUMERIC) > 0");
                    table.CheckConstraint("ck_trade_execution_revisions_quantity", "quantity > 0");
                    table.CheckConstraint("ck_trade_execution_revisions_record_disposition", "record_disposition IN ('Effective', 'Voided')");
                    table.CheckConstraint("ck_trade_execution_revisions_revision_kind", "(revision_no = 1 AND supersedes_id IS NULL AND change_kind = 'Initial') OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id AND change_kind IN ('Correction', 'Void'))");
                    table.CheckConstraint("ck_trade_execution_revisions_revision_no", "revision_no > 0");
                    table.ForeignKey(
                        name: "fk_trade_execution_revisions_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_execution_revisions_trade_execution_revisions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "trade_execution_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_execution_revisions_trade_executions_trade_execution_id",
                        column: x => x.trade_execution_id,
                        principalTable: "trade_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_result_sources",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    ai_result_id = table.Column<string>(type: "TEXT", nullable: false),
                    url = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: true),
                    published_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    retrieved_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    ordinal = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_result_sources", x => x.id);
                    table.CheckConstraint("ck_ai_result_sources_ordinal", "ordinal >= 0");
                    table.ForeignKey(
                        name: "fk_ai_result_sources_ai_results_ai_result_id",
                        column: x => x.ai_result_id,
                        principalTable: "ai_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "margin_lots",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    position_id = table.Column<string>(type: "TEXT", nullable: false),
                    opening_trade_execution_id = table.Column<string>(type: "TEXT", nullable: false),
                    initial_opening_trade_execution_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_margin_lots", x => x.id);
                    table.ForeignKey(
                        name: "fk_margin_lots_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_margin_lots_trade_execution_revisions_initial_opening_trade_execution_revision_id",
                        column: x => x.initial_opening_trade_execution_revision_id,
                        principalTable: "trade_execution_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_margin_lots_trade_executions_opening_trade_execution_id",
                        column: x => x.opening_trade_execution_id,
                        principalTable: "trade_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lot_allocation_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    allocation_key = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    closing_trade_execution_id = table.Column<string>(type: "TEXT", nullable: false),
                    closing_trade_execution_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                    margin_lot_id = table.Column<string>(type: "TEXT", nullable: false),
                    quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    record_disposition = table.Column<string>(type: "TEXT", nullable: false),
                    change_kind = table.Column<string>(type: "TEXT", nullable: false),
                    user_confirmed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    correction_reason = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lot_allocation_revisions", x => x.id);
                    table.CheckConstraint("ck_lot_allocation_revisions_change_kind", "change_kind IN ('Initial', 'Correction', 'Void')");
                    table.CheckConstraint("ck_lot_allocation_revisions_content_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_lot_allocation_revisions_correction_reason", "(revision_no = 1 AND record_disposition = 'Effective' AND change_kind = 'Initial') OR (revision_no > 1 AND correction_reason IS NOT NULL)");
                    table.CheckConstraint("ck_lot_allocation_revisions_disposition_change", "(change_kind = 'Void' AND record_disposition = 'Voided') OR (change_kind IN ('Initial', 'Correction') AND record_disposition = 'Effective')");
                    table.CheckConstraint("ck_lot_allocation_revisions_quantity", "quantity > 0");
                    table.CheckConstraint("ck_lot_allocation_revisions_record_disposition", "record_disposition IN ('Effective', 'Voided')");
                    table.CheckConstraint("ck_lot_allocation_revisions_revision_kind", "(revision_no = 1 AND supersedes_id IS NULL AND change_kind = 'Initial') OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id AND change_kind IN ('Correction', 'Void'))");
                    table.CheckConstraint("ck_lot_allocation_revisions_revision_no", "revision_no > 0");
                    table.ForeignKey(
                        name: "fk_lot_allocation_revisions_lot_allocation_revisions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "lot_allocation_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lot_allocation_revisions_margin_lots_margin_lot_id",
                        column: x => x.margin_lot_id,
                        principalTable: "margin_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lot_allocation_revisions_trade_execution_revisions_closing_trade_execution_revision_id",
                        column: x => x.closing_trade_execution_revision_id,
                        principalTable: "trade_execution_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lot_allocation_revisions_trade_executions_closing_trade_execution_id",
                        column: x => x.closing_trade_execution_id,
                        principalTable: "trade_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "margin_cost_items",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    margin_lot_id = table.Column<string>(type: "TEXT", nullable: false),
                    cost_type = table.Column<string>(type: "TEXT", nullable: false),
                    occurrence_key = table.Column<string>(type: "TEXT", nullable: false),
                    period_start_date = table.Column<string>(type: "TEXT", nullable: false),
                    period_end_date = table.Column<string>(type: "TEXT", nullable: false),
                    broker_statement_line_id = table.Column<string>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_margin_cost_items", x => x.id);
                    table.CheckConstraint("ck_margin_cost_items_cost_type", "cost_type IN ('BuyerInterest', 'StockLendingFee', 'Backwardation', 'DividendEquivalent', 'BrokerSpecific', 'Other')");
                    table.CheckConstraint("ck_margin_cost_items_period", "period_end_date >= period_start_date");
                    table.ForeignKey(
                        name: "fk_margin_cost_items_margin_lots_margin_lot_id",
                        column: x => x.margin_lot_id,
                        principalTable: "margin_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "margin_lot_contract_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    margin_lot_id = table.Column<string>(type: "TEXT", nullable: false),
                    opening_trade_execution_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                    margin_type = table.Column<string>(type: "TEXT", nullable: false),
                    broker = table.Column<string>(type: "TEXT", nullable: false),
                    product_name = table.Column<string>(type: "TEXT", nullable: false),
                    effective_from_date = table.Column<string>(type: "TEXT", nullable: false),
                    effective_to_date = table.Column<string>(type: "TEXT", nullable: true),
                    term_type = table.Column<string>(type: "TEXT", nullable: false),
                    final_repayment_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    buyer_interest_rate = table.Column<string>(type: "TEXT", nullable: true),
                    stock_lending_rate = table.Column<string>(type: "TEXT", nullable: true),
                    rate_unit = table.Column<string>(type: "TEXT", nullable: true),
                    contract_currency = table.Column<string>(type: "TEXT", nullable: false),
                    day_count_convention = table.Column<string>(type: "TEXT", nullable: true),
                    special_fee_policy_json = table.Column<string>(type: "TEXT", nullable: false),
                    rights_processing_json = table.Column<string>(type: "TEXT", nullable: false),
                    confirmed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    evidence = table.Column<string>(type: "TEXT", nullable: false),
                    change_kind = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_margin_lot_contract_revisions", x => x.id);
                    table.CheckConstraint("ck_margin_lot_contract_revisions_change_kind", "change_kind IN ('Initial', 'ContractAmendment', 'InputCorrection')");
                    table.CheckConstraint("ck_margin_lot_contract_revisions_content_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_margin_lot_contract_revisions_currency", "length(contract_currency) = 3 AND contract_currency = upper(contract_currency)");
                    table.CheckConstraint("ck_margin_lot_contract_revisions_effective_dates", "effective_to_date IS NULL OR effective_to_date >= effective_from_date");
                    table.CheckConstraint("ck_margin_lot_contract_revisions_margin_type", "margin_type IN ('Standardized', 'General', 'Unknown')");
                    table.CheckConstraint("ck_margin_lot_contract_revisions_revision_kind", "(revision_no = 1 AND supersedes_id IS NULL AND change_kind = 'Initial') OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id AND change_kind IN ('ContractAmendment', 'InputCorrection'))");
                    table.CheckConstraint("ck_margin_lot_contract_revisions_revision_no", "revision_no > 0");
                    table.CheckConstraint("ck_margin_lot_contract_revisions_term_deadline", "(term_type = 'FixedDate' AND final_repayment_at_utc IS NOT NULL) OR (term_type IN ('NoFixedTerm', 'Unknown') AND final_repayment_at_utc IS NULL)");
                    table.CheckConstraint("ck_margin_lot_contract_revisions_term_type", "term_type IN ('FixedDate', 'NoFixedTerm', 'Unknown')");
                    table.ForeignKey(
                        name: "fk_margin_lot_contract_revisions_margin_lot_contract_revisions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "margin_lot_contract_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_margin_lot_contract_revisions_margin_lots_margin_lot_id",
                        column: x => x.margin_lot_id,
                        principalTable: "margin_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_margin_lot_contract_revisions_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_margin_lot_contract_revisions_trade_execution_revisions_opening_trade_execution_revision_id",
                        column: x => x.opening_trade_execution_revision_id,
                        principalTable: "trade_execution_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_adjustments",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    adjustment_key = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    replaces_adjustment_key = table.Column<string>(type: "TEXT", nullable: true),
                    position_id = table.Column<string>(type: "TEXT", nullable: false),
                    margin_lot_id = table.Column<string>(type: "TEXT", nullable: false),
                    corporate_action_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    effective_date = table.Column<string>(type: "TEXT", nullable: false),
                    quantity_factor = table.Column<string>(type: "TEXT", nullable: true),
                    price_factor = table.Column<string>(type: "TEXT", nullable: true),
                    before_quantity = table.Column<string>(type: "TEXT", nullable: false),
                    after_quantity = table.Column<string>(type: "TEXT", nullable: true),
                    before_basis_price = table.Column<string>(type: "TEXT", nullable: false),
                    after_basis_price = table.Column<string>(type: "TEXT", nullable: true),
                    before_fixed_atr = table.Column<string>(type: "TEXT", nullable: true),
                    after_fixed_atr = table.Column<string>(type: "TEXT", nullable: true),
                    before_stop_price = table.Column<string>(type: "TEXT", nullable: true),
                    after_stop_price = table.Column<string>(type: "TEXT", nullable: true),
                    before_take_profit_price = table.Column<string>(type: "TEXT", nullable: true),
                    after_take_profit_price = table.Column<string>(type: "TEXT", nullable: true),
                    details_json = table.Column<string>(type: "TEXT", nullable: false),
                    confirmed_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_adjustments", x => x.id);
                    table.CheckConstraint("ck_position_adjustments_content_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_position_adjustments_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
                    table.CheckConstraint("ck_position_adjustments_revision_no", "revision_no > 0");
                    table.CheckConstraint("ck_position_adjustments_status", "status IN ('Applied', 'ReconciliationRequired', 'Resolved', 'Reversed')");
                    table.ForeignKey(
                        name: "fk_position_adjustments_corporate_action_revisions_corporate_action_revision_id",
                        column: x => x.corporate_action_revision_id,
                        principalTable: "corporate_action_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_adjustments_margin_lots_margin_lot_id",
                        column: x => x.margin_lot_id,
                        principalTable: "margin_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_adjustments_position_adjustments_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "position_adjustments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_position_adjustments_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_basis_snapshots",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    margin_lot_id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    opening_trade_execution_revision_id = table.Column<string>(type: "TEXT", nullable: false),
                    origin_candidate_result_id = table.Column<string>(type: "TEXT", nullable: true),
                    strategy_parameter_snapshot_id = table.Column<string>(type: "TEXT", nullable: true),
                    analysis_input_manifest_id = table.Column<string>(type: "TEXT", nullable: true),
                    entry_basis_price = table.Column<string>(type: "TEXT", nullable: false),
                    atr_reference_bar_date = table.Column<string>(type: "TEXT", nullable: false),
                    fixed_atr = table.Column<string>(type: "TEXT", nullable: false),
                    atr_period = table.Column<long>(type: "INTEGER", nullable: false),
                    atr_algorithm_id = table.Column<string>(type: "TEXT", nullable: false),
                    stop_multiplier = table.Column<string>(type: "TEXT", nullable: false),
                    risk_amount_r = table.Column<string>(type: "TEXT", nullable: false),
                    partial_take_profit_r_multiple = table.Column<string>(type: "TEXT", nullable: false),
                    partial_take_profit_fraction = table.Column<string>(type: "TEXT", nullable: false),
                    initial_stop_price = table.Column<string>(type: "TEXT", nullable: false),
                    initial_take_profit_price = table.Column<string>(type: "TEXT", nullable: false),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_basis_snapshots", x => x.id);
                    table.CheckConstraint("ck_risk_basis_snapshots_atr_period", "atr_period > 0");
                    table.CheckConstraint("ck_risk_basis_snapshots_content_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_risk_basis_snapshots_fixed_atr", "CAST(fixed_atr AS NUMERIC) > 0");
                    table.CheckConstraint("ck_risk_basis_snapshots_partial_fraction", "CAST(partial_take_profit_fraction AS NUMERIC) > 0 AND CAST(partial_take_profit_fraction AS NUMERIC) <= 1");
                    table.CheckConstraint("ck_risk_basis_snapshots_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
                    table.CheckConstraint("ck_risk_basis_snapshots_revision_no", "revision_no > 0");
                    table.ForeignKey(
                        name: "fk_risk_basis_snapshots_analysis_input_manifests_analysis_input_manifest_id",
                        column: x => x.analysis_input_manifest_id,
                        principalTable: "analysis_input_manifests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_risk_basis_snapshots_candidate_results_origin_candidate_result_id",
                        column: x => x.origin_candidate_result_id,
                        principalTable: "candidate_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_risk_basis_snapshots_margin_lots_margin_lot_id",
                        column: x => x.margin_lot_id,
                        principalTable: "margin_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_risk_basis_snapshots_risk_basis_snapshots_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "risk_basis_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_risk_basis_snapshots_strategy_parameter_snapshots_strategy_parameter_snapshot_id",
                        column: x => x.strategy_parameter_snapshot_id,
                        principalTable: "strategy_parameter_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_risk_basis_snapshots_trade_execution_revisions_opening_trade_execution_revision_id",
                        column: x => x.opening_trade_execution_revision_id,
                        principalTable: "trade_execution_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "margin_cost_observations",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    margin_cost_item_id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    reconciles_estimate_id = table.Column<string>(type: "TEXT", nullable: true),
                    valuation_kind = table.Column<string>(type: "TEXT", nullable: false),
                    direction = table.Column<string>(type: "TEXT", nullable: false),
                    amount_status = table.Column<string>(type: "TEXT", nullable: false),
                    quantity = table.Column<string>(type: "TEXT", nullable: true),
                    rate = table.Column<string>(type: "TEXT", nullable: true),
                    rate_unit = table.Column<string>(type: "TEXT", nullable: true),
                    included_days = table.Column<long>(type: "INTEGER", nullable: true),
                    day_count_convention = table.Column<string>(type: "TEXT", nullable: true),
                    amount = table.Column<string>(type: "TEXT", nullable: true),
                    currency = table.Column<string>(type: "TEXT", nullable: true),
                    formula_version = table.Column<string>(type: "TEXT", nullable: true),
                    margin_lot_contract_revision_id = table.Column<string>(type: "TEXT", nullable: true),
                    published_margin_cost_revision_id = table.Column<string>(type: "TEXT", nullable: true),
                    source_kind = table.Column<string>(type: "TEXT", nullable: false),
                    source_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    source_published_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    available_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    observed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    booked_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_margin_cost_observations", x => x.id);
                    table.CheckConstraint("ck_margin_cost_observations_amount", "(amount_status = 'KnownAmount' AND amount IS NOT NULL AND CAST(amount AS NUMERIC) <> 0 AND currency IS NOT NULL) OR (amount_status = 'KnownZero' AND amount IS NOT NULL AND CAST(amount AS NUMERIC) = 0 AND currency IS NOT NULL) OR (amount_status IN ('NotOccurred', 'Unpublished', 'FetchFailed', 'Unknown', 'NotApplicable') AND amount IS NULL AND currency IS NULL)");
                    table.CheckConstraint("ck_margin_cost_observations_amount_status", "amount_status IN ('KnownAmount', 'KnownZero', 'NotOccurred', 'Unpublished', 'FetchFailed', 'Unknown', 'NotApplicable')");
                    table.CheckConstraint("ck_margin_cost_observations_content_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_margin_cost_observations_currency", "currency IS NULL OR (length(currency) = 3 AND currency = upper(currency))");
                    table.CheckConstraint("ck_margin_cost_observations_direction", "direction IN ('Charge', 'Credit')");
                    table.CheckConstraint("ck_margin_cost_observations_included_days", "included_days IS NULL OR included_days >= 0");
                    table.CheckConstraint("ck_margin_cost_observations_reconciliation_kind", "reconciles_estimate_id IS NULL OR valuation_kind = 'Confirmed'");
                    table.CheckConstraint("ck_margin_cost_observations_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
                    table.CheckConstraint("ck_margin_cost_observations_revision_no", "revision_no > 0");
                    table.CheckConstraint("ck_margin_cost_observations_source_kind", "source_kind IN ('ApplicationEstimate', 'PublishedMarketData', 'BrokerStatement', 'UserEntry')");
                    table.CheckConstraint("ck_margin_cost_observations_valuation_kind", "valuation_kind IN ('Estimate', 'Confirmed')");
                    table.ForeignKey(
                        name: "fk_margin_cost_observations_margin_cost_items_margin_cost_item_id",
                        column: x => x.margin_cost_item_id,
                        principalTable: "margin_cost_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_margin_cost_observations_margin_cost_observations_reconciles_estimate_id",
                        column: x => x.reconciles_estimate_id,
                        principalTable: "margin_cost_observations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_margin_cost_observations_margin_cost_observations_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "margin_cost_observations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_margin_cost_observations_margin_lot_contract_revisions_margin_lot_contract_revision_id",
                        column: x => x.margin_lot_contract_revision_id,
                        principalTable: "margin_lot_contract_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_margin_cost_observations_published_margin_cost_revisions_published_margin_cost_revision_id",
                        column: x => x.published_margin_cost_revision_id,
                        principalTable: "published_margin_cost_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_margin_cost_observations_source_artifacts_source_artifact_id",
                        column: x => x.source_artifact_id,
                        principalTable: "source_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_plan_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    revision_no = table.Column<long>(type: "INTEGER", nullable: false),
                    supersedes_id = table.Column<string>(type: "TEXT", nullable: true),
                    content_sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    risk_basis_snapshot_id = table.Column<string>(type: "TEXT", nullable: false),
                    stop_price = table.Column<string>(type: "TEXT", nullable: false),
                    take_profit_price = table.Column<string>(type: "TEXT", nullable: false),
                    trigger_trade_execution_id = table.Column<string>(type: "TEXT", nullable: true),
                    trigger_lot_allocation_revision_id = table.Column<string>(type: "TEXT", nullable: true),
                    trigger_position_adjustment_id = table.Column<string>(type: "TEXT", nullable: true),
                    plan_reason = table.Column<string>(type: "TEXT", nullable: false),
                    effective_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    is_cost_adjusted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_plan_revisions", x => x.id);
                    table.CheckConstraint("ck_risk_plan_revisions_content_sha256", "length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("ck_risk_plan_revisions_cost_adjusted", "is_cost_adjusted = 0");
                    table.CheckConstraint("ck_risk_plan_revisions_plan_reason", "plan_reason IN ('Initial', 'PartialExitBreakeven', 'CorporateActionConversion', 'UserCorrection')");
                    table.CheckConstraint("ck_risk_plan_revisions_revision_kind", "(revision_no = 1 AND supersedes_id IS NULL AND plan_reason = 'Initial') OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id AND plan_reason <> 'Initial')");
                    table.CheckConstraint("ck_risk_plan_revisions_revision_no", "revision_no > 0");
                    table.CheckConstraint("ck_risk_plan_revisions_triggers", "(plan_reason = 'PartialExitBreakeven' AND trigger_trade_execution_id IS NOT NULL AND trigger_lot_allocation_revision_id IS NOT NULL AND trigger_position_adjustment_id IS NULL) OR (plan_reason = 'CorporateActionConversion' AND trigger_position_adjustment_id IS NOT NULL AND trigger_trade_execution_id IS NULL AND trigger_lot_allocation_revision_id IS NULL) OR (plan_reason IN ('Initial', 'UserCorrection') AND trigger_trade_execution_id IS NULL AND trigger_lot_allocation_revision_id IS NULL AND trigger_position_adjustment_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_risk_plan_revisions_lot_allocation_revisions_trigger_lot_allocation_revision_id",
                        column: x => x.trigger_lot_allocation_revision_id,
                        principalTable: "lot_allocation_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_risk_plan_revisions_position_adjustments_trigger_position_adjustment_id",
                        column: x => x.trigger_position_adjustment_id,
                        principalTable: "position_adjustments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_risk_plan_revisions_risk_basis_snapshots_risk_basis_snapshot_id",
                        column: x => x.risk_basis_snapshot_id,
                        principalTable: "risk_basis_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_risk_plan_revisions_risk_plan_revisions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "risk_plan_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_risk_plan_revisions_trade_executions_trigger_trade_execution_id",
                        column: x => x.trigger_trade_execution_id,
                        principalTable: "trade_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "margin_cost_amount_components",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    margin_cost_observation_id = table.Column<string>(type: "TEXT", nullable: false),
                    component_type = table.Column<string>(type: "TEXT", nullable: false),
                    direction = table.Column<string>(type: "TEXT", nullable: false),
                    amount_status = table.Column<string>(type: "TEXT", nullable: false),
                    amount = table.Column<string>(type: "TEXT", nullable: true),
                    currency = table.Column<string>(type: "TEXT", nullable: true),
                    ordinal = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_margin_cost_amount_components", x => x.id);
                    table.CheckConstraint("ck_margin_cost_amount_components_amount", "(amount_status = 'KnownAmount' AND amount IS NOT NULL AND CAST(amount AS NUMERIC) <> 0 AND currency IS NOT NULL) OR (amount_status = 'KnownZero' AND amount IS NOT NULL AND CAST(amount AS NUMERIC) = 0 AND currency IS NOT NULL) OR (amount_status IN ('NotOccurred', 'Unpublished', 'FetchFailed', 'Unknown', 'NotApplicable') AND amount IS NULL AND currency IS NULL)");
                    table.CheckConstraint("ck_margin_cost_amount_components_amount_status", "amount_status IN ('KnownAmount', 'KnownZero', 'NotOccurred', 'Unpublished', 'FetchFailed', 'Unknown', 'NotApplicable')");
                    table.CheckConstraint("ck_margin_cost_amount_components_component_type", "component_type IN ('Gross', 'TaxEquivalent', 'Net', 'BrokerBooked', 'Other')");
                    table.CheckConstraint("ck_margin_cost_amount_components_currency", "currency IS NULL OR (length(currency) = 3 AND currency = upper(currency))");
                    table.CheckConstraint("ck_margin_cost_amount_components_direction", "direction IN ('Charge', 'Credit')");
                    table.CheckConstraint("ck_margin_cost_amount_components_ordinal", "ordinal >= 0");
                    table.ForeignKey(
                        name: "fk_margin_cost_amount_components_margin_cost_observations_margin_cost_observation_id",
                        column: x => x.margin_cost_observation_id,
                        principalTable: "margin_cost_observations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_ai_attempt_events_ai_attempt_id_ordinal",
                table: "ai_attempt_events",
                columns: new[] { "ai_attempt_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ai_attempts_ai_check_job_id",
                table: "ai_attempts",
                column: "ai_check_job_id",
                unique: true,
                filter: "status IN ('Queued', 'Running')");

            migrationBuilder.CreateIndex(
                name: "ux_ai_attempts_ai_check_job_id_attempt_no",
                table: "ai_attempts",
                columns: new[] { "ai_check_job_id", "attempt_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_check_jobs_ai_profile_snapshot_id",
                table: "ai_check_jobs",
                column: "ai_profile_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_check_jobs_prompt_template_snapshot_id",
                table: "ai_check_jobs",
                column: "prompt_template_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ux_ai_check_jobs_candidate_result_id_input_sha256_ai_profile_snapshot_id_prompt_template_snapshot_id",
                table: "ai_check_jobs",
                columns: new[] { "candidate_result_id", "input_sha256", "ai_profile_snapshot_id", "prompt_template_snapshot_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ai_job_request_events_ai_check_job_id_ordinal",
                table: "ai_job_request_events",
                columns: new[] { "ai_check_job_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ai_profile_snapshots_profile_sha256",
                table: "ai_profile_snapshots",
                column: "profile_sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ai_result_sources_ai_result_id_ordinal",
                table: "ai_result_sources",
                columns: new[] { "ai_result_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ai_results_ai_attempt_id",
                table: "ai_results",
                column: "ai_attempt_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analysis_action_applications_corporate_action_revision_id",
                table: "analysis_action_applications",
                column: "corporate_action_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_analysis_action_applications_reference_price_revision_id",
                table: "analysis_action_applications",
                column: "reference_price_revision_id");

            migrationBuilder.CreateIndex(
                name: "ux_analysis_action_applications_analysis_input_manifest_id_corporate_action_revision_id",
                table: "analysis_action_applications",
                columns: new[] { "analysis_input_manifest_id", "corporate_action_revision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_analysis_action_applications_analysis_input_manifest_id_ordinal",
                table: "analysis_action_applications",
                columns: new[] { "analysis_input_manifest_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analysis_input_manifests_instrument_id",
                table: "analysis_input_manifests",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_analysis_input_manifests_price_revision_set_id",
                table: "analysis_input_manifests",
                column: "price_revision_set_id");

            migrationBuilder.CreateIndex(
                name: "ux_analysis_input_manifests_analysis_run_id_instrument_id_price_provider",
                table: "analysis_input_manifests",
                columns: new[] { "analysis_run_id", "instrument_id", "price_provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analysis_runs_market_calendar_version_id",
                table: "analysis_runs",
                column: "market_calendar_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_analysis_runs_strategy_parameter_snapshot_id",
                table: "analysis_runs",
                column: "strategy_parameter_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ux_candidate_results_technical_analysis_result_id",
                table: "candidate_results",
                column: "technical_analysis_result_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_candidate_score_components_candidate_result_id_component_key",
                table: "candidate_score_components",
                columns: new[] { "candidate_result_id", "component_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_candidate_score_components_candidate_result_id_ordinal",
                table: "candidate_score_components",
                columns: new[] { "candidate_result_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_corporate_action_revisions_source_artifact_id",
                table: "corporate_action_revisions",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ux_corporate_action_revisions_corporate_action_id_revision_no",
                table: "corporate_action_revisions",
                columns: new[] { "corporate_action_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_corporate_action_revisions_supersedes_id",
                table: "corporate_action_revisions",
                column: "supersedes_id",
                unique: true,
                filter: "\"supersedes_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_corporate_actions_instrument_id_provider_derived_event_key",
                table: "corporate_actions",
                columns: new[] { "instrument_id", "provider", "derived_event_key" },
                unique: true,
                filter: "\"source_event_id\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_corporate_actions_instrument_id_provider_source_event_id",
                table: "corporate_actions",
                columns: new[] { "instrument_id", "provider", "source_event_id" },
                unique: true,
                filter: "\"source_event_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_daily_price_revisions_source_artifact_id",
                table: "daily_price_revisions",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ux_daily_price_revisions_daily_price_id_revision_no",
                table: "daily_price_revisions",
                columns: new[] { "daily_price_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_daily_price_revisions_supersedes_id",
                table: "daily_price_revisions",
                column: "supersedes_id",
                unique: true,
                filter: "\"supersedes_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_daily_prices_instrument_id_bar_date_provider",
                table: "daily_prices",
                columns: new[] { "instrument_id", "bar_date", "provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_data_update_failures_data_update_item_id",
                table: "data_update_failures",
                column: "data_update_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_data_update_failures_data_update_run_id",
                table: "data_update_failures",
                column: "data_update_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_data_update_failures_instrument_id",
                table: "data_update_failures",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_data_update_items_instrument_id",
                table: "data_update_items",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_data_update_items_source_artifact_id",
                table: "data_update_items",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ux_data_update_items_data_update_run_id_item_key_item_attempt_no",
                table: "data_update_items",
                columns: new[] { "data_update_run_id", "item_key", "item_attempt_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fundamental_records_instrument_id_provider_source_record_key",
                table: "fundamental_records",
                columns: new[] { "instrument_id", "provider", "source_record_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fundamental_revisions_source_artifact_id",
                table: "fundamental_revisions",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ux_fundamental_revisions_fundamental_record_id_revision_no",
                table: "fundamental_revisions",
                columns: new[] { "fundamental_record_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fundamental_revisions_supersedes_id",
                table: "fundamental_revisions",
                column: "supersedes_id",
                unique: true,
                filter: "\"supersedes_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_indicator_results_technical_analysis_result_id_indicator_key",
                table: "indicator_results",
                columns: new[] { "technical_analysis_result_id", "indicator_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_indicator_results_technical_analysis_result_id_ordinal",
                table: "indicator_results",
                columns: new[] { "technical_analysis_result_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instrument_identifier_revisions_source_artifact_id",
                table: "instrument_identifier_revisions",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_identifier_revisions_value_instrument_identifier_id",
                table: "instrument_identifier_revisions",
                columns: new[] { "value", "instrument_identifier_id" });

            migrationBuilder.CreateIndex(
                name: "ux_instrument_identifier_revisions_instrument_identifier_id_revision_no",
                table: "instrument_identifier_revisions",
                columns: new[] { "instrument_identifier_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_instrument_identifier_revisions_supersedes_id",
                table: "instrument_identifier_revisions",
                column: "supersedes_id",
                unique: true,
                filter: "\"supersedes_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_identifiers_instrument_id",
                table: "instrument_identifiers",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_master_revisions_source_artifact_id",
                table: "instrument_master_revisions",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ux_instrument_master_revisions_instrument_id_provider_revision_no",
                table: "instrument_master_revisions",
                columns: new[] { "instrument_id", "provider", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_instrument_master_revisions_supersedes_id",
                table: "instrument_master_revisions",
                column: "supersedes_id",
                unique: true,
                filter: "\"supersedes_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_lot_allocation_revisions_closing_trade_execution_id",
                table: "lot_allocation_revisions",
                column: "closing_trade_execution_id");

            migrationBuilder.CreateIndex(
                name: "ix_lot_allocation_revisions_closing_trade_execution_revision_id",
                table: "lot_allocation_revisions",
                column: "closing_trade_execution_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_lot_allocation_revisions_margin_lot_id",
                table: "lot_allocation_revisions",
                column: "margin_lot_id");

            migrationBuilder.CreateIndex(
                name: "ux_lot_allocation_revisions_allocation_key_revision_no",
                table: "lot_allocation_revisions",
                columns: new[] { "allocation_key", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_lot_allocation_revisions_supersedes_id",
                table: "lot_allocation_revisions",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_margin_cost_amount_components_margin_cost_observation_id_component_type",
                table: "margin_cost_amount_components",
                columns: new[] { "margin_cost_observation_id", "component_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_margin_cost_amount_components_margin_cost_observation_id_ordinal",
                table: "margin_cost_amount_components",
                columns: new[] { "margin_cost_observation_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_margin_cost_items_margin_lot_id_broker_statement_line_id",
                table: "margin_cost_items",
                columns: new[] { "margin_lot_id", "broker_statement_line_id" },
                unique: true,
                filter: "broker_statement_line_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_margin_cost_items_margin_lot_id_cost_type_occurrence_key",
                table: "margin_cost_items",
                columns: new[] { "margin_lot_id", "cost_type", "occurrence_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_margin_cost_observations_margin_lot_contract_revision_id",
                table: "margin_cost_observations",
                column: "margin_lot_contract_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_margin_cost_observations_published_margin_cost_revision_id",
                table: "margin_cost_observations",
                column: "published_margin_cost_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_margin_cost_observations_reconciles_estimate_id",
                table: "margin_cost_observations",
                column: "reconciles_estimate_id");

            migrationBuilder.CreateIndex(
                name: "ix_margin_cost_observations_source_artifact_id",
                table: "margin_cost_observations",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ux_margin_cost_observations_margin_cost_item_id_valuation_kind_revision_no",
                table: "margin_cost_observations",
                columns: new[] { "margin_cost_item_id", "valuation_kind", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_margin_cost_observations_supersedes_id",
                table: "margin_cost_observations",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_margin_eligibility_records_instrument_id_provider_source_record_key",
                table: "margin_eligibility_records",
                columns: new[] { "instrument_id", "provider", "source_record_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_margin_eligibility_revisions_source_artifact_id",
                table: "margin_eligibility_revisions",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ux_margin_eligibility_revisions_margin_eligibility_record_id_revision_no",
                table: "margin_eligibility_revisions",
                columns: new[] { "margin_eligibility_record_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_margin_eligibility_revisions_supersedes_id",
                table: "margin_eligibility_revisions",
                column: "supersedes_id",
                unique: true,
                filter: "\"supersedes_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_margin_lot_contract_revisions_opening_trade_execution_revision_id",
                table: "margin_lot_contract_revisions",
                column: "opening_trade_execution_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_margin_lot_contract_revisions_source_artifact_id",
                table: "margin_lot_contract_revisions",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ux_margin_lot_contract_revisions_margin_lot_id_revision_no",
                table: "margin_lot_contract_revisions",
                columns: new[] { "margin_lot_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_margin_lot_contract_revisions_supersedes_id",
                table: "margin_lot_contract_revisions",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_margin_lots_position_id",
                table: "margin_lots",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ux_margin_lots_initial_opening_trade_execution_revision_id",
                table: "margin_lots",
                column: "initial_opening_trade_execution_revision_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_margin_lots_opening_trade_execution_id",
                table: "margin_lots",
                column: "opening_trade_execution_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_market_calendar_days_source_artifact_id",
                table: "market_calendar_days",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_calendar_versions_source_artifact_id",
                table: "market_calendar_versions",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ux_market_calendar_versions_market_code_content_sha256",
                table: "market_calendar_versions",
                columns: new[] { "market_code", "content_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_market_calendar_versions_market_code_version_name",
                table: "market_calendar_versions",
                columns: new[] { "market_code", "version_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_position_adjustments_corporate_action_revision_id",
                table: "position_adjustments",
                column: "corporate_action_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_position_adjustments_margin_lot_id",
                table: "position_adjustments",
                column: "margin_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_position_adjustments_position_id",
                table: "position_adjustments",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ux_position_adjustments_adjustment_key_revision_no",
                table: "position_adjustments",
                columns: new[] { "adjustment_key", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_position_adjustments_margin_lot_id_corporate_action_revision_id",
                table: "position_adjustments",
                columns: new[] { "margin_lot_id", "corporate_action_revision_id" },
                unique: true,
                filter: "revision_no = 1");

            migrationBuilder.CreateIndex(
                name: "ux_position_adjustments_supersedes_id",
                table: "position_adjustments",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_position_evaluation_input_manifests_analysis_input_manifest_id",
                table: "position_evaluation_input_manifests",
                column: "analysis_input_manifest_id");

            migrationBuilder.CreateIndex(
                name: "ix_position_evaluation_input_manifests_current_price_revision_id",
                table: "position_evaluation_input_manifests",
                column: "current_price_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_position_evaluation_input_manifests_position_id",
                table: "position_evaluation_input_manifests",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ux_position_evaluation_input_manifests_analysis_run_id_position_id",
                table: "position_evaluation_input_manifests",
                columns: new[] { "analysis_run_id", "position_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_position_evaluations_position_id",
                table: "position_evaluations",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ux_position_evaluations_analysis_run_id_position_id",
                table: "position_evaluations",
                columns: new[] { "analysis_run_id", "position_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_position_evaluations_position_evaluation_input_manifest_id",
                table: "position_evaluations",
                column: "position_evaluation_input_manifest_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_position_state_revisions_position_id_revision_no",
                table: "position_state_revisions",
                columns: new[] { "position_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_position_state_revisions_supersedes_id",
                table: "position_state_revisions",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_positions_instrument_id",
                table: "positions",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_positions_origin_candidate_result_id",
                table: "positions",
                column: "origin_candidate_result_id");

            migrationBuilder.CreateIndex(
                name: "ix_positions_strategy_parameter_snapshot_id",
                table: "positions",
                column: "strategy_parameter_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_history_assessments_instrument_id",
                table: "price_history_assessments",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_history_assessments_source_artifact_id",
                table: "price_history_assessments",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_revision_set_changes_daily_price_revision_id",
                table: "price_revision_set_changes",
                column: "daily_price_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_revision_set_changes_replaced_daily_price_revision_id",
                table: "price_revision_set_changes",
                column: "replaced_daily_price_revision_id");

            migrationBuilder.CreateIndex(
                name: "ux_price_revision_set_changes_price_revision_set_id_bar_date",
                table: "price_revision_set_changes",
                columns: new[] { "price_revision_set_id", "bar_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_price_revision_set_changes_price_revision_set_id_ordinal",
                table: "price_revision_set_changes",
                columns: new[] { "price_revision_set_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_price_revision_sets_parent_set_id",
                table: "price_revision_sets",
                column: "parent_set_id");

            migrationBuilder.CreateIndex(
                name: "ux_price_revision_sets_instrument_id_provider_set_sha256",
                table: "price_revision_sets",
                columns: new[] { "instrument_id", "provider", "set_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_prompt_template_snapshots_template_sha256",
                table: "prompt_template_snapshots",
                column: "template_sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_published_margin_cost_revisions_source_artifact_id",
                table: "published_margin_cost_revisions",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ux_published_margin_cost_revisions_published_margin_cost_id_revision_no",
                table: "published_margin_cost_revisions",
                columns: new[] { "published_margin_cost_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_published_margin_cost_revisions_supersedes_id",
                table: "published_margin_cost_revisions",
                column: "supersedes_id",
                unique: true,
                filter: "\"supersedes_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_published_margin_costs_instrument_id_provider_cost_type_source_record_key",
                table: "published_margin_costs",
                columns: new[] { "instrument_id", "provider", "cost_type", "source_record_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_risk_basis_snapshots_analysis_input_manifest_id",
                table: "risk_basis_snapshots",
                column: "analysis_input_manifest_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_basis_snapshots_opening_trade_execution_revision_id",
                table: "risk_basis_snapshots",
                column: "opening_trade_execution_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_basis_snapshots_origin_candidate_result_id",
                table: "risk_basis_snapshots",
                column: "origin_candidate_result_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_basis_snapshots_strategy_parameter_snapshot_id",
                table: "risk_basis_snapshots",
                column: "strategy_parameter_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ux_risk_basis_snapshots_margin_lot_id_revision_no",
                table: "risk_basis_snapshots",
                columns: new[] { "margin_lot_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_risk_basis_snapshots_supersedes_id",
                table: "risk_basis_snapshots",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_risk_plan_revisions_trigger_lot_allocation_revision_id",
                table: "risk_plan_revisions",
                column: "trigger_lot_allocation_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_plan_revisions_trigger_position_adjustment_id",
                table: "risk_plan_revisions",
                column: "trigger_position_adjustment_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_plan_revisions_trigger_trade_execution_id",
                table: "risk_plan_revisions",
                column: "trigger_trade_execution_id");

            migrationBuilder.CreateIndex(
                name: "ux_risk_plan_revisions_risk_basis_snapshot_id_revision_no",
                table: "risk_plan_revisions",
                columns: new[] { "risk_basis_snapshot_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_risk_plan_revisions_supersedes_id",
                table: "risk_plan_revisions",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_source_artifacts_provider_dataset_kind_content_sha256",
                table: "source_artifacts",
                columns: new[] { "provider", "dataset_kind", "content_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_strategy_parameter_snapshots_strategy_key_parameters_sha256",
                table: "strategy_parameter_snapshots",
                columns: new[] { "strategy_key", "parameters_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_technical_analysis_results_analysis_input_manifest_id",
                table: "technical_analysis_results",
                column: "analysis_input_manifest_id");

            migrationBuilder.CreateIndex(
                name: "ix_technical_analysis_results_instrument_id",
                table: "technical_analysis_results",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ux_technical_analysis_results_analysis_run_id_instrument_id_position_side_signal_purpose",
                table: "technical_analysis_results",
                columns: new[] { "analysis_run_id", "instrument_id", "position_side", "signal_purpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trade_execution_revisions_source_artifact_id",
                table: "trade_execution_revisions",
                column: "source_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ux_trade_execution_revisions_supersedes_id",
                table: "trade_execution_revisions",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_trade_execution_revisions_trade_execution_id_revision_no",
                table: "trade_execution_revisions",
                columns: new[] { "trade_execution_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trade_executions_candidate_context_id",
                table: "trade_executions",
                column: "candidate_context_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_executions_position_id",
                table: "trade_executions",
                column: "position_id");

            CreateBusinessTriggers(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropBusinessTriggers(migrationBuilder);

            migrationBuilder.DropTable(
                name: "ai_attempt_events");

            migrationBuilder.DropTable(
                name: "ai_job_request_events");

            migrationBuilder.DropTable(
                name: "ai_result_sources");

            migrationBuilder.DropTable(
                name: "analysis_action_applications");

            migrationBuilder.DropTable(
                name: "candidate_score_components");

            migrationBuilder.DropTable(
                name: "data_update_failures");

            migrationBuilder.DropTable(
                name: "fundamental_revisions");

            migrationBuilder.DropTable(
                name: "indicator_results");

            migrationBuilder.DropTable(
                name: "instrument_identifier_revisions");

            migrationBuilder.DropTable(
                name: "instrument_master_revisions");

            migrationBuilder.DropTable(
                name: "margin_cost_amount_components");

            migrationBuilder.DropTable(
                name: "margin_eligibility_revisions");

            migrationBuilder.DropTable(
                name: "market_calendar_days");

            migrationBuilder.DropTable(
                name: "position_evaluations");

            migrationBuilder.DropTable(
                name: "position_state_revisions");

            migrationBuilder.DropTable(
                name: "price_history_assessments");

            migrationBuilder.DropTable(
                name: "price_revision_set_changes");

            migrationBuilder.DropTable(
                name: "risk_plan_revisions");

            migrationBuilder.DropTable(
                name: "ai_results");

            migrationBuilder.DropTable(
                name: "data_update_items");

            migrationBuilder.DropTable(
                name: "fundamental_records");

            migrationBuilder.DropTable(
                name: "instrument_identifiers");

            migrationBuilder.DropTable(
                name: "margin_cost_observations");

            migrationBuilder.DropTable(
                name: "margin_eligibility_records");

            migrationBuilder.DropTable(
                name: "position_evaluation_input_manifests");

            migrationBuilder.DropTable(
                name: "lot_allocation_revisions");

            migrationBuilder.DropTable(
                name: "position_adjustments");

            migrationBuilder.DropTable(
                name: "risk_basis_snapshots");

            migrationBuilder.DropTable(
                name: "ai_attempts");

            migrationBuilder.DropTable(
                name: "data_update_runs");

            migrationBuilder.DropTable(
                name: "margin_cost_items");

            migrationBuilder.DropTable(
                name: "margin_lot_contract_revisions");

            migrationBuilder.DropTable(
                name: "published_margin_cost_revisions");

            migrationBuilder.DropTable(
                name: "daily_price_revisions");

            migrationBuilder.DropTable(
                name: "corporate_action_revisions");

            migrationBuilder.DropTable(
                name: "ai_check_jobs");

            migrationBuilder.DropTable(
                name: "margin_lots");

            migrationBuilder.DropTable(
                name: "published_margin_costs");

            migrationBuilder.DropTable(
                name: "daily_prices");

            migrationBuilder.DropTable(
                name: "corporate_actions");

            migrationBuilder.DropTable(
                name: "ai_profile_snapshots");

            migrationBuilder.DropTable(
                name: "prompt_template_snapshots");

            migrationBuilder.DropTable(
                name: "trade_execution_revisions");

            migrationBuilder.DropTable(
                name: "trade_executions");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.DropTable(
                name: "candidate_results");

            migrationBuilder.DropTable(
                name: "technical_analysis_results");

            migrationBuilder.DropTable(
                name: "analysis_input_manifests");

            migrationBuilder.DropTable(
                name: "analysis_runs");

            migrationBuilder.DropTable(
                name: "price_revision_sets");

            migrationBuilder.DropTable(
                name: "market_calendar_versions");

            migrationBuilder.DropTable(
                name: "strategy_parameter_snapshots");

            migrationBuilder.DropTable(
                name: "instruments");

            migrationBuilder.DropTable(
                name: "source_artifacts");
        }
    }
}
