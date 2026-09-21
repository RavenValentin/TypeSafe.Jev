namespace TypeSafe.Jev;

/// <summary>Answer to a <see cref="NoulQuestion"/>.</summary>
public sealed record NoulAnswer : Answer
{
    /// <summary>
    /// Probability of yes/true, from 0 to 1. Values near 1 favour yes, near 0 favour no,
    /// and near 0.5 mean the model is uncertain.
    /// </summary>
    public required double Noul { get; init; }
}
