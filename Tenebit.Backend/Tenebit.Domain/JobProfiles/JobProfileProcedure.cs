namespace Tenebit.Domain.JobProfiles;

public sealed class JobProfileProcedure
{
    private JobProfileProcedure() { }
    public JobProfileProcedure(Guid organizationId, Guid jobProfileId, Guid procedureId)
    {
        OrganizationId = organizationId;
        JobProfileId = jobProfileId;
        ProcedureId = procedureId;
    }
    public Guid OrganizationId { get; private set; }
    public Guid JobProfileId { get; private set; }
    public Guid ProcedureId { get; private set; }
}
