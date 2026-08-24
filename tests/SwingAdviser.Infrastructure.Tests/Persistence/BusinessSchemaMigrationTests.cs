using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SwingAdviser.Infrastructure.Persistence;

namespace SwingAdviser.Infrastructure.Tests.Persistence;

public sealed class BusinessSchemaMigrationTests
{
    private static readonly string[] ExpectedBusinessTables =
    [
        "instruments", "instrument_identifiers", "instrument_identifier_revisions",
        "instrument_master_revisions", "market_calendar_versions", "market_calendar_days",
        "source_artifacts", "data_update_runs", "data_update_items", "data_update_failures",
        "daily_prices", "daily_price_revisions", "price_history_assessments", "price_revision_sets",
        "price_revision_set_changes", "corporate_actions", "corporate_action_revisions",
        "margin_eligibility_records", "margin_eligibility_revisions", "published_margin_costs",
        "published_margin_cost_revisions", "fundamental_records", "fundamental_revisions",
        "strategy_parameter_snapshots", "analysis_runs", "analysis_input_manifests",
        "analysis_action_applications", "technical_analysis_results", "indicator_results",
        "candidate_results", "candidate_score_components", "positions", "position_state_revisions",
        "trade_executions", "trade_execution_revisions", "margin_lots",
        "margin_lot_contract_revisions", "lot_allocation_revisions", "position_adjustments",
        "risk_basis_snapshots", "risk_plan_revisions", "position_evaluation_input_manifests",
        "position_evaluations", "margin_cost_items", "margin_cost_observations",
        "margin_cost_amount_components", "prompt_template_snapshots", "ai_profile_snapshots",
        "ai_check_jobs", "ai_job_request_events", "ai_attempts", "ai_attempt_events",
        "ai_results", "ai_result_sources",
    ];

