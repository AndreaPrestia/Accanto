using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Security;
using Accanto.Application.Common.Storage;
using Accanto.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Account;

public class AccountService : IAccountService
{
    private readonly IAccantoDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IFileStorage _storage;
    private readonly IValidator<ChangePasswordRequest> _changeValidator;
    private readonly IValidator<DeleteAccountRequest> _deleteValidator;

    public AccountService(
        IAccantoDbContext db,
        IPasswordHasher hasher,
        IFileStorage storage,
        IValidator<ChangePasswordRequest> changeValidator,
        IValidator<DeleteAccountRequest> deleteValidator)
    {
        _db = db;
        _hasher = hasher;
        _storage = storage;
        _changeValidator = changeValidator;
        _deleteValidator = deleteValidator;
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var v = await _changeValidator.ValidateAsync(request, cancellationToken);
        if (!v.IsValid) throw ToValidation(v);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ForbiddenException("La password attuale non è corretta.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken = default)
    {
        var v = await _deleteValidator.ValidateAsync(request, cancellationToken);
        if (!v.IsValid) throw ToValidation(v);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ForbiddenException("La password non è corretta.");

        // Conservative policy: per non cancellare in modo sorprendente dati su cui altre persone
        // stanno contando, rifiutiamo se l'utente fa parte di un cerchio condiviso con qualcun altro.
        var myCircleIds = await _db.CareCircleMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.CareCircleId)
            .ToListAsync(cancellationToken);

        if (myCircleIds.Count > 0)
        {
            var hasSharedCircle = await _db.CareCircleMembers
                .Where(m => myCircleIds.Contains(m.CareCircleId) && m.UserId != userId)
                .AnyAsync(cancellationToken);

            if (hasSharedCircle)
            {
                throw new ConflictException(
                    "Fai parte di uno o più cerchi insieme ad altre persone. " +
                    "Per eliminare l'account, esci prima da quei cerchi o rimuovi gli altri membri.");
            }
        }

        // A questo punto tutti i cerchi a cui l'utente partecipa sono solo suoi: li elimino interamente.
        if (myCircleIds.Count > 0)
        {
            var documents = await _db.MedicalDocuments
                .Where(d => myCircleIds.Contains(d.CareCircleId))
                .ToListAsync(cancellationToken);

            // Rimuovo prima i file fisici cifrati; eventuali errori non bloccano la pulizia del DB.
            foreach (var doc in documents)
            {
                try { await _storage.DeleteAsync(doc.StoragePath, cancellationToken); }
                catch { /* file gia' assente: ignoriamo */ }
            }

            _db.MedicalDocuments.RemoveRange(documents);

            var timeline = await _db.TimelineEntries
                .Where(t => myCircleIds.Contains(t.CareCircleId))
                .ToListAsync(cancellationToken);
            _db.TimelineEntries.RemoveRange(timeline);

            var questions = await _db.DoctorQuestions
                .Where(q => myCircleIds.Contains(q.CareCircleId))
                .ToListAsync(cancellationToken);
            _db.DoctorQuestions.RemoveRange(questions);

            var updates = await _db.SharedUpdates
                .Where(s => myCircleIds.Contains(s.CareCircleId))
                .ToListAsync(cancellationToken);
            _db.SharedUpdates.RemoveRange(updates);

            var invites = await _db.CareCircleInvites
                .Where(i => myCircleIds.Contains(i.CareCircleId))
                .ToListAsync(cancellationToken);
            _db.CareCircleInvites.RemoveRange(invites);

            var circles = await _db.CareCircles
                .Where(c => myCircleIds.Contains(c.Id))
                .ToListAsync(cancellationToken);
            // I membri vengono eliminati a cascata dalla configurazione EF su CareCircle → Members.
            _db.CareCircles.RemoveRange(circles);
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static AppValidationException ToValidation(FluentValidation.Results.ValidationResult v) =>
        new("Dati non validi.",
            v.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
}
