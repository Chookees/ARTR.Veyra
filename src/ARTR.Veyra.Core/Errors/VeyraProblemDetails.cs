namespace ARTR.Veyra.Core.Errors;

public sealed record VeyraProblemDetails(
    string? Type,
    string Title,
    int Status,
    string? Detail,
    string? Instance,
    string ErrorCode,
    IReadOnlyDictionary<string, object?> Extensions);
