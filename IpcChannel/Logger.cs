using System;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Reflection;

namespace IpcChannel
{
	//
	// Summary:
	//     Defines logging severity levels.
	public enum LogLevel
	{
		Trace = 0,
		Debug = 1,
		Information = 2,
		Warning = 3,
		Error = 4,
		Critical = 5,
		None = 6
	}

	public class LoggerOptions
	{
		public LogLevel LoggingLevel { get; set; } = LogLevel.Information;
		public bool AutoFlush { get; set; } = true;
		public string LogName { get; set; } = string.Empty;
	}

	public static class Logger
	{
		private static readonly object obj4lock = new object();
		private static readonly StringBuilder _logger = new StringBuilder();

		private static readonly string _logPath =
				Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "log");

		private static string GetLogFileName()
			=> $@"{_logPath}\{LoggerOptions.LogName}{DateTime.Now.ToString("yyyy-MM")}.log";

		public static LoggerOptions LoggerOptions { get; } = new LoggerOptions() { LoggingLevel = LogLevel.Information };

		public static void LogDebug(string message, bool OutputToConsole = false)
			=> WriteLog(LogLevel.Debug, message, OutputToConsole);
		
		public static void LogInfo(string message, bool OutputToConsole = false)
			=> WriteLog(LogLevel.Information, message, OutputToConsole);

		public static void LogTrace(string message, bool OutputToConsole = false)
			=> WriteLog(LogLevel.Trace, message, OutputToConsole);
		
		public static void LogError(string message, bool OutputToConsole = false)
			=> WriteLog(LogLevel.Error, message, OutputToConsole);

		public static void LogError(Exception exception, bool OutputToConsole = false)
		{
			WriteLog(LogLevel.Error, $"{exception.Message}\n{exception.Source}\n{exception.StackTrace}", OutputToConsole);
			if (LoggerOptions.AutoFlush && LoggerOptions.LoggingLevel == LogLevel.Debug)
				Flush();
		}

		public static void WriteLog(string message, bool OutputToConsole = false)
			=> WriteLog(LogLevel.Information, message, OutputToConsole);

		public static void WriteLog(LogLevel logLevel, string message, bool OutputToConsole = false)
		{
			if (logLevel < LoggerOptions.LoggingLevel)
				return;

			if (OutputToConsole)
				Console.WriteLine(message);

			lock (obj4lock)
				_logger.AppendLine($"{DateTime.Now:yyyy-MM-dd hh:mm:ss}\t{logLevel.ToString().ToUpper()}:\t{message}");
		}


		public static void Flush()
		{
			if (!Directory.Exists(_logPath))
				Directory.CreateDirectory(_logPath);

			lock (obj4lock)
			{
				File.AppendAllText(GetLogFileName(), _logger.ToString());
				_logger.Clear();
			}
		}

		public static void Send(string subject)
		{
			throw new NotImplementedException();
		}
	}
}
