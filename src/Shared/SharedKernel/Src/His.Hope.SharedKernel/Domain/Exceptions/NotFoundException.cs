namespace His.Hope.SharedKernel.Domain.Exceptions;

public class NotFoundException : KeyNotFoundException
{
    public NotFoundException() : base() { }

    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.") { }

    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException) { }
}
