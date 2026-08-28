namespace AskARabbiLIB.Persistence.Mongo;

/// <summary>Indicates that application persistence has not been configured.</summary>
public sealed class PersistenceUnavailableException : InvalidOperationException
{
    /// <summary>Initializes the exception with a safe configuration message.</summary>
    public PersistenceUnavailableException() : base("Application persistence is unavailable. Configure MongoDB:ConnectionString and MongoDB:DatabaseName.")
    {
    }
}
