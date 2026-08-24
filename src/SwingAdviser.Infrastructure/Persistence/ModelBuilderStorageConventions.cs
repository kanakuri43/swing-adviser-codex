using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SwingAdviser.Infrastructure.Persistence;

internal static class ModelBuilderStorageConventions
{
    public static void ApplySwingAdviserStorageConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName()
                ?? throw new InvalidOperationException($"{entityType.Name} is not mapped to a table.");

            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            var primaryKey = entityType.FindPrimaryKey();
            primaryKey?.SetName($"pk_{tableName}");

            foreach (var index in entityType.GetIndexes())
            {
                var prefix = index.IsUnique ? "ux" : "ix";
                var columns = string.Join(
                    "_",
                    index.Properties.Select(property => property.GetColumnName()));
                index.SetDatabaseName($"{prefix}_{tableName}_{columns}");
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
                var principalTableName = foreignKey.PrincipalEntityType.GetTableName()
                    ?? throw new InvalidOperationException(
                        $"{foreignKey.PrincipalEntityType.Name} is not mapped to a table.");
                var columns = string.Join(
                    "_",
                    foreignKey.Properties.Select(property => property.GetColumnName()));
                foreignKey.SetConstraintName($"fk_{tableName}_{principalTableName}_{columns}");
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        var builder = new StringBuilder(value.Length + 8);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                var previous = value[index - 1];
                var nextIsLower = index + 1 < value.Length && char.IsLower(value[index + 1]);
                if (char.IsLower(previous) || char.IsDigit(previous) || nextIsLower)
                {
                    builder.Append('_');
                }
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
