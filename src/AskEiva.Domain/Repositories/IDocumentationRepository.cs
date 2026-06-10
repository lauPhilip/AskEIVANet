using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.ValueObjects;

namespace AskEiva.Domain.Repositories;

public interface IDocumentationRepository
{
    Task UpsertDocumentationAsync(AskEiva.Domain.Entities.DocumentationNode docNode);
    
    Task BatchIngestDocChunksAsync(IEnumerable<TextChunk> chunks, string documentType, List<string> globalTags);
}