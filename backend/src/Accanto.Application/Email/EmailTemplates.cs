using System.Net;

namespace Accanto.Application.Email;

/// <summary>
/// Modelli HTML inline per le email transazionali. Italiano, sobrio.
/// </summary>
public static class EmailTemplates
{
    public static string TimelineEntryCreated(string circleName, string authorName, string entryTitle) =>
        Layout(
            "Nuova voce nel diario",
            $"<p><strong>{H(authorName)}</strong> ha aggiunto una voce nel diario di <strong>{H(circleName)}</strong>:</p>" +
            $"<p style=\"padding:12px;background:#f5f5f5;border-radius:6px\">{H(entryTitle)}</p>" +
            "<p>Apri Accanto per leggerla.</p>");

    public static string SharedUpdateCreated(string circleName, string authorName) =>
        Layout(
            "Nuovo aggiornamento condiviso",
            $"<p><strong>{H(authorName)}</strong> ha pubblicato un nuovo aggiornamento in <strong>{H(circleName)}</strong>.</p>" +
            "<p>Apri Accanto per leggerlo.</p>");

    public static string DoctorQuestionAnswered(string circleName, string question) =>
        Layout(
            "Domanda al medico aggiornata",
            $"<p>La domanda al medico in <strong>{H(circleName)}</strong> è stata segnata come risposta:</p>" +
            $"<p style=\"padding:12px;background:#f5f5f5;border-radius:6px\">{H(question)}</p>");

    public static string InviteAccepted(string circleName, string newMemberName) =>
        Layout(
            "Nuova persona nel cerchio",
            $"<p><strong>{H(newMemberName)}</strong> ha accettato l'invito ed è entrata nel cerchio <strong>{H(circleName)}</strong>.</p>");

    public static string PasswordChanged() =>
        Layout(
            "Password modificata",
            "<p>La password del tuo account Accanto è stata appena cambiata.</p>" +
            "<p>Se non sei stato tu, contatta subito l'amministratore del servizio e cambia di nuovo la password.</p>");

    private static string Layout(string title, string innerHtml) =>
        "<!doctype html><html><body style=\"font-family:Inter,Segoe UI,Arial,sans-serif;color:#222;max-width:560px;margin:0 auto;padding:24px\">" +
        $"<h2 style=\"margin:0 0 16px\">{H(title)}</h2>" +
        innerHtml +
        "<hr style=\"margin-top:32px;border:none;border-top:1px solid #e5e5e5\"/>" +
        "<p style=\"color:#888;font-size:12px\">Ricevi questa email perché sei iscritto ad Accanto. " +
        "Puoi gestire le tue preferenze di notifica dalla sezione Account.</p>" +
        "</body></html>";

    private static string H(string s) => WebUtility.HtmlEncode(s ?? string.Empty);
}
