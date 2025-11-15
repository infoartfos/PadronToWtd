namespace PadronWtd.Infrastucture.Logging
{
    public interface ILogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, System.Exception ex = null);
    }
}
