using System.Diagnostics.CodeAnalysis;

namespace Jev.Net;

/// <summary>Client for Jev, TypeSafe AI's System One model.</summary>
public interface IJevClient
{
    /// <summary>Answers <paramref name="questions"/> about <paramref name="state"/>.</summary>
    /// <param name="state">Text, an object, an array, or <c>null</c>. Use <see cref="JsonContent.From{T}(T, System.Text.Json.Serialization.Metadata.JsonTypeInfo{T})"/> for your own types.</param>
    /// <param name="questions">Nonempty questions keyed by the names their answers will carry.</param>
    /// <param name="options">Per-call overrides; <c>null</c> uses the client's settings.</param>
    /// <param name="cancellationToken">Cancels the whole call, retries and waits included.</param>
    Task<SystemOneResponse> SystemOneAsync(
        JsonContent state,
        IReadOnlyDictionary<string, Question> questions,
        JevRequestOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers <paramref name="questions"/> and fills the answer-typed properties of <typeparamref name="T"/>
    /// from the answers of the same name.
    /// </summary>
    /// <typeparam name="T">
    /// A <see cref="SystemOneResponse"/> subclass with settable answer properties. Each is required unless it
    /// carries <see cref="OptionalAnswerAttribute"/>.
    /// </typeparam>
    Task<T> SystemOneAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        JsonContent state,
        IReadOnlyDictionary<string, Question> questions,
        JevRequestOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : SystemOneResponse, new();

    /// <summary>Lists the models and aliases available to the account.</summary>
    Task<IReadOnlyList<ModelCard>> ListModelsAsync(JevRequestOptions? options = null, CancellationToken cancellationToken = default);
}
