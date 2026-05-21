using Accanto.Application.Common.Exceptions;
using FluentValidation;
using FluentValidation.Results;

namespace Accanto.Application.Common.Validation;

public static class ValidationExtensions
{
    public static AppValidationException ToAppException(this ValidationResult result, string message = "Dati non validi.") =>
        new(message,
            result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    public static async Task EnsureValidAsync<T>(this IValidator<T> validator, T instance, CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (!result.IsValid) throw result.ToAppException();
    }
}
