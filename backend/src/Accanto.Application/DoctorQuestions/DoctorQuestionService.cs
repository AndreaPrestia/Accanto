using Accanto.Application.Common.Authorization;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Validation;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.DoctorQuestions;

public class DoctorQuestionService : IDoctorQuestionService
{
    private readonly IAccantoDbContext _db;
    private readonly ICareCircleAuthorization _auth;
    private readonly IValidator<CreateDoctorQuestionRequest> _createValidator;
    private readonly IValidator<UpdateDoctorQuestionRequest> _updateValidator;

    public DoctorQuestionService(
        IAccantoDbContext db,
        ICareCircleAuthorization auth,
        IValidator<CreateDoctorQuestionRequest> createValidator,
        IValidator<UpdateDoctorQuestionRequest> updateValidator)
    {
        _db = db;
        _auth = auth;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<DoctorQuestionDto>> ListAsync(Guid userId, Guid careCircleId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Viewer, cancellationToken);
        var rows = await _db.DoctorQuestions
            .Where(q => q.CareCircleId == careCircleId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<DoctorQuestionDto> CreateAsync(Guid userId, Guid careCircleId, CreateDoctorQuestionRequest request, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);
        await _createValidator.EnsureValidAsync(request, cancellationToken);

        var q = new DoctorQuestion
        {
            Id = Guid.NewGuid(),
            CareCircleId = careCircleId,
            CreatedByUserId = userId,
            Question = request.Question.Trim(),
            Category = request.Category,
            Status = DoctorQuestionStatus.ToAsk,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.DoctorQuestions.Add(q);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(q);
    }

    public async Task<DoctorQuestionDto> UpdateAsync(Guid userId, Guid careCircleId, Guid questionId, UpdateDoctorQuestionRequest request, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);
        await _updateValidator.EnsureValidAsync(request, cancellationToken);

        var q = await _db.DoctorQuestions.FirstOrDefaultAsync(x => x.Id == questionId && x.CareCircleId == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Domanda non trovata.");

        q.Question = request.Question.Trim();
        q.Category = request.Category;
        q.Status = request.Status;
        q.AnswerNotes = string.IsNullOrWhiteSpace(request.AnswerNotes) ? null : request.AnswerNotes.Trim();
        q.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Map(q);
    }

    public async Task DeleteAsync(Guid userId, Guid careCircleId, Guid questionId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);
        var q = await _db.DoctorQuestions.FirstOrDefaultAsync(x => x.Id == questionId && x.CareCircleId == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Domanda non trovata.");
        _db.DoctorQuestions.Remove(q);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static DoctorQuestionDto Map(DoctorQuestion q) => new(
        q.Id, q.CareCircleId, q.CreatedByUserId, q.Question, q.Category, q.Status, q.AnswerNotes, q.CreatedAt, q.UpdatedAt);
}
