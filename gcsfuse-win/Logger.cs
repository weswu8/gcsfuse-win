using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace gcsfuse_win
{
    public static class LoggerLocker
    {
        public static Object Locker = new object();
    }
    public interface ILoggerHandler
    {
        void Publish(LogMessage logMessage);
    }
    public interface ILoggerFormatter
    {
        string ApplyFormat(LogMessage logMessage);
    }
    internal class DefaultLoggerFormatter : ILoggerFormatter
    {
        public string ApplyFormat(LogMessage logMessage)
        {
            return string.Format("{0:yyyy-MM-dd HH:mm:ss:FFF}: {1} [line: {2} {3} -> {4}()]: {5}",
                            logMessage.DateTime, logMessage.Level, logMessage.LineNumber, logMessage.CallingClass,
                            logMessage.CallingMethod, logMessage.Text);
        }
    }
    internal class ConsoleLoggerFormatter : ILoggerFormatter
    {
        public string ApplyFormat(LogMessage logMessage)
        {
            return string.Format("{0:yyyy-MM-dd HH:mm:ss:FFF}: {1} : {2}",
                            logMessage.DateTime, logMessage.Level, logMessage.Text);
        }
    }
    public interface ILoggerHandlerManager
    {
        ILoggerHandlerManager AddHandler(ILoggerHandler loggerHandler);
        ILoggerHandlerManager AddHandler(ILoggerHandler loggerHandler, Predicate<LogMessage> filter);

        bool RemoveHandler(ILoggerHandler loggerHandler);
    }
    internal class FilteredHandler : ILoggerHandler
    {
        public Predicate<LogMessage> Filter { get; set; }
        public ILoggerHandler Handler { get; set; }
        public void Publish(LogMessage logMessage)
        {
            if (Filter(logMessage))
                Handler.Publish(logMessage);
        }
    }

    internal class LogPublisher : ILoggerHandlerManager
    {
        private readonly IList<ILoggerHandler> _loggerHandlers;
        private readonly IList<LogMessage> _messages;
        public LogPublisher()
        {
            _loggerHandlers = new List<ILoggerHandler>();
            _messages = new List<LogMessage>();
            StoreLogMessages = false;
        }
        public LogPublisher(bool storeLogMessages)
        {
            _loggerHandlers = new List<ILoggerHandler>();
            _messages = new List<LogMessage>();
            StoreLogMessages = storeLogMessages;
        }
        public void Publish(LogMessage logMessage)
        {
            if (StoreLogMessages)
                _messages.Add(logMessage);
            foreach (var loggerHandler in _loggerHandlers)
                loggerHandler.Publish(logMessage);
        }
        public ILoggerHandlerManager AddHandler(ILoggerHandler loggerHandler)
        {
            if (loggerHandler != null)
                _loggerHandlers.Add(loggerHandler);
            return this;
        }
        public ILoggerHandlerManager AddHandler(ILoggerHandler loggerHandler, Predicate<LogMessage> filter)
        {
            if ((filter == null) || loggerHandler == null)
                return this;

            return AddHandler(new FilteredHandler()
            {
                Filter = filter,
                Handler = loggerHandler
            });
        }
        public bool RemoveHandler(ILoggerHandler loggerHandler)
        {
            return _loggerHandlers.Remove(loggerHandler);
        }
        public IEnumerable<LogMessage> Messages
        {
            get { return _messages; }
        }
        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="SimpleLogger.Logging.LogPublisher"/> store log messages.
        /// </summary>
        /// <value><c>true</c> if store log messages; otherwise, <c>false</c>. By default is <c>false</c></value>
        public bool StoreLogMessages { get; set; }
    }
    public class LogMessage
    {
        public DateTime DateTime { get; set; }
        public Logger.Level Level { get; set; }
        public string Text { get; set; }
        public string CallingClass { get; set; }
        public string CallingMethod { get; set; }
        public int LineNumber { get; set; }
        public LogMessage() { }

        public LogMessage(Logger.Level level, string text, DateTime dateTime, string callingClass, string callingMethod, int lineNumber)
        {
            Level = level;
            Text = text;
            DateTime = dateTime;
            CallingClass = callingClass;
            CallingMethod = callingMethod;
            LineNumber = lineNumber;
        }
        public override string ToString()
        {
            return new DefaultLoggerFormatter().ApplyFormat(this);
        }
    }

    public class ConsoleLoggerHandler : ILoggerHandler
    {
        private readonly ILoggerFormatter _loggerFormatter;
        public ConsoleLoggerHandler() : this(new ConsoleLoggerFormatter()) { }
        public ConsoleLoggerHandler(ILoggerFormatter loggerFormatter)
        {
            _loggerFormatter = loggerFormatter;
        }
        public void Publish(LogMessage logMessage)
        {
            if (logMessage.Level <= Logger.Level.Info)
            { Console.ForegroundColor = ConsoleColor.Green; }
            else { { Console.ForegroundColor = ConsoleColor.Red; } }
            Console.WriteLine(_loggerFormatter.ApplyFormat(logMessage));
            Console.ResetColor();
        }
    }
    public class FileLoggerHandler : ILoggerHandler
    {
        private string _fileName;
        private readonly string _directory;
        private readonly ILoggerFormatter _loggerFormatter;
        public FileLoggerHandler() : this(CreateFileName()) { }
        public FileLoggerHandler(string fileName) : this(fileName, Constants.LOG_PATH) { }
        public FileLoggerHandler(string fileName, string directory) : this(new DefaultLoggerFormatter(), fileName, directory) { }
        public FileLoggerHandler(ILoggerFormatter loggerFormatter) : this(loggerFormatter, CreateFileName()) { }
        public FileLoggerHandler(ILoggerFormatter loggerFormatter, string fileName) : this(loggerFormatter, fileName, string.Empty) { }
        public FileLoggerHandler(ILoggerFormatter loggerFormatter, string fileName, string directory)
        {
            _loggerFormatter = loggerFormatter;
            _fileName = fileName;
            _directory = directory;
        }
        public void Publish(LogMessage logMessage)
        {
            try
            {
                if (!string.IsNullOrEmpty(_directory))
                {
                    var directoryInfo = new DirectoryInfo(_directory);
                    if (!directoryInfo.Exists)
                        directoryInfo.Create();
                }
                // 防止多个进程同时打开文件
                lock (LoggerLocker.Locker)
                {
                    using (var writer = new StreamWriter(File.Open(System.IO.Path.Combine(_directory, _fileName), FileMode.Append)))
                        writer.WriteLine(_loggerFormatter.ApplyFormat(logMessage));
                }
            }
            catch
            { }
        }

        private static string CreateFileName()
        {
            var currentDate = DateTime.Now;
            var filePrefix = typeof(FileLoggerHandler).Namespace;
            //hack here to make the log file rotate daily
            return string.Format(filePrefix + "-{0:0000}-{1:00}-{2:00}.log",
                currentDate.Year, currentDate.Month, currentDate.Day);
        }
    }
    public abstract class LoggerModule
    {
        public abstract string Name { get; }
        public virtual void BeforeLog() { }
        public virtual void AfterLog(LogMessage logMessage) { }
        public virtual void Initialize() { }
    }
    public class ModuleManager
    {
        private readonly IDictionary<string, LoggerModule> _modules;

        public ModuleManager()
        {
            _modules = new Dictionary<string, LoggerModule>();
        }

        public void BeforeLog()
        {
            foreach (var loggerModule in _modules.Values)
                loggerModule.BeforeLog();
        }

        public void AfterLog(LogMessage logMessage)
        {
            foreach (var loggerModule in _modules.Values)
                loggerModule.AfterLog(logMessage);
        }

        public void Install(LoggerModule module)
        {
            if (!_modules.ContainsKey(module.Name))
            {
                module.Initialize();
                _modules.Add(module.Name, module);
            }
            else
            {
                // reinstall module
                Uninstall(module.Name);
                Install(module);
            }
        }

        public void Uninstall(LoggerModule module)
        {
            if (_modules.ContainsKey(module.Name))
                _modules.Remove(module.Name);
        }

        public void Uninstall(string moduleName)
        {
            if (_modules.ContainsKey(moduleName))
                _modules.Remove(moduleName);
        }
    }
    public static class Logger
    {
        private static readonly LogPublisher LogPublisher;
        private static readonly ModuleManager ModuleManager;

        private static readonly object Sync = new object();
        private static Level _defaultLevel = Level.Info;
        private static bool _isTurned = true;
        private static bool _isTurnedDebug = true;

        public enum Level
        {
            Debug,
            Info,
            Warning,
            Error,
            Fatal
        }

        static Logger()
        {
            lock (Sync)
            {
                LogPublisher = new LogPublisher();
                ModuleManager = new ModuleManager();
            }
        }

        public static void DefaultInitialization()
        {
            LoggerHandlerManager
                .AddHandler(new ConsoleLoggerHandler())
                .AddHandler(new FileLoggerHandler());

            Log(Level.Info, "Default initialization");
        }

        public static Level DefaultLevel
        {
            get { return _defaultLevel; }
            set { _defaultLevel = value; }
        }

        public static ILoggerHandlerManager LoggerHandlerManager
        {
            get { return LogPublisher; }
        }

        public static void Log()
        {
            Log("There is no message");
        }

        public static void Log(string message)
        {
            Log(_defaultLevel, message);
        }

        public static void Log(Level level, string message)
        {
            var stackFrame = FindStackFrame();
            var methodBase = GetCallingMethodBase(stackFrame);
            var callingMethod = methodBase.Name;
            var callingClass = methodBase.ReflectedType.Name;
            var lineNumber = stackFrame.GetFileLineNumber();

            Log(level, message, callingClass, callingMethod, lineNumber);
        }

        public static void Log(Exception exception)
        {
            var ExceptionMsg = getExecptionCompleteMessage(exception);
            Log(Level.Error, ExceptionMsg);
        }

        public static string getExecptionCompleteMessage(Exception exception)
        {
            Exception e = exception;
            StringBuilder s = new System.Text.StringBuilder();
            while (e != null)
            {
                s.AppendLine("Type: " + e.GetType().FullName);
                s.AppendLine("Message: " + e.Message);
                s.AppendLine("Stacktrace: ");
                s.Append(e.StackTrace);
                e = e.InnerException;
            }
            return s.ToString();
        }
        private static void Log(Level level, string message, string callingClass, string callingMethod, int lineNumber)
        {
            if (!_isTurned || (!_isTurnedDebug && level == Level.Debug))
                return;
            // filter by log level
            if ((int)level < (int)DefaultLevel)
            {
                return;
            }
            var currentDateTime = DateTime.Now;

            ModuleManager.BeforeLog();
            var logMessage = new LogMessage(level, message, currentDateTime, callingClass, callingMethod, lineNumber);
            LogPublisher.Publish(logMessage);
            ModuleManager.AfterLog(logMessage);
        }
        private static MethodBase GetCallingMethodBase(StackFrame stackFrame)
        {
            return stackFrame == null
                ? MethodBase.GetCurrentMethod() : stackFrame.GetMethod();
        }

        private static StackFrame FindStackFrame()
        {
            var stackTrace = new StackTrace();
            for (var i = 0; i < stackTrace.GetFrames().Count(); i++)
            {
                var methodBase = stackTrace.GetFrame(i).GetMethod();
                var name = MethodBase.GetCurrentMethod().Name;
                if (!methodBase.Name.Equals("Log") && !methodBase.Name.Equals(name))
                    return new StackFrame(i, true);
            }
            return null;
        }

        public static void On()
        {
            _isTurned = true;
        }

        public static void Off()
        {
            _isTurned = false;
        }

        public static void DebugOn()
        {
            _isTurnedDebug = true;
        }

        public static void DebugOff()
        {
            _isTurnedDebug = false;
        }

        public static IEnumerable<LogMessage> Messages
        {
            get { return LogPublisher.Messages; }
        }

        public static ModuleManager Modules
        {
            get { return ModuleManager; }
        }

        public static bool StoreLogMessages
        {
            get { return LogPublisher.StoreLogMessages; }
            set { LogPublisher.StoreLogMessages = value; }
        }

    }
}
