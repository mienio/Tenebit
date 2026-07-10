namespace Tenebit.Domain.JobProfiles;

public sealed class JobProfileAssetCategory
{
    private JobProfileAssetCategory() { }
    public JobProfileAssetCategory(Guid jobProfileId, Guid assetCategoryId)
    {
        JobProfileId = jobProfileId;
        AssetCategoryId = assetCategoryId;
    }
    public Guid JobProfileId { get; private set; }
    public Guid AssetCategoryId { get; private set; }
}
