using PadronWtd.Infrastucture.Logging;
using System;
using System.IO;
using System.Text;

namespace PadronWtd.Infrastructure.Logging
{
    internal class FileLogger : ILogger
    {
        private readonly string _filePath;
        private readonly object _lock = new object();

        public FileLogger(string filePath)
        {
            _filePath = filePath;
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch { }
        }

        private void Write(string level, string message, Exception ex = null)
        {
            var line = new StringBuilder();
            line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            line.Append(" | ");
            line.Append(level.PadRight(5));
            line.Append(" | ");
            line.Append(message);

            if (ex != null)
            {
                line.Append(" | EX: ");
                line.Append(ex.GetType().FullName);
                line.Append(" - ");
                line.Append(ex.Message);
                line.Append(" | ");
                line.Append(ex.StackTrace);
            }

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_filePath, line.ToString() + Environment.NewLine, Encoding.UTF8);
                }
                catch { }
            }
        }

        public void Info(string message) => Write("INFO", message);
        public void Warn(string message) => Write("WARN", message);
        public void Error(string message, Exception ex = null) => Write("ERRR", message, ex);
    }
}
