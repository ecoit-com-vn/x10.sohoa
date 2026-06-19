namespace EvnHanoi.Infrastructure.Messaging;

public record DossierChangedEvent(
    string DossierId,
    string Action,
    string MessageId,
    DateTime OccurredAt
);

public static class DossierChangedActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string FormDataSaved = "FormDataSaved";
    public const string WorkflowChanged = "WorkflowChanged";
    public const string Deleted = "Deleted";
}

public static class DossierMessaging
{
    public const string IndexQueue = "dossier_index_queue";
    public const string IndexName = "dossier_index";
}
