namespace PlayerScope.Handlers;

internal sealed class PlayerMapping
{
    public required ulong? AccountId { get; init; }
    public required ulong ContentId { get; init; }
    public required string PlayerName { get; init; } = string.Empty;
    public ushort? WorldId { get; init; }
}
