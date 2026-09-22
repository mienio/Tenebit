using System.Reflection;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Common;

namespace Tenebit.Tests;

/// <summary>
/// QA BUG-001: a valid confirmation was reported as "INTEGRITY COMPROMISED". The seal hashed the raw clock
/// value at 100-nanosecond precision, PostgreSQL's timestamptz stored only microseconds, and every later
/// verification recomputed the hash from the truncated timestamp and got a different digest.
/// </summary>
public class AcceptanceSealIntegrityTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid PersonId = Guid.NewGuid();

    /// <summary>What the database does to a timestamptz: keep microseconds, drop the rest.</summary>
    private static DateTimeOffset AsStoredByDatabase(DateTimeOffset value) => value.AddTicks(-(value.Ticks % 10));

    private static Assignment NewAssignment() =>
        new(OrganizationId, PersonId, "TEN-20260922-TEST", DateTimeOffset.UtcNow, null, null, "tester");

    /// <summary>Writes a private setter, the way EF does when it materializes a row.</summary>
    private static void Overwrite(object target, string property, object? value) =>
        target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(target, [value]);

    /// <summary>
    /// Reproduces a seal as the pre-fix code wrote it: the hash covers the raw 100-nanosecond timestamp text,
    /// which is what made the digest unreproducible once the database had truncated the column.
    /// </summary>
    private static void SealTheOldWay(Assignment assignment, DateTimeOffset acceptedAt)
    {
        var computeHash = typeof(Assignment)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(m => m.Name == "ComputeHash" && m.GetParameters()[0].ParameterType == typeof(string));
        var legacyHash = (string)computeHash.Invoke(assignment, [acceptedAt.ToUniversalTime().ToString("O"), null, null])!;

        Overwrite(assignment, nameof(Assignment.IntegrityVersion), 3);
        Overwrite(assignment, nameof(Assignment.AcceptanceHash), legacyHash);
        // The database kept only microseconds.
        Overwrite(assignment, nameof(Assignment.AcceptedAt), AsStoredByDatabase(acceptedAt));
    }

    [Fact]
    public void Accept_SealsATimestampTheDatabaseCanStoreExactly()
    {
        var assignment = NewAssignment();
        assignment.AddAsset(Guid.NewGuid(), "ok");

        // A clock tick that is not a whole microsecond - the case the database cannot store.
        var acceptedAt = new DateTimeOffset(2026, 9, 22, 6, 38, 49, TimeSpan.Zero).AddTicks(7_938_175);
        assignment.Accept(acceptedAt, "203.0.113.7");

        Assert.Equal(0, assignment.AcceptedAt!.Value.Ticks % 10);
        Assert.Equal(AsStoredByDatabase(acceptedAt), assignment.AcceptedAt!.Value);
    }

    [Fact]
    public void VerifyIntegrity_HoldsAfterTheTimestampRoundTripsThroughTheDatabase()
    {
        var assignment = NewAssignment();
        assignment.AddAsset(Guid.NewGuid(), "ok");
        var acceptedAt = new DateTimeOffset(2026, 9, 22, 6, 38, 49, TimeSpan.Zero).AddTicks(7_938_175);
        assignment.Accept(acceptedAt, "203.0.113.7");

        // Reload: the database hands back the truncated value.
        Overwrite(assignment, nameof(Assignment.AcceptedAt), AsStoredByDatabase(assignment.AcceptedAt!.Value));

        Assert.True(assignment.VerifyIntegrity());
    }

    [Fact]
    public void VerifyIntegrity_AcceptsALegacySealWrittenBeforeTheTimestampWasNormalized()
    {
        var assignment = NewAssignment();
        assignment.AddAsset(Guid.NewGuid(), "ok");
        var acceptedAt = new DateTimeOffset(2026, 9, 22, 6, 38, 49, TimeSpan.Zero).AddTicks(7_938_175);
        assignment.Accept(acceptedAt, null);
        SealTheOldWay(assignment, acceptedAt);

        Assert.True(assignment.VerifyIntegrity());
    }

    [Fact]
    public void VerifyIntegrity_StillDetectsRealTamperingOnALegacySeal()
    {
        var assignment = NewAssignment();
        var assetId = Guid.NewGuid();
        assignment.AddAsset(assetId, "ok");
        var acceptedAt = new DateTimeOffset(2026, 9, 22, 6, 38, 49, TimeSpan.Zero).AddTicks(7_938_175);
        assignment.Accept(acceptedAt, null);
        SealTheOldWay(assignment, acceptedAt);

        // Someone edits the signed condition directly in the database.
        var item = assignment.Assets.Single(x => x.AssetId == assetId);
        Overwrite(item, nameof(AssignmentAsset.IssueCondition), "damaged");

        Assert.False(assignment.VerifyIntegrity());
    }

    [Fact]
    public void ProcedureAcceptance_VerifiesAfterTheTimestampRoundTripsThroughTheDatabase()
    {
        var sentAt = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero).AddTicks(1_234_567);
        var acceptance = new ProcedureAcceptance(OrganizationId, Guid.NewGuid(), PersonId, Guid.NewGuid(), sentAt);
        acceptance.Accept(new DateTimeOffset(2026, 9, 22, 6, 38, 49, TimeSpan.Zero).AddTicks(7_938_175), "203.0.113.7");

        Overwrite(acceptance, nameof(ProcedureAcceptance.SentAt), AsStoredByDatabase(sentAt));
        Overwrite(acceptance, nameof(ProcedureAcceptance.AcceptedAt), AsStoredByDatabase(acceptance.AcceptedAt!.Value));

        Assert.True(acceptance.VerifyIntegrity());
    }

    [Fact]
    public void LegacyTexts_CoverEverySubMicrosecondValueTheDatabaseDropped()
    {
        var value = new DateTimeOffset(2026, 9, 22, 6, 38, 49, TimeSpan.Zero).AddTicks(7_938_175);
        var stored = AsStoredByDatabase(value);

        var candidates = IntegritySealTimestamp.LegacyTexts(stored).ToList();

        Assert.Equal(10, candidates.Count);
        Assert.Equal(candidates.Count, candidates.Distinct().Count());
        Assert.Contains(value.ToUniversalTime().ToString("O"), candidates);
    }
}
