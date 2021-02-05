﻿using System;
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
	}

	public static class Logger
	{
		private static readonly object obj4lock = new object();
		private static readonly StringBuilder _logger = new StringBuilder();

		private static readonly string _logPath =
				Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "log");

		private static readonly string _logFileName =
			string.Format(@"{0}\{1}.log",
				_logPath,
				DateTime.Now.ToString("yyyy-MM"));

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
				File.AppendAllText(_logFileName, _logger.ToString());
				_logger.Clear();
			}
		}

		public static void Send(string subject)
		{
			string sender = "for.matador@mail.ru";
			string password = "s031-s031";
			MailMessage mail = new MailMessage
			{
				From = new MailAddress(sender)
			};

			using (SmtpClient smtp = new SmtpClient())
			{
				smtp.Host = "smtp.mail.ru";
				smtp.Port = 587;
				smtp.Timeout = 10000;
				smtp.EnableSsl = true;
				smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
				smtp.UseDefaultCredentials = false;
				smtp.Credentials = new NetworkCredential(sender, password);

				//recipient address
				mail.To.Add(new MailAddress("svostrikov@metib.ru"));
				mail.To.Add(new MailAddress("nkirgizov@metib.ru"));
				mail.Subject = subject;
				mail.From = new MailAddress(sender);
				mail.IsBodyHtml = false;
				string st = _logger.ToString();

				mail.Body = st;
				smtp.Send(mail);
			}
		}
	}
}