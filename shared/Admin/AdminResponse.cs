namespace Bogdabot.Shared.Admin;

public sealed record AdminResponse(
    bool Success,
    string? Message = null,
    string? Error = null,
    IReadOnlyList<ActorOperationResult>? Results = null
);