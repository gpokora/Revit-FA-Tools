using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Revit_FA_Tools.Core.Services.Interfaces;
using Revit_FA_Tools.Core.Infrastructure.ServiceRegistration;

namespace Revit_FA_Tools.Core.Services.Implementation
{
    /// <summary>
    /// Comprehensive logging service implementation
    /// </summary>
    public class LoggingService : ILoggingService, IDisposable
    {
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();
        private readonly Queue<LogEntry> _logQueue = new Queue<LogEntry>();
        private readonly System.Timers.Timer _flushTimer;
        private bool _disposed = false;

        public LoggingService()
        {
            // Create logs directory in application data folder
            var logsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RevitFATools", "Logs");

            Directory.CreateDirectory(logsDirectory);

            _logFilePath = Path.Combine(logsDirectory, $"RevitFATools_{DateTime.Now:yyyyMMdd}.log");

            // Setup timer to flush logs periodically
            _flushTimer = new System.Timers.Timer(5000); // Flush every 5 seconds
            _flushTimer.Elapsed += (sender, e) => FlushLogs();
            _flushTimer.Start();

            // Log service initialization
            LogInfo("Logging service initialized", GetType().Name);
        }

        // Interface implementations
        public void LogInfo(string message)
        {
            LogMessage(LogLevel.Info, message, null, "");
        }

        public void LogWarning(string message)
        {
            LogMessage(LogLevel.Warning, message, null, "");
        }

        public void LogError(string message, Exception exception = null)
        {
            LogMessage(LogLevel.Error, message, exception, "");
        }

        public void LogDebug(string message)
        {
#if DEBUG
            LogMessage(LogLevel.Debug, message, null, "");
#endif
        }

        // Extended implementations with caller info
        public void LogInfo(string message, [CallerMemberName] string callerName = "")
        {
            LogMessage(LogLevel.Info, message, null, callerName);
        }

        public void LogWarning(string message, [CallerMemberName] string callerName = "")
        {
            LogMessage(LogLevel.Warning, message, null, callerName);
        }

        public void LogError(string message, Exception exception = null, [CallerMemberName] string callerName = "")
        {
            LogMessage(LogLevel.Error, message, exception, callerName);
        }

        public void LogDebug(string message, [CallerMemberName] string callerName = "")
        {
#if DEBUG
            LogMessage(LogLevel.Debug, message, null, callerName);
#endif
        }

        /// <summary>
        /// Logs a message with performance timing
        /// </summary>
        public void LogPerformance(string operationName, TimeSpan duration, string details = null, [CallerMemberName] string callerName = "")
        {
            var message = $"Performance: {operationName} completed in {duration.TotalMilliseconds:F2}ms";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" | {details}";
            }
            LogMessage(LogLevel.Performance, message, null, callerName);
        }

        /// <summary>
        /// Logs method entry for debugging
        /// </summary>
        public void LogMethodEntry(string methodName, object parameters = null, [CallerMemberName] string callerName = "")
        {
#if DEBUG
            var message = $"Entering: {methodName}";
            if (parameters != null)
            {
                message += $" | Parameters: {parameters}";
            }
            LogMessage(LogLevel.Debug, message, null, callerName);
#endif
        }

        /// <summary>
        /// Logs method exit for debugging
        /// </summary>
        public void LogMethodExit(string methodName, object result = null, [CallerMemberName] string callerName = "")
        {
#if DEBUG
            var message = $"Exiting: {methodName}";
            if (result != null)
            {
                message += $" | Result: {result}";
            }
            LogMessage(LogLevel.Debug, message, null, callerName);
#endif
        }

        /// <summary>
        /// Logs validation results
        /// </summary>
        public void LogValidation(string validationContext, bool isValid, IEnumerable<string> errors = null, [CallerMemberName] string callerName = "")
        {
            var message = $"Validation [{validationContext}]: {(isValid ? "PASSED" : "FAILED")}";
            if (!isValid && errors != null)
            {
                message += $" | Errors: {string.Join("; ", errors)}";
            }

            LogMessage(isValid ? LogLevel.Info : LogLevel.Warning, message, null, callerName);
        }

        /// <summary>
        /// Logs business operation results
        /// </summary>
        public void LogBusinessOperation(string operation, bool success, string details = null, [CallerMemberName] string callerName = "")
        {
            var message = $"Business Operation [{operation}]: {(success ? "SUCCESS" : "FAILURE")}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" | {details}";
            }

