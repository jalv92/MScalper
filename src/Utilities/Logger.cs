using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MScalper.Utilities
{
    /// <summary>
    /// Centralized logging system for the trading application
    /// </summary>
    public static class Logger
    {
        #region Enums and Structures
        /// <summary>
        /// Log message severity levels
        /// </summary>
        public enum LogLevel
        {
            Debug = 0,
            Info = 1,
            Warning = 2,
            Error = 3,
            Critical = 4
        }
        
        /// <summary>
        /// Log message structure
        /// </summary>
        private class LogMessage
        {
            public DateTime Timestamp { get; set; }
            public LogLevel Level { get; set; }
            public string Message { get; set; }
            public string Source { get; set; }
            public string ThreadId { get; set; }
        }
        
        /// <summary>
        /// Types of log outputs
        /// </summary>
        [Flags]
        public enum LogOutput
        {
            None = 0,
            Console = 1,
            File = 2,
            Memory = 4,
            CustomHandler = 8,
            All = Console | File | Memory | CustomHandler
        }
        #endregion

        #region Private Static Fields
        private static readonly object _lockObject = new object();
        private static bool _initialized = false;
        private static string _logDirectory;
        private static string _currentLogFile;
        private static LogLevel _minimumLogLevel = LogLevel.Info;
        private static LogOutput _logOutputs = LogOutput.Console | LogOutput.File;
        private static readonly ConcurrentQueue<LogMessage> _pendingMessages = new ConcurrentQueue<LogMessage>();
        private static readonly List<LogMessage> _memoryLog = new List<LogMessage>();
        private static readonly int _maxMemoryLogSize = 1000;
        private static readonly Timer _processingTimer;
        private static bool _isProcessing = false;
        private static readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(1);
        private static Func<LogMessage, string> _customFormatter;
        private static Action<string, LogLevel> _customLogHandler;
        private static readonly Dictionary<LogLevel, ConsoleColor> _consoleColors = new Dictionary<LogLevel, ConsoleColor>
        {
            { LogLevel.Debug, ConsoleColor.Gray },
            { LogLevel.Info, ConsoleColor.White },
            { LogLevel.Warning, ConsoleColor.Yellow },
            { LogLevel.Error, ConsoleColor.Red },
            { LogLevel.Critical, ConsoleColor.DarkRed }
        };
        #endregion

        #region Constructor
        /// <summary>
        /// Static constructor for Logger
        /// </summary>
        static Logger()
        {
            // Start background processing timer
            _processingTimer = new Timer(ProcessPendingMessages, null, 
                _processingInterval, _processingInterval);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Initializes the logging system
        /// </summary>
        /// <param name="logDirectory">Directory for log files</param>
        /// <param name="minimumLevel">Minimum level of messages to log</param>
        /// <param name="outputs">Types of outputs to use</param>
        /// <returns>True if initialization was successful</returns>
        public static bool Initialize(string logDirectory, LogLevel minimumLevel = LogLevel.Info,
            LogOutput outputs = LogOutput.Console | LogOutput.File)
        {
            try
            {
                lock (_lockObject)
                {
                    // Ensure directory exists
                    if (!Directory.Exists(logDirectory) && (outputs & LogOutput.File) != 0)
                    {
                        Directory.CreateDirectory(logDirectory);
                    }
                    
                    _logDirectory = logDirectory;
                    _minimumLogLevel = minimumLevel;
                    _logOutputs = outputs;
                    
                    // Create current log file name
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    _currentLogFile = Path.Combine(_logDirectory, $"OrderFlowScalper_{timestamp}.log");
                    
                    _initialized = true;
                    
                    // Log initialization
                    Log("Logging system initialized", LogLevel.Info, "Logger");
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Can't use normal logging here since we're initializing
                Console.WriteLine($"Error initializing logger: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Logs a message with specified level
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="level">Log level</param>
        /// <param name="source">Source of the message (component name)</param>
        public static void Log(string message, LogLevel level = LogLevel.Info, string source = null)
        {
            if (string.IsNullOrEmpty(message))
                return;
                
            // Skip messages below minimum level
            if (level < _minimumLogLevel)
                return;
                
            // Create log message
            var logMessage = new LogMessage
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Source = source ?? GetCallerComponent(),
                ThreadId = Thread.CurrentThread.ManagedThreadId.ToString()
            };
            
            // Add to queue for background processing
            _pendingMessages.Enqueue(logMessage);
            
            // If this is a critical message, process immediately
            if (level >= LogLevel.Error)
            {
                ProcessPendingMessages(null);
            }
        }
        
        /// <summary>
        /// Logs a debug message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="source">Source of the message</param>
        public static void Debug(string message, string source = null)
        {
            Log(message, LogLevel.Debug, source);
        }
        
        /// <summary>
        /// Logs an info message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="source">Source of the message</param>
        public static void Info(string message, string source = null)
        {
            Log(message, LogLevel.Info, source);
        }
        
        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="source">Source of the message</param>
        public static void Warning(string message, string source = null)
        {
            Log(message, LogLevel.Warning, source);
        }
        
        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="source">Source of the message</param>
        public static void Error(string message, string source = null)
        {
            Log(message, LogLevel.Error, source);
        }
        
        /// <summary>
        /// Logs a critical message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="source">Source of the message</param>
        public static void Critical(string message, string source = null)
        {
            Log(message, LogLevel.Critical, source);
        }
        
        /// <summary>
        /// Logs an exception
        /// </summary>
        /// <param name="ex">Exception to log</param>
        /// <param name="level">Log level</param>
        /// <param name="additionalInfo">Additional information</param>
        /// <param name="source">Source of the message</param>
        public static void LogException(Exception ex, LogLevel level = LogLevel.Error, 
            string additionalInfo = null, string source = null)
        {
            if (ex == null)
                return;
                
            StringBuilder sb = new StringBuilder();
            
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                sb.AppendLine(additionalInfo);
            }
            
            sb.AppendLine($"Exception: {ex.GetType().Name}");
            sb.AppendLine($"Message: {ex.Message}");
            
            if (ex.StackTrace != null)
            {
                sb.AppendLine("StackTrace:");
                sb.AppendLine(ex.StackTrace);
            }
            
            // Handle inner exceptions
            if (ex.InnerException != null)
            {
                sb.AppendLine("Inner Exception:");
                StringBuilder innerSb = new StringBuilder();
                Exception innerEx = ex.InnerException;
                
                while (innerEx != null)
                {
                    innerSb.AppendLine($"  {innerEx.GetType().Name}: {innerEx.Message}");
                    innerEx = innerEx.InnerException;
                }
                
                sb.Append(innerSb.ToString());
            }
            
            Log(sb.ToString(), level, source);
        }
        
        /// <summary>
        /// Sets a custom formatter for log messages
        /// </summary>
        /// <param name="formatter">Formatter function</param>
        public static void SetCustomFormatter(Func<LogMessage, string> formatter)
        {
            _customFormatter = formatter;
        }
        
        /// <summary>
        /// Sets a custom log handler
        /// </summary>
        /// <param name="handler">Handler action</param>
        public static void SetCustomLogHandler(Action<string, LogLevel> handler)
        {
            _customLogHandler = handler;
            _logOutputs |= LogOutput.CustomHandler;
        }
        
        /// <summary>
        /// Gets recent log entries from memory
        /// </summary>
        /// <param name="count">Number of entries to get</param>
        /// <param name="level">Minimum level to include</param>
        /// <returns>List of formatted log entries</returns>
        public static List<string> GetRecentLogs(int count = 100, LogLevel level = LogLevel.Debug)
        {
            lock (_lockObject)
            {
                return _memoryLog
                    .Where(m => m.Level >= level)
                    .OrderByDescending(m => m.Timestamp)
                    .Take(count)
                    .Select(FormatLogMessage)
                    .ToList();
            }
        }
        
        /// <summary>
        /// Flushes all pending log messages
        /// </summary>
        public static void Flush()
        {
            ProcessPendingMessages(null);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Processes pending log messages in the background
        /// </summary>
        private static void ProcessPendingMessages(object state)
        {
            if (_isProcessing)
                return;
                
            _isProcessing = true;
            
            try
            {
                // Process up to 100 messages at a time to avoid long locks
                int processCount = 0;
                while (_pendingMessages.TryDequeue(out LogMessage message) && processCount < 100)
                {
                    processCount++;
                    
                    // Format message
                    string formattedMessage = FormatLogMessage(message);
                    
                    // Output to console if enabled
                    if ((_logOutputs & LogOutput.Console) != 0)
                    {
                        WriteToConsole(message, formattedMessage);
                    }
                    
                    // Output to file if enabled
                    if ((_logOutputs & LogOutput.File) != 0 && _initialized)
                    {
                        WriteToFile(formattedMessage);
                    }
                    
                    // Store in memory if enabled
                    if ((_logOutputs & LogOutput.Memory) != 0)
                    {
                        lock (_lockObject)
                        {
                            _memoryLog.Add(message);
                            
                            // Trim memory log if needed
                            if (_memoryLog.Count > _maxMemoryLogSize)
                            {
                                _memoryLog.RemoveAt(0);
                            }
                        }
                    }
                    
                    // Call custom handler if enabled
                    if ((_logOutputs & LogOutput.CustomHandler) != 0 && _customLogHandler != null)
                    {
                        try
                        {
                            _customLogHandler(formattedMessage, message.Level);
                        }
                        catch (Exception ex)
                        {
                            // Write to console if custom handler fails
                            Console.WriteLine($"Error in custom log handler: {ex.Message}");
                        }
                    }
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }
        
        /// <summary>
        /// Formats a log message
        /// </summary>
        /// <param name="message">Message to format</param>
        /// <returns>Formatted message</returns>
        private static string FormatLogMessage(LogMessage message)
        {
            // Use custom formatter if available
            if (_customFormatter != null)
            {
                try
                {
                    return _customFormatter(message);
                }
                catch
                {
                    // Fall back to default formatter if custom one fails
                }
            }
            
            // Default format: [Time] [Level] [Source] [ThreadId] Message
            return $"[{message.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{message.Level}] [{message.Source}] [{message.ThreadId}] {message.Message}";
        }
        
        /// <summary>
        /// Writes a message to the console with appropriate color
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="formattedMessage">Formatted message text</param>
        private static void WriteToConsole(LogMessage message, string formattedMessage)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            
            try
            {
                // Set color based on log level
                if (_consoleColors.ContainsKey(message.Level))
                {
                    Console.ForegroundColor = _consoleColors[message.Level];
                }
                
                Console.WriteLine(formattedMessage);
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
        
        /// <summary>
        /// Writes a message to the log file
        /// </summary>
        /// <param name="message">Formatted message text</param>
        private static void WriteToFile(string message)
        {
            if (string.IsNullOrEmpty(_currentLogFile))
                return;
                
            try
            {
                lock (_lockObject)
                {
                    File.AppendAllText(_currentLogFile, message + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // Write to console if file write fails
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the calling component name
        /// </summary>
        /// <returns>Component name</returns>
        private static string GetCallerComponent()
        {
            try
            {
                // Get the calling stack frame
                var stackFrame = new System.Diagnostics.StackFrame(2, false);
                var method = stackFrame.GetMethod();
                
                if (method != null)
                {
                    string className = method.DeclaringType?.Name ?? "Unknown";
                    return className;
                }
            }
            catch
            {
                // Ignore errors in getting caller
            }
            
            return "Unknown";
        }
        #endregion
    }
}