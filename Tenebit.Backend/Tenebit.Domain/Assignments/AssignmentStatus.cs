namespace Tenebit.Domain.Assignments;

public enum AssignmentStatus
{
    Draft = 0,
    AwaitingAcceptance = 1,
    Accepted = 2,
    Returned = 3,
    Cancelled = 4,
    Overdue = 5
}
