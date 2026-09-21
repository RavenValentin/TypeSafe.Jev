namespace TypeSafe.Jev;

/// <summary>An available model or alias, as returned by <see cref="IJevClient.ListModelsAsync"/>.</summary>
public sealed record ModelCard
{
    /// <summary>Model ID or alias usable in requests, e.g. <c>jev-latest</c>.</summary>
    public string Name { get; init; } = "";

    /// <summary>Purpose and capabilities.</summary>
    public string? Description { get; init; }

    /// <summary>Release timestamp as reported by the API.</summary>
    public string? ReleaseDate { get; init; }
}

/// <summary>The models endpoint answers with a bare array; some deployments wrap it in <c>data</c>.</summary>
internal sealed record ModelList
{
    public List<ModelCard>? Data { get; init; }
    public List<ModelCard>? Models { get; init; }
}
