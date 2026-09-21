using System.Text.Json.Serialization;

namespace TypeSafe.Jev;

/// <summary>An answer from Jev; the concrete type matches the question type.</summary>
[JsonConverter(typeof(AnswerConverter))]
public abstract record Answer;
