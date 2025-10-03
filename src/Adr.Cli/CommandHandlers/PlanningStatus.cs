namespace Adr.Cli.CommandHandlers;

public enum PlanningStatus
{
    None = 0,
    New = 1,
    OnHold = 2,
    Planned = 3,
    Active = 4,
    Related = 5,
    ReviewPending = 6,
    ReviewComplete = 7,
    AcceptancePending = 8,
    Completed = 9,
    Abandoned = 10
}