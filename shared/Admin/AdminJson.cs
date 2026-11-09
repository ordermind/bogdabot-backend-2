using System.Text.Json;

namespace Bogdabot.Shared.Admin;

public static class AdminJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
