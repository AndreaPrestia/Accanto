namespace Accanto.Admin.Application.Common;

/// <summary>Eccezioni applicative del control plane admin. Mappate a status code dall'ErrorHandlingMiddleware dell'Admin API.</summary>
public class AdminNotFoundException : Exception
{
    public AdminNotFoundException(string message) : base(message) { }
}

public class AdminForbiddenException : Exception
{
    public AdminForbiddenException(string message) : base(message) { }
}

public class AdminUnauthorizedException : Exception
{
    public AdminUnauthorizedException(string message) : base(message) { }
}

public class AdminValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public AdminValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public AdminValidationException(string message, IReadOnlyDictionary<string, string[]> errors) : base(message)
    {
        Errors = errors;
    }
}
