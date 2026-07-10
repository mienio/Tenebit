namespace Tenebit.Domain.JobProfiles;

public sealed class JobProfileProcedure
{
    private JobProfileProcedure() { }
    public JobProfileProcedure(Guid jobProfileId, Guid procedureId)
    {
        JobProfileId = jobProfileId;
        ProcedureId = procedureId;
    }
    public Guid JobProfileId { get; private set; }
    public Guid ProcedureId { get; private set; }
}
