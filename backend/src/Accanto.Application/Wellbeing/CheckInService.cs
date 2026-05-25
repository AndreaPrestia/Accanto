using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Validation;
using Accanto.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Wellbeing;

public record CreateCheckInRequest(short Mood, short Energy, short Stress, string? Note);

public record CaregiverCheckInDto(Guid Id, short Mood, short Energy, short Stress, string? Note, DateTimeOffset CreatedAt);

public interface ICheckInService
{
    Task<CaregiverCheckInDto> CreateAsync(Guid userId, CreateCheckInRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CaregiverCheckInDto>> ListAsync(Guid userId, DateTimeOffset? from, DateTimeOffset? to, int take, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}

public class CreateCheckInRequestValidator : AbstractValidator<CreateCheckInRequest>
{
    public CreateCheckInRequestValidator()
    {
        RuleFor(x => x.Mood).InclusiveBetween((short)1, (short)5).WithMessage("Il valore di umore deve essere tra 1 e 5.");
        RuleFor(x => x.Energy).InclusiveBetween((short)1, (short)5).WithMessage("Il valore di energia deve essere tra 1 e 5.");
        RuleFor(x => x.Stress).InclusiveBetween((short)1, (short)5).WithMessage("Il valore di stress deve essere tra 1 e 5.");
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null).WithMessage("La nota non può superare 500 caratteri.");
    }
}

public class CheckInService : ICheckInService
{
    private const int MaxTake = 365;
    private readonly IAccantoDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IValidator<CreateCheckInRequest> _validator;

    public CheckInService(IAccantoDbContext db, TimeProvider clock, IValidator<CreateCheckInRequest> validator)
    {
        _db = db;
        _clock = clock;
        _validator = validator;
    }

    public async Task<CaregiverCheckInDto> CreateAsync(Guid userId, CreateCheckInRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid) throw result.ToAppException();

        var entity = new CaregiverCheckIn
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Mood = request.Mood,
            Energy = request.Energy,
            Stress = request.Stress,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = _clock.GetUtcNow()
        };
        _db.CaregiverCheckIns.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<CaregiverCheckInDto>> ListAsync(Guid userId, DateTimeOffset? from, DateTimeOffset? to, int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0) take = 60;
        if (take > MaxTake) take = MaxTake;

        var query = _db.CaregiverCheckIns.Where(c => c.UserId == userId);
        if (from.HasValue) query = query.Where(c => c.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(c => c.CreatedAt <= to.Value);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.CaregiverCheckIns.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
        if (entity is null) throw new NotFoundException("Check-in non trovato.");
        _db.CaregiverCheckIns.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static CaregiverCheckInDto ToDto(CaregiverCheckIn c) =>
        new(c.Id, c.Mood, c.Energy, c.Stress, c.Note, c.CreatedAt);
}
