namespace App.Domain.Exceptions;

public sealed class NotFoundException(string resource, string key)
    : DomainException($"{resource} '{key}' was not found.");
