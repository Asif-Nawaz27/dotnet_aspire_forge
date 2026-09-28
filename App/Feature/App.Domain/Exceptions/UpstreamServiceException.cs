namespace App.Domain.Exceptions;

// A dependency we don't control (e.g. the GitHub API) failed or refused the request.
public sealed class UpstreamServiceException(
    string service, string message, TimeSpan? retryAfter = null, Exception? innerException = null)
    : DomainException($"{service}: {message}", innerException)
{
    public string Service { get; } = service;

    public TimeSpan? RetryAfter { get; } = retryAfter;
}
