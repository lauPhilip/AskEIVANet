using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Repositories;

public interface ITicketRepository
{
    Task UpsertTicketAsync(TicketNode ticket);
    Task<IEnumerable<TicketNode>> GetUnprocessedTicketsAsync(int limit);
    Task MarkAsDistilledAsync(string sourceId);
    Task<bool> DoesTicketExistAsync(string sourceId);
    Task BatchIngestTicketNodesAsync(IEnumerable<TicketNode> tickets); 
    Task<List<TicketNode>> GetUnprocessedTicketHeadersAsync(int batchSize);
    Task<string> GetStitchedTicketContentAsync(string sourceId);
}