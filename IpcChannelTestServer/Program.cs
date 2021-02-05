using IpcChannel;
using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace IpcChannelTestServer
{
	class Program
	{
		private const bool start_client_in_thread = false;

		static async Task Main(string[] args)
		{
			CancellationToken cancellationToken = new CancellationTokenSource().Token;
#if DEBUG
			Logger.LoggerOptions.LoggingLevel = LogLevel.Debug;
#else
			Logger.LoggerOptions.LoggingLevel = LogLevel.Information;
#endif

			if (args.Length == 0)
			{
				Logger.LogInfo("Started application (Process A)...", true);
				using (IpcPipeServer s = start_client_in_thread
					? StartServerWithThreadClient(cancellationToken)
					: StartServerWithProcessClient(cancellationToken))
				{
					int loopCount = 100000;
					Logger.LogInfo($"Start loop for {loopCount} iterations", true);
					DateTime d = DateTime.Now;
					for (int i = 0; i < loopCount; i++)
					{
						Logger.LogDebug($"TestMethod{i}");
						var r = await s.CallMethod($"TestMethod{i}");
						Logger.LogDebug($"Result{i}:\t{r}");
					}
					Logger.LogInfo($"End loop for {loopCount} iterations with {(DateTime.Now - d).TotalMilliseconds} ms", true);
					Logger.Flush();
					Console.ReadLine();
				}
			}
			else
			{
				Logger.LogInfo("Client start..");
				await StartClient(args[0], args[1], cancellationToken);
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
			using (IpcPipeClient c = new IpcPipeClient(pipeServerReadHandle, pipeServerWriteHandle,
				(s, t) => ProcessMessage(s), cancellationToken))
			{
				await c.ListenAsync();
			}
		}

		private static async Task<string> ProcessMessage(string request)
			=> await Task.Run(() => $"{request} processed");
	}
}
