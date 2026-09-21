namespace Jev.Net;

/// <summary>Answer to a <see cref="ChoiceQuestion"/>.</summary>
public sealed record ChoiceAnswer : Answer
{
    /// <summary>Name of the most probable option.</summary>
    public required string Choice { get; init; }

    /// <summary>Confidence in <see cref="Choice"/>, from 0 to 1.</summary>
    public required double Confidence { get; init; }

    /// <summary>Probability of every option, keyed by option name.</summary>
    public required IReadOnlyDictionary<string, double> Probabilities { get; init; }

    /// <summary>Maps <see cref="Choice"/> back to <typeparamref name="TEnum"/>.</summary>
    /// <exception cref="JevResponseValidationException">The chosen name is not a member of <typeparamref name="TEnum"/>.</exception>
    public TEnum As<TEnum>() where TEnum : struct, Enum =>
        EnumNames.FromWire<TEnum>(Choice)
        ?? throw new JevResponseValidationException($"Choice '{Choice}' is not a member of {typeof(TEnum).Name}.", "choice");
}
