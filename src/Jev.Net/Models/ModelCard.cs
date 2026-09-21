namespace Jev.Net;

/// <summary>An available model or alias, as returned by <see cref="IJevClient.ListModelsAsync"/>.</summary>
public sealed record ModelCard
{
    /// <summary>Model ID or alias usable in requests, e.g. <c>jev-latest</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Purpose and capabilities.</summary>
    public string? Description { get; init; }

    /// <summary>Release timestamp as reported by the API.</summary>
    public string? ReleaseDate { get; init; }
}
