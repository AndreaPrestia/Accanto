using Accanto.Api.Common;
using Accanto.Application.DoctorQuestions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/care-circles/{careCircleId:guid}/doctor-questions")]
public class DoctorQuestionsController : ControllerBase
{
    private readonly IDoctorQuestionService _svc;
    private readonly ICurrentUser _currentUser;

    public DoctorQuestionsController(IDoctorQuestionService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DoctorQuestionDto>>> List(Guid careCircleId, CancellationToken ct)
        => Ok(await _svc.ListAsync(_currentUser.RequireUserId(), careCircleId, ct));

    [HttpPost]
    public async Task<ActionResult<DoctorQuestionDto>> Create(Guid careCircleId, [FromBody] CreateDoctorQuestionRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateAsync(_currentUser.RequireUserId(), careCircleId, request, ct);
        return Created($"/api/care-circles/{careCircleId}/doctor-questions/{dto.Id}", dto);
    }

    [HttpPut("{questionId:guid}")]
    public async Task<ActionResult<DoctorQuestionDto>> Update(Guid careCircleId, Guid questionId, [FromBody] UpdateDoctorQuestionRequest request, CancellationToken ct)
        => Ok(await _svc.UpdateAsync(_currentUser.RequireUserId(), careCircleId, questionId, request, ct));

    [HttpDelete("{questionId:guid}")]
    public async Task<IActionResult> Delete(Guid careCircleId, Guid questionId, CancellationToken ct)
    {
        await _svc.DeleteAsync(_currentUser.RequireUserId(), careCircleId, questionId, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/doctor-question-templates")]
public class DoctorQuestionTemplatesController : ControllerBase
{
    private readonly IDoctorQuestionTemplateProvider _provider;
    public DoctorQuestionTemplatesController(IDoctorQuestionTemplateProvider provider) { _provider = provider; }

    [HttpGet]
    public ActionResult<IReadOnlyList<DoctorQuestionTemplateDto>> Get() => Ok(_provider.GetTemplates());
}
