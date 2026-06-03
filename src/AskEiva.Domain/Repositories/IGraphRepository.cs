using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Repositories;

public interface IGraphRepository
{
    Task<bool> InsertChainAsync(GraphContextChain chain);
    Task<List<GraphContextChain>> SearchGraphContextAsync(ReadOnlyMemory<float> queryVector, int maxResults);
    Task<List<GraphContextChain>> GetChainsByTicketAsync(string ticketId);
    Task<bool> HasTicketBeenProcessedAsync(string ticketId);
}