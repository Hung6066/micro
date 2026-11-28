namespace His.Hope.SharedKernel.Domain.Exceptions;

public class ConflictException : InvalidOperationException
{
    public ConflictException(string message) : base(message) { }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException) { }
}
