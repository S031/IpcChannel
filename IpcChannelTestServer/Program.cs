﻿using IpcChannel;
using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IpcChannelTestServer
{
	class Program
	{
		//test parameters
		//Using test on new thread or process
		private const bool start_client_in_thread = false;
		//performance test loop count
		private const int loop_count = 100_000;


		static async Task Main(string[] args)
		{
			using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
			{
				CancellationToken cancellationToken = cancellationTokenSource.Token;
#if DEBUG
			Logger.LoggerOptions.LoggingLevel = LogLevel.Debug;
#else
				Logger.LoggerOptions.LoggingLevel = LogLevel.Information;
#endif

				if (args.Length == 0)
				{
					//Log for this will be one
					Logger.LoggerOptions.LogName = "IpcPipeServer-";
					Logger.LogInfo("Started application (Process A)...", true);
					using (IpcPipeServer s = start_client_in_thread
						? StartServerWithThreadClient(cancellationToken)
						: StartServerWithProcessClient(cancellationToken))
					{
						Logger.LogInfo($"Start loop for {loop_count} iterations", true);
						DateTime d = DateTime.Now;
						for (int i = 0; i < loop_count; i++)
						{
							var r = await s.CallMethod($"TestMethod{i}").ConfigureAwait(false);
						}
						Logger.LogInfo($"End loop for {loop_count} iterations with {(DateTime.Now - d).TotalMilliseconds} ms", true);
						await s.CloseChannel().ConfigureAwait(false);
						Console.ReadLine();
					}
				}
				else
				{
					//Client runing in process mode
					Logger.LoggerOptions.LogName = "IpcPipeClient-";
					await StartClient(args[0], args[1], cancellationToken);
				}
				Logger.Flush();
			}
		}

		private static IpcPipeServer StartServerWithProcessClient(CancellationToken cancellationToken)
		{
			return new IpcPipeServer(new IpcPipeServerOptions()
			{
				CancellationToken = cancellationToken,
				ProcessStartPath = Assembly.GetEntryAssembly().Location.Replace(".dll", ".exe") //"IpcChannelTestServer.exe"
			});
		}

		private static IpcPipeServer StartServerWithThreadClient(CancellationToken cancellationToken)
		{
			IpcPipeServer s = new IpcPipeServer(new IpcPipeServerOptions()
			{
				CancellationToken = cancellationToken
			});
			Task.Run(async () => await StartClient(s.PipeReadHandle, s.PipeWriteHandle), cancellationToken);
			return s;
		}

		private static async Task StartClient(string pipeServerReadHandle, string pipeServerWriteHandle, CancellationToken cancellationToken = default)
		{
			Logger.LogInfo("Client start..");
			using (IpcPipeClient c = new IpcPipeClient(pipeServerReadHandle, pipeServerWriteHandle,
				(request, cancellationtoken) =>
					ProcessMessage(request, cancellationtoken), cancellationToken))
			{
				await c.ListenAsync();
			}
		}

		private static async Task<byte[]> ProcessMessage(byte[] request, CancellationToken cancellationToken)
		{
			var requestString = Encoding.UTF8.GetString(request);
			var response = Encoding.UTF8.GetBytes($"{requestString} processed");
			return await Task.FromResult(response);
		}
	}
}
