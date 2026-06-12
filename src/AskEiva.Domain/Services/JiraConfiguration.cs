namespace AskEiva.Domain.Services;

/// <summary>
/// Holds the application configuration parameters and secret tokens needed to authenticate 
/// and connect with external Atlassian Jira cloud services.
/// </summary>
public class JiraConfiguration
{
    /// <summary>
    /// Gets or sets the target base uniform resource locator address of the corporate Jira cloud workspace.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authorized user account email identity utilized to establish connection permissions.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the secure API access token or cryptographic key used to authorize requests against the Jira platform.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;
}