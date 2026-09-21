namespace Jev.Net;

/// <summary>Client for Jev, TypeSafe AI's System One model.</summary>
public interface IJevClient
{
    /// <summary>Answers <paramref name="questions"/> about <paramref name="state"/>.</summary>
    /// <param name="state">Text, an object, an array, or <c>null</c>; serialized as JSON.</param>
    /// <param name="questions">Nonempty questions keyed by the names used for their answers.</param>
    /// <param name="model">Model override; defaults to <see cref="JevClientOptions.DefaultModel"/>.</param>
    /// <param name="cancellationToken">Cancels the whole call, including retries.</param>
    Task<SystemOneResponse> SystemOneAsync(
        object? state,
        IReadOnlyDictionary<string, Question> questions,
        string? model = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lists models and aliases available to the account.</summary>
    Task<IReadOnlyList<ModelCard>> ListModelsAsync(CancellationToken cancellationToken = default);
}
