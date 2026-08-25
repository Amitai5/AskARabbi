namespace AskARabbiPrototype;

internal sealed record ConsoleCommandParseResult(ConsoleCommand? Command, string? Error)
{
    internal bool IsSuccess => Command is not null && Error is null;
}
