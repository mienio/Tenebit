namespace Tenebit.Tests.Integration;

/// <summary>
/// All PostgreSQL integration classes share the same physical test database. Keeping only these classes
/// in one xUnit collection prevents parallel schema/data interference while ordinary unit tests remain parallel.
/// Individual tests may still create intentional concurrency inside a single test (for example refresh/evidence races).
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresIntegrationCollection
{
    public const string Name = "PostgresIntegration";
}
