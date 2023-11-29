namespace EC.BatchServices.LetterGenerator.Interfaces
{
    public interface ILoggerAdapter<T>
    {
        void LogInformation(string message);
        void LogError(string message, Exception exception = null);
    }
}
