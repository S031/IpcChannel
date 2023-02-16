using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IpcChannel
{
	/// <summary>
	/// ToDo:
	///		multithred use test
	///     exception hanglig (IpcChannelException)
	///     client file not support pipe exceptions
	///     reload/unload channel support
	///     configuration channels
	/// </summary>
	public sealed class IpcPipeServer : IDisposable
	{
		private const string close_channel_method_name = "CloseChannel";
		private readonly AnonymousPipeServerStream _pipeRead = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
		private readonly AnonymousPipeServerStream _pipeWrite = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
		private readonly Process _workProcess;
		private readonly CancellationToken _cancellationToken;
		private readonly EventWaitHandle _ewh;

		public IpcPipeServer(IpcPipeServerOptions options)
		{
			PipeReadHandle = _pipeRead.GetClientHandleAsString();
			PipeWriteHandle = _pipeWrite.GetClientHandleAsString();
			_cancellationToken = options.CancellationToken == default 
				? new CancellationTokenSource().Token 
				: options.CancellationToken;

			_ewh = new EventWaitHandle(true, EventResetMode.AutoReset, $"{PipeReadHandle}-{PipeWriteHandle}");
			if (!string.IsNullOrEmpty(options.ProcessStartPath) && File.Exists(options.ProcessStartPath))
			{
				_workProcess = new Process
				{
					StartInfo =
					{
						FileName = options.ProcessStartPath,
						CreateNoWindow = true,
						UseShellExecute = false,
						Arguments = PipeReadHandle + " " + PipeWriteHandle
					}
				};
				_workProcess.Start();
			}
			//Call when restart process
			//_pipeRead?.DisposeLocalCopyOfClientHandle();
			//_pipeWrite?.DisposeLocalCopyOfClientHandle();
		}
		public string PipeReadHandle { get; private set; }

		public string PipeWriteHandle { get; private set; }

		public async Task<string> CallMethod(string callMethodInfo)
		{
			_ewh.Set();
			var buffer = Encoding.UTF8.GetBytes(callMethodInfo);
#if NETCOREAPP
			await _pipeWrite.WriteAsync(BitConverter.GetBytes(buffer.Length).AsMemory(0, sizeof(int)), _cancellationToken);
			await _pipeWrite.WriteAsync(buffer.AsMemory(0, buffer.Length), _cancellationToken);
#else
			await _pipeWrite.WriteAsync(BitConverter.GetBytes(buffer.Length), 0, sizeof(int), _cancellationToken);
			await _pipeWrite.WriteAsync(buffer, 0, buffer.Length, _cancellationToken);
#endif

			buffer = await GetByteArrayFromStreamAsync(_pipeRead, sizeof(int));
			int streamSize = BitConverter.ToInt32(buffer, 0);
			if (streamSize == 0)
				return null;

			buffer = await GetByteArrayFromStreamAsync(_pipeRead, streamSize);
			return Encoding.UTF8.GetString(buffer);
		}

		private async Task<byte[]> GetByteArrayFromStreamAsync(PipeStream stream, int length)
		{
			var result = new byte[length];
#if NETCOREAPP
			await stream.ReadAsync(result.AsMemory(0, length), _cancellationToken);
#else
			await stream.ReadAsync(result, 0, length, _cancellationToken);
#endif
			return result;
		}

		public async Task CloseChannel()
		{
			_ewh.Set();
			/// send neghative buffer length for close cyfnnel
			/// ToDo:
			/// Response && error handling
#if NETCOREAPP
			await _pipeWrite.WriteAsync(BitConverter.GetBytes(-1).AsMemory(0, sizeof(int)), _cancellationToken);
#else
			await _pipeWrite.WriteAsync(BitConverter.GetBytes(-1), 0, sizeof(int), _cancellationToken);
#endif
		}


		public void Dispose()
		{
			Logger.LogInfo($"Server {PipeReadHandle}-{PipeWriteHandle}  disposing");
			_workProcess?.Close();
			_ewh.Dispose();
			_pipeRead?.Dispose();
			_pipeWrite.Dispose();
			Logger.Flush();
		}
	}

	public class IpcPipeServerOptions
	{
		public string ProcessStartPath { get; set; }
		public CancellationToken CancellationToken { get; set; }
	}
}
