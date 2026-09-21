using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jev.Net;

/// <summary>
/// Fills the answer-typed properties of a <see cref="SystemOneResponse"/> subclass from the answers that
/// came back. The one place this library reflects — which is why <c>T</c> carries
/// <see cref="DynamicallyAccessedMembersAttribute"/> and why the AOT smoke test exercises this path.
/// </summary>
internal static class AnswerBinder
{
    /// <summary>Copies the envelope onto a new <typeparamref name="T"/> and fills its answer properties by name.</summary>
    public static T Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(SystemOneResponse source)
        where T : SystemOneResponse, new()
    {
        var target = new T
        {
            Model = source.Model,
            Answers = source.Answers,
            Usage = source.Usage,
            RequestId = source.RequestId,
        };

        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!typeof(Answer).IsAssignableFrom(property.PropertyType) || property.SetMethod is null) continue;

            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
            var optional = property.GetCustomAttribute<OptionalAnswerAttribute>() is not null;

            if (!TryFind(source.Answers, name, out var answer))
            {
                if (optional) continue;
                throw new JevResponseValidationException(
                    $"The response has no answer named '{name}' for {typeof(T).Name}.{property.Name}.", $"answers.{name}");
            }

            if (!property.PropertyType.IsInstanceOfType(answer))
                throw new JevResponseValidationException(
                    $"Answer '{name}' is {answer.GetType().Name}, not {property.PropertyType.Name}.", $"answers.{name}");

            property.SetValue(target, answer);
        }

        return target;
    }

    /// <summary>Exact, then case-insensitive, then snake_case — the same order the official SDKs use.</summary>
    private static bool TryFind(IReadOnlyDictionary<string, Answer> answers, string name, out Answer answer)
    {
        if (answers.TryGetValue(name, out answer!)) return true;

        foreach (var (key, value) in answers)
        {
            if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) continue;
            answer = value;
            return true;
        }

        var snake = JsonNamingPolicy.SnakeCaseLower.ConvertName(name);
        return answers.TryGetValue(snake, out answer!);
    }
}
