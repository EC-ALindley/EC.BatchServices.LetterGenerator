using EC.BatchServices.LetterGenerator.Interfaces;

namespace EC.BatchServices.LetterGenerator.Repositories
{
    public class NLogAdapter<T> : ILoggerAdapter<T>
    {
        private readonly NLog.ILogger _logger = NLog.LogManager.GetLogger(typeof(T).FullName);

        public void LogInformation(string message)
        {
            _logger.Info(message);
        }

        public void LogError(string message, Exception exception = null)
        {
            if (exception != null)
            {
                _logger.Error(exception, message);
            }
            else
            {
                _logger.Error(message);
            }
        }

        // ... other methods implementations
    }

}
