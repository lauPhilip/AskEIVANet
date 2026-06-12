using System.Threading.Tasks;

namespace AskEiva.Domain.Services;

/// <summary>
/// A dedicated configuration parameter container utilized to enforce type-safe constraints 
/// when submitting lookup criteria to Atlassian Jira search APIs.
/// </summary>
public class JiraQueryOptions
{
    /// <summary>
    /// Gets or sets the formal Jira Query Language (JQL) constraint text string used to filter targets.
    /// </summary>
    public string Jql { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the page index offset sequence index from which data extraction loops should commence.
    /// </summary>
    public int StartAt { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum relative limit number of issue cards requested per page layer transaction.
    /// </summary>
    public int MaxResults { get; set; } = 100;
}

/// <summary>
/// Defines the external API connector contracts tasked with querying, paginating, and pulling 
/// software tracking issue cards out of Atlassian Jira cloud instances.
/// </summary>
public interface IJiraService
{
    /// <summary>
    /// Queries the targeted issue tracking instance using structured JQL parameters to fetch an individual paginated block of data.
    /// </summary>
    /// <param name="options">The type-safe query parameters specifying JQL rules, start indices, and batch sizes.</param>
    /// <returns>A deserialized response transfer object containing issue items and total record counts, or null if execution drops.</returns>
    Task<JiraSearchResponse?> GetIssuesPageAsync(JiraQueryOptions options);
}