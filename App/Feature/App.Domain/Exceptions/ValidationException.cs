namespace App.Domain.Exceptions;

public sealed class ValidationException(string field, string message)
    : DomainException(message)
{
    public string Field { get; } = field;
}
