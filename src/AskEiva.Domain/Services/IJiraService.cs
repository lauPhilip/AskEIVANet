namespace AskEiva.Domain.Services;

// A dedicated parameter container to enforce strict, un-swappable types
public class JiraQueryOptions
{
    public string Jql { get; set; } = string.Empty;
    public int StartAt { get; set; } = 0;
    public int MaxResults { get; set; } = 100;
}

public interface IJiraService
{
    Task<JiraSearchResponse?> GetIssuesPageAsync(JiraQueryOptions options);
}