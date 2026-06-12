using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AskEiva.Domain.Services;

/// <summary>
/// Defines the external API connector contracts tasked with pulling customer support ticket batches 
/// and conversational message strings from the Freshdesk CRM ecosystem.
/// </summary>
public interface IFreshdeskService
{
    /// <summary>
    /// Fetches an individual paginated block of support tickets using chronological tracking index offsets.
    /// </summary>
    /// <param name="page">The specific page index layer to target for retrieval.</param>
    /// <param name="perPage">The maximum size quantity of ticket entries requested per page layer (defaults to 30).</param>
    /// <returns>A collection sequence of populated ticket transfer data metrics records.</returns>
    Task<IEnumerable<FreshdeskTicketDto>> GetTicketsPageAsync(int page, int perPage = 30);
    
    /// <summary>
    /// Retrieves the collection of sequential thread replies, agent logs, or customer comments associated with a specific ticket.
    /// </summary>
    /// <param name="ticketId">The numerical primary tracking identifier of the target ticket node in Freshdesk.</param>
    /// <returns>A collection sequence of individual conversation reply blocks ordered chronologically.</returns>
    Task<IEnumerable<FreshdeskConversationDto>> GetTicketConversationsAsync(long ticketId);
}

/// <summary>
/// Mapped data transfer contract model reflecting the root ticket structure properties exposed by the external Freshdesk API.
/// </summary>
public class FreshdeskTicketDto
{
    /// <summary>
    /// Gets or sets the primary CRM tracking identifier for the support ticket.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the descriptive title headline or subject text of the support ticket issue card.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw, unstripped main body description plain text detailing the original issue submission.
    /// </summary>
    public string Description_Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the active operational lifecycle index stage value tracking ticket progression status states.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Gets or sets the ticket urgency priority index classification value assigned by CRM routing agents.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the absolute date and time stamp indicating exactly when the ticket record was opened.
    /// </summary>
    public DateTime Created_At { get; set; }

    /// <summary>
    /// Gets or sets the absolute date and time stamp indicating exactly when the ticket record was last updated.
    /// </summary>
    public DateTime Updated_At { get; set; }

    /// <summary>
    /// Gets or sets the metadata grouping labels or tracking tags assigned to this specific support ticket instance.
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Mapped data transfer contract model reflecting an individual response entry or thread reply within a ticket timeline.
/// </summary>
public class FreshdeskConversationDto
{
    /// <summary>
    /// Gets or sets the unique tracking identifier for this specific conversation reply block instance.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the plain-text message contents or statement text extracted from the conversational reply node.
    /// </summary>
    public string Body_Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the response originated from outside the engineering company.
    /// </summary>
    /// <value>True if submitted by an external client or surveyor, false if posted internally by an active support engineer.</value>
    public bool Incoming { get; set; }

    /// <summary>
    /// Gets or sets the absolute date and time stamp indicating exactly when this conversation item was posted.
    /// </summary>
    public DateTime Created_At { get; set; }
}