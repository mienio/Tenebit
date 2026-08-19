using System.Linq.Expressions;
using Tenebit.Domain.Evidence;
using Tenebit.Infrastructure.Repositories;

namespace Tenebit.Tests;

public sealed class AssetEvidenceMetadataProjectionTests
{
    [Fact]
    public void ListMetadataProjection_NeverReadsBinaryContent()
    {
        var visitor = new MemberCollector();
        visitor.Visit(AssetEvidenceMetadataProjection.Select);

        Assert.DoesNotContain(nameof(AssetEvidence.Content), visitor.MemberNames);
        Assert.Contains(nameof(AssetEvidence.FileName), visitor.MemberNames);
        Assert.Contains(nameof(AssetEvidence.SizeBytes), visitor.MemberNames);
        Assert.Contains(nameof(AssetEvidence.Sha256), visitor.MemberNames);
    }

    private sealed class MemberCollector : ExpressionVisitor
    {
        public HashSet<string> MemberNames { get; } = new(StringComparer.Ordinal);

        protected override Expression VisitMember(MemberExpression node)
        {
            MemberNames.Add(node.Member.Name);
            return base.VisitMember(node);
        }
    }
}
