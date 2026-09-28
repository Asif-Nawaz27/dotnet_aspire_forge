namespace App.Domain.Exceptions;

// Base type for expected failures the API maps to a specific status code, rather than a 500.
public abstract class DomainException(string message, Exception? innerException = null)
    : Exception(message, innerException);
