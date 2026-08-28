namespace Go212.POS.Application.Exceptions;

public class AppException : Exception
{
    public AppException(string message) : base(message) { }
    public AppException(string message, Exception innerException) : base(message, innerException) { }
}

public class EntityNotFoundException : AppException
{
    public string EntityName { get; }
    public object Key { get; }

    public EntityNotFoundException(string entityName, object key)
        : base($"{entityName} avec l'identifiant '{key}' est introuvable.")
    {
        EntityName = entityName;
        Key = key;
    }
}

public class AppValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public AppValidationException(IDictionary<string, string[]> errors)
        : base("Une ou plusieurs erreurs de validation sont survenues.")
    {
        Errors = new Dictionary<string, string[]>(errors);
    }
}

public class UnauthorizedOperationException : AppException
{
    public UnauthorizedOperationException(string message = "Vous n'avez pas les autorisations nécessaires pour effectuer cette action.")
        : base(message) { }
}

public class BusinessRuleViolationException : AppException
{
    public BusinessRuleViolationException(string message) : base(message) { }
}

public class SessionAlreadyOpenException : AppException
{
    public SessionAlreadyOpenException(string message = "Une session de caisse est déjà ouverte.")
        : base(message) { }
}

public class SessionNotOpenException : AppException
{
    public SessionNotOpenException(string message = "Aucune session de caisse n'est actuellement ouverte. Veuillez ouvrir la caisse avant d'encaisser.")
        : base(message) { }
}

public class HardwareDeviceException : AppException
{
    public string DeviceType { get; }

    public HardwareDeviceException(string deviceType, string message, Exception? inner = null)
        : base($"Erreur du périphérique [{deviceType}]: {message}", inner!)
    {
        DeviceType = deviceType;
    }
}
