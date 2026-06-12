using System;

namespace AskEiva.Domain.Entities;

/// <summary>
/// Represents an authorized administrative or engineering user within the platform ecosystem.
/// </summary>
public class ApplicationUser
{
    /// <summary>
    /// Gets or sets the unique primary identifier tracking this specific user record.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the verified corporate email address used as the login account credential.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the secure cryptographic hash of the user account password string.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the universal date and time stamp indicating when the user profile was provisioned.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}