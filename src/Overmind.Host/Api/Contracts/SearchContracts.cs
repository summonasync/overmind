using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record SearchHitResponse(Guid EntryId, string Title, EntryType Type, string Snippet, double Rank);

public sealed record SearchResponse(string Query, IReadOnlyList<SearchHitResponse> Hits);