    [Fact]
    public async Task AddBusinessSchema_CreatesExpectedSchemaAndPassesIntegrityChecks()
    {
        await using var fixture = await MigratedDatabase.CreateAsync();

        var tableNames = await ReadNamesAsync(
            fixture.Connection,
            "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name NOT IN ('__ef_migrations_history', '__EFMigrationsLock') ORDER BY name;");
        Assert.Equal(ExpectedBusinessTables.Order(StringComparer.Ordinal), tableNames);
        Assert.All(tableNames, name => Assert.Matches(new Regex("^[a-z][a-z0-9_]*$"), name));

        var triggerNames = await ReadNamesAsync(
            fixture.Connection,
            "SELECT name FROM sqlite_schema WHERE type = 'trigger' ORDER BY name;");
        Assert.Equal(109, triggerNames.Length);
        Assert.Contains("trg_trade_executions_immutable_update", triggerNames);
        Assert.Contains("trg_analysis_runs_operational_update", triggerNames);

        await using var foreignKeyCheck = fixture.Connection.CreateCommand();
        foreignKeyCheck.CommandText = "PRAGMA foreign_key_check;";
        Assert.Null(await foreignKeyCheck.ExecuteScalarAsync());

        await using var integrityCheck = fixture.Connection.CreateCommand();
        integrityCheck.CommandText = "PRAGMA integrity_check;";
        Assert.Equal("ok", Assert.IsType<string>(await integrityCheck.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task EveryForeignKey_UsesRestrictAndHasALeadingIndex()
    {
        await using var fixture = await MigratedDatabase.CreateAsync();

        foreach (var table in ExpectedBusinessTables)
        {
            var indexedLeadingColumns = await ReadLeadingIndexColumnsAsync(fixture.Connection, table);

            await using var command = fixture.Connection.CreateCommand();
            command.CommandText = $"PRAGMA foreign_key_list(\"{table}\");";
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var column = reader.GetString(reader.GetOrdinal("from"));
                var onDelete = reader.GetString(reader.GetOrdinal("on_delete"));
                Assert.Equal("RESTRICT", onDelete);
                Assert.Contains(column, indexedLeadingColumns);
            }
        }
    }

    [Fact]
    public async Task ImmutableTables_RejectUpdateAndDelete()
    {
        await using var fixture = await MigratedDatabase.CreateAsync();
        var instrumentId = Guid.NewGuid().ToString("D");
        const string instant = "2026-08-24T00:00:00.0000000Z";

        await ExecuteAsync(
            fixture.Connection,
            "INSERT INTO instruments(id, created_at_utc) VALUES ($id, $instant);",
            ("$id", instrumentId), ("$instant", instant));

        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
            fixture.Connection,
            "UPDATE instruments SET created_at_utc = $newInstant WHERE id = $id;",
            ("$newInstant", "2026-08-24T00:00:01.0000000Z"), ("$id", instrumentId)));
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
            fixture.Connection,
            "DELETE FROM instruments WHERE id = $id;",
            ("$id", instrumentId)));
    }

    [Fact]
    public async Task OperationalRun_AllowsForwardProgressButRejectsImmutableAndTerminalUpdates()
    {
        await using var fixture = await MigratedDatabase.CreateAsync();
        var runId = Guid.NewGuid().ToString("D");
        const string instant = "2026-08-24T00:00:00.0000000Z";
        var hash = new string('a', 64);

        await ExecuteAsync(
            fixture.Connection,
            """
            INSERT INTO data_update_runs(
                id, dataset_kind, provider, status, requested_at_utc,
                configuration_snapshot_json, configuration_sha256)
            VALUES ($id, 'Prices', 'Provider', 'Queued', $instant, '{}', $hash);
            """,
            ("$id", runId), ("$instant", instant), ("$hash", hash));

        await ExecuteAsync(
            fixture.Connection,
            "UPDATE data_update_runs SET status = 'Running', started_at_utc = $instant WHERE id = $id;",
            ("$instant", instant), ("$id", runId));

        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
            fixture.Connection,
            "UPDATE data_update_runs SET provider = 'Changed' WHERE id = $id;",
            ("$id", runId)));

        await ExecuteAsync(
            fixture.Connection,
            "UPDATE data_update_runs SET status = 'Succeeded', completed_at_utc = $instant, success_count = 1 WHERE id = $id;",
            ("$instant", instant), ("$id", runId));

        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
            fixture.Connection,
            "UPDATE data_update_runs SET summary = 'changed after terminal' WHERE id = $id;",
            ("$id", runId)));
    }

    [Fact]
    public async Task TradeExecutionRevisionChain_CannotBranch()
    {
        await using var fixture = await MigratedDatabase.CreateAsync();
        var instrumentId = Guid.NewGuid().ToString("D");
        var positionId = Guid.NewGuid().ToString("D");
        var executionId = Guid.NewGuid().ToString("D");
        var firstRevisionId = Guid.NewGuid().ToString("D");
        const string instant = "2026-08-24T00:00:00.0000000Z";

        await ExecuteAsync(fixture.Connection,
            "INSERT INTO instruments(id, created_at_utc) VALUES ($id, $instant);",
            ("$id", instrumentId), ("$instant", instant));
        await ExecuteAsync(fixture.Connection,
            "INSERT INTO positions(id, instrument_id, position_side, created_at_utc) VALUES ($id, $instrument, 'Long', $instant);",
            ("$id", positionId), ("$instrument", instrumentId), ("$instant", instant));
        await ExecuteAsync(fixture.Connection,
            "INSERT INTO trade_executions(id, position_id, execution_kind, origin, created_at_utc) VALUES ($id, $position, 'Open', 'UserConfirmed', $instant);",
            ("$id", executionId), ("$position", positionId), ("$instant", instant));

        await InsertTradeRevisionAsync(fixture.Connection, firstRevisionId, executionId, 1, null, "Initial", null);
        await InsertTradeRevisionAsync(
            fixture.Connection,
            Guid.NewGuid().ToString("D"),
            executionId,
            2,
            firstRevisionId,
            "Correction",
            "first correction");

        await Assert.ThrowsAsync<SqliteException>(() => InsertTradeRevisionAsync(
            fixture.Connection,
            Guid.NewGuid().ToString("D"),
            executionId,
            3,
            firstRevisionId,
            "Correction",
            "branch attempt"));
    }

    [Fact]
    public async Task SchemaTriggers_NeverCreateTradeExecutions()
    {
        await using var fixture = await MigratedDatabase.CreateAsync();
        await using var command = fixture.Connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type = 'trigger' AND lower(sql) LIKE '%insert%trade_executions%';";
        Assert.Null(await command.ExecuteScalarAsync());
    }

    [Fact]
    public void PersistenceFormats_RequireCanonicalValues()
    {
        Assert.Equal("1.23", PersistenceValueFormats.FormatDecimal(1.2300m));
        Assert.Equal(1.23m, PersistenceValueFormats.ParseDecimal("1.23"));
        Assert.Throws<FormatException>(() => PersistenceValueFormats.ParseDecimal("1.230"));
        Assert.Throws<FormatException>(() => PersistenceValueFormats.ParseDecimal("-0"));
        Assert.Throws<FormatException>(() => PersistenceValueFormats.ParseDecimal("1e2"));

        var id = Guid.Parse("80A1D29C-52E0-4D11-B964-2D4B081EFB70");
        Assert.Equal("80a1d29c-52e0-4d11-b964-2d4b081efb70", PersistenceValueFormats.FormatGuid(id));
        Assert.Throws<FormatException>(() => PersistenceValueFormats.ParseGuid("80A1D29C-52E0-4D11-B964-2D4B081EFB70"));

        var instant = new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.FromHours(9));
        Assert.Equal("2026-08-24T00:00:00.0000000Z", PersistenceValueFormats.FormatInstant(instant));
    }

    private static async Task InsertTradeRevisionAsync(
        SqliteConnection connection,
        string id,
        string executionId,
        long revisionNo,
        string? supersedesId,
        string changeKind,
        string? correctionReason)
    {
        const string instant = "2026-08-24T00:00:00.0000000Z";
        await ExecuteAsync(
            connection,
            """
            INSERT INTO trade_execution_revisions(
                id, revision_no, supersedes_id, content_sha256, recorded_at_utc,
                trade_execution_id, executed_at_utc, price, quantity, currency,
                record_disposition, change_kind, user_confirmed_at_utc, correction_reason)
            VALUES (
                $id, $revisionNo, $supersedesId, $hash, $instant,
                $executionId, $instant, '1000', 100, 'JPY',
                'Effective', $changeKind, $instant, $correctionReason);
            """,
            ("$id", id),
            ("$revisionNo", revisionNo),
            ("$supersedesId", supersedesId),
            ("$hash", new string(revisionNo == 1 ? 'a' : 'b', 64)),
            ("$instant", instant),
            ("$executionId", executionId),
            ("$changeKind", changeKind),
            ("$correctionReason", correctionReason));
    }

    private static async Task<string[]> ReadLeadingIndexColumnsAsync(SqliteConnection connection, string table)
    {
        var columns = new List<string>();
        await using var indexList = connection.CreateCommand();
        indexList.CommandText = $"PRAGMA index_list(\"{table}\");";
        await using var indexReader = await indexList.ExecuteReaderAsync();
        var indexNames = new List<string>();
        while (await indexReader.ReadAsync())
        {
            indexNames.Add(indexReader.GetString(indexReader.GetOrdinal("name")));
        }

        await indexReader.DisposeAsync();
        foreach (var indexName in indexNames)
        {
            await using var indexInfo = connection.CreateCommand();
            indexInfo.CommandText = $"PRAGMA index_info(\"{indexName}\");";
            await using var infoReader = await indexInfo.ExecuteReaderAsync();
            if (await infoReader.ReadAsync())
            {
                columns.Add(infoReader.GetString(infoReader.GetOrdinal("name")));
            }
        }

        return columns.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static async Task<string[]> ReadNamesAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names.ToArray();
    }

    private static async Task<int> ExecuteAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    private sealed class MigratedDatabase : IAsyncDisposable
    {
        private MigratedDatabase(SqliteConnection connection, SwingAdviserDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }
        private SwingAdviserDbContext Context { get; }

        public static async Task<MigratedDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SwingAdviserDbContext>()
                .UseSwingAdviserSqlite(connection)
                .Options;
            var context = new SwingAdviserDbContext(options);
            await context.Database.MigrateAsync();
            return new MigratedDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
