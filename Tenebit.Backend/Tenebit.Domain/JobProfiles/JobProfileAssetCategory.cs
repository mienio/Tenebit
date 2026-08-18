namespace Tenebit.Domain.JobProfiles;

public sealed class JobProfileAssetCategory
{
    private JobProfileAssetCategory() { }
    public JobProfileAssetCategory(Guid organizationId, Guid jobProfileId, Guid assetCategoryId)
    {
        OrganizationId = organizationId;
        JobProfileId = jobProfileId;
        AssetCategoryId = assetCategoryId;
    }
    public Guid OrganizationId { get; private set; }
    public Guid JobProfileId { get; private set; }
    public Guid AssetCategoryId { get; private set; }
}
