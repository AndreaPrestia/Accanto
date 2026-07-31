namespace Accanto.Admin.Domain.Enums;

/// <summary>Stato del ciclo di vita di una <see cref="AdminOperationType"/> richiesta.</summary>
public enum AdminOperationStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
