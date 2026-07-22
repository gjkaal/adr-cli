using System.Collections.Generic;
using System.Threading.Tasks;

using Adr.Cli.CommandHandlers;

using McpCore;

namespace Adr.Cli.Ai;

/// <summary>
/// A condensed view of an existing task, used as grounding context when drafting a new proposal.
/// </summary>
public class TaskSummary
{
    public int RecordId { get; set; }
    public string Title { get; set; } = string.Empty;
    public PlanningStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// AI-drafted content for the Description and Details sections of a task.
/// </summary>
public class TaskProposal
{
    public string Description { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Drafts Description/Details content for a new task from its title and description. A task
/// describes a concrete unit of work to be done - contrast with <see cref="IAdrProposalGenerator" />,
/// which drafts a decision and its consequences. Implementations are swappable per
/// <see cref="Adr.Cli.AiProviderSettings" />; when AI is not configured, the registered
/// implementation is a no-op that always fails, so callers never need a null check.
/// </summary>
public interface ITaskProposalGenerator
{
    /// <summary>
    /// Draft a proposal for a new task.
    /// </summary>
    /// <param name="title">
    /// The title for the new task.
    /// </param>
    /// <param name="description">
    /// The user-authored description for the new task, if any. When empty, the generator drafts one
    /// instead of leaving it blank.
    /// </param>
    /// <param name="existingTasks">
    /// Condensed summaries of existing tasks, so the generator can stay consistent with related work.
    /// </param>
    /// <param name="templateType">
    /// The task's <c>TemplateType</c> (e.g. "Task"), used to look up the matching on-disk template as
    /// a structure/tone example for the model. Best-effort - implementations should tolerate a
    /// missing template file.
    /// </param>
    Task<Response<TaskProposal>> GenerateAsync(string title, string description, IReadOnlyList<TaskSummary> existingTasks, string templateType);
}
