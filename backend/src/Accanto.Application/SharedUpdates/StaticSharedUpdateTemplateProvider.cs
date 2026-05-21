namespace Accanto.Application.SharedUpdates;

public class StaticSharedUpdateTemplateProvider : ISharedUpdateTemplateProvider
{
    private static readonly IReadOnlyList<SharedUpdateTemplateDto> Templates = new[]
    {
        new SharedUpdateTemplateDto(
            "Situazione delicata",
            "Oggi la situazione resta delicata. Stiamo cercando di concentrarci sul suo comfort e di seguire le indicazioni dei medici. Vi aggiorneremo quando ci saranno novità importanti."
        ),
        new SharedUpdateTemplateDto(
            "Meno telefonate, grazie",
            "In questo momento preferiamo evitare troppe telefonate, ma leggiamo i vostri messaggi. Grazie per la vicinanza."
        ),
        new SharedUpdateTemplateDto(
            "Dopo aver parlato con i medici",
            "Oggi abbiamo parlato con i medici. La situazione è complessa e stiamo cercando di affrontare una cosa alla volta."
        )
    };

    public IReadOnlyList<SharedUpdateTemplateDto> GetTemplates() => Templates;
}
