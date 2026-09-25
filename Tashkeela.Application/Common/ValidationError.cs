using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Tashkeela.Application.Common;

/// <summary>
/// For IValidatableObject rules. MVC reports their member names verbatim (unlike attribute errors, which get the JSON
/// name), so this converts nameof(Property) to the name clients send: "EndsAt" → "endsAt".
/// </summary>
internal static class ValidationError
{
    public static ValidationResult For(string propertyName, string message) =>
        new(message, [JsonNamingPolicy.CamelCase.ConvertName(propertyName)]);
}