            LogMessage(success ? LogLevel.Info : LogLevel.Warning, message, null, callerName);
        }

        private void LogMessage(LogLevel level, string message, Exception exception, string callerName)
        {
            if (_disposed) return;

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Exception = exception,
                CallerName = callerName,
                ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId
            };

            lock (_lockObject)
            {
                _logQueue.Enqueue(logEntry);
                
                // Also output to debug console in debug mode
#if DEBUG
                Debug.WriteLine($"[{logEntry.Timestamp:HH:mm:ss.fff}] [{level}] [{callerName}] {message}");
                if (exception != null)
                {
                    Debug.WriteLine($"Exception: {exception}");
                }
#endif

                // Immediate flush for errors and critical messages
                if (level >= LogLevel.Error)
                {
                    FlushLogs();
                }
            }
        }

        private void FlushLogs()
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                if (_logQueue.Count == 0) return;

                try
                {
                    using var writer = new StreamWriter(_logFilePath, append: true);
                    
                    while (_logQueue.Count > 0)
                    {
                        var entry = _logQueue.Dequeue();
                        var logLine = FormatLogEntry(entry);
                        writer.WriteLine(logLine);
                    }
                    
                    writer.Flush();
                }
                catch (Exception ex)
                {
                    // Fallback to debug output if file logging fails
                    Debug.WriteLine($"Failed to write to log file: {ex.Message}");
                }
            }
        }

        private string FormatLogEntry(LogEntry entry)
        {
            var formatted = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level,-11}] [T{entry.ThreadId:D2}] [{entry.CallerName}] {entry.Message}";
            
            if (entry.Exception != null)
            {
                formatted += Environment.NewLine + $"Exception: {entry.Exception}";
            }
            
            return formatted;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            try
            {
                _flushTimer?.Stop();
                _flushTimer?.Dispose();
                
                // Final flush of any remaining logs
                FlushLogs();
                
                LogInfo("Logging service disposed", GetType().Name);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disposing logging service: {ex.Message}");
            }
        }

        #region Helper Classes

        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public LogLevel Level { get; set; }
            public string Message { get; set; }
            public Exception Exception { get; set; }
            public string CallerName { get; set; }
            public int ThreadId { get; set; }
        }

        private enum LogLevel
        {
            Debug = 0,
            Info = 1,
            Performance = 2,
            Warning = 3,
            Error = 4,
            Critical = 5
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for logging service
    /// </summary>
    public static class LoggingServiceExtensions
    {
        /// <summary>
        /// Logs and measures the execution time of an operation
        /// </summary>
        public static T LogAndMeasure<T>(this ILoggingService logger, Func<T> operation, string operationName, [CallerMemberName] string callerName = "")
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                logger.LogDebug($"[{callerName}] Entering: {operationName}");
                var result = operation();
                stopwatch.Stop();
                
                logger.LogInfo($"[{callerName}] Performance: {operationName} completed in {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
                logger.LogDebug($"[{callerName}] Exiting: {operationName}");
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger.LogError($"[{callerName}] Operation {operationName} failed after {stopwatch.Elapsed.TotalMilliseconds:F2}ms", ex);
                throw;
            }
        }

        /// <summary>
        /// Logs and measures the execution time of an async operation
        /// </summary>
        public static async Task<T> LogAndMeasureAsync<T>(this ILoggingService logger, Func<Task<T>> operation, string operationName, [CallerMemberName] string callerName = "")
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                logger.LogDebug($"[{callerName}] Entering: {operationName}");
                var result = await operation();
                stopwatch.Stop();
                
                logger.LogInfo($"[{callerName}] Performance: {operationName} completed in {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
                logger.LogDebug($"[{callerName}] Exiting: {operationName}");
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger.LogError($"[{callerName}] Async operation {operationName} failed after {stopwatch.Elapsed.TotalMilliseconds:F2}ms", ex);
                throw;
            }
        }

        /// <summary>
        /// Creates a disposable scope that logs entry and exit
        /// </summary>
        public static IDisposable CreateMethodScope(this ILoggingService logger, string methodName, object parameters = null, [CallerMemberName] string callerName = "")
        {
            return new MethodScope(logger, methodName, parameters, callerName);
        }

        private class MethodScope : IDisposable
        {
            private readonly ILoggingService _logger;
            private readonly string _methodName;
            private readonly string _callerName;
            private readonly Stopwatch _stopwatch;

            public MethodScope(ILoggingService logger, string methodName, object parameters, string callerName)
            {
                _logger = logger;
                _methodName = methodName;
                _callerName = callerName;
                _stopwatch = Stopwatch.StartNew();
                
                _logger.LogDebug($"[ENTRY] {_methodName} called by {_callerName} with parameters: {string.Join(", ", parameters ?? new object[0])}");
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                _logger.LogDebug($"[PERF] {_methodName} executed in {_stopwatch.Elapsed.TotalMilliseconds:F2}ms");
                _logger.LogDebug($"[EXIT] {_methodName} completed");
            }
        }
    }
}