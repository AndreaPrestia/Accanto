using Accanto.Domain.Enums;

namespace Accanto.Application.DoctorQuestions;

public class StaticDoctorQuestionTemplateProvider : IDoctorQuestionTemplateProvider
{
    private static readonly IReadOnlyList<DoctorQuestionTemplateDto> Templates = new[]
    {
        new DoctorQuestionTemplateDto(DoctorQuestionCategory.Pain, "Dolore", new[]
        {
            "Come possiamo capire se il dolore è controllato?",
            "Cosa dobbiamo fare se il dolore peggiora di notte?",
            "Ci sono farmaci al bisogno? Quando vanno usati?"
        }),
        new DoctorQuestionTemplateDto(DoctorQuestionCategory.Nutrition, "Alimentazione e idratazione", new[]
        {
            "È normale che mangi o beva meno?",
            "Dobbiamo insistere o rispettare il suo rifiuto?",
            "Ci sono segnali di disagio da osservare?"
        }),
        new DoctorQuestionTemplateDto(DoctorQuestionCategory.PalliativeCare, "Cure palliative", new[]
        {
            "È possibile attivare un supporto di cure palliative?",
            "Chi possiamo chiamare se peggiora a casa?",
            "Quali sintomi dobbiamo aspettarci?"
        }),
        new DoctorQuestionTemplateDto(DoctorQuestionCategory.Discharge, "Dimissioni", new[]
        {
            "Cosa dobbiamo avere pronto a casa?",
            "Quali farmaci servono?",
            "A chi dobbiamo rivolgerci in caso di emergenza?"
        }),
        new DoctorQuestionTemplateDto(DoctorQuestionCategory.Therapy, "Terapia", new[]
        {
            "Quali sono gli effetti attesi e quali quelli da segnalare?",
            "Cosa fare se salta una dose?",
            "Ci sono interazioni con altri farmaci che già prende?"
        }),
        new DoctorQuestionTemplateDto(DoctorQuestionCategory.HomeCare, "Assistenza a casa", new[]
        {
            "Quali servizi domiciliari possiamo attivare?",
            "Possiamo avere un infermiere a domicilio?",
            "Come gestire la routine quotidiana in sicurezza?"
        })
    };

    public IReadOnlyList<DoctorQuestionTemplateDto> GetTemplates() => Templates;
}
