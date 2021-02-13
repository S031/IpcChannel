using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IpcChannel
{
	public sealed class IpcPipeClient : IDisposable
	{
		private const string close_channel_method_name = "CloseChannel";
		private readonly AnonymousPipeClientStream _pipeRead;
		private readonly AnonymousPipeClientStream _pipeWrite;
		private readonly Func<string, CancellationToken, Task<string>> _messageProcessor;
		private readonly CancellationToken _cancellationToken;
		private readonly EventWaitHandle _ewh;

		public IpcPipeClient(string pipeServerReadHandle, string pipeServerWriteHandle, 
			Func<string, CancellationToken, Task<string>> messageProcessor, 
			CancellationToken token = default)
		{
			_pipeRead = new AnonymousPipeClientStream(PipeDirection.In, pipeServerWriteHandle);
			_pipeWrite = new AnonymousPipeClientStream(PipeDirection.Out, pipeServerReadHandle);
			_messageProcessor = messageProcessor;
			_cancellationToken = token == default ? new CancellationTokenSource().Token : token;
#pragma warning disable CA1416 // Validate platform compatibility
			_ewh = EventWaitHandle.OpenExisting($"{pipeServerReadHandle}-{pipeServerWriteHandle}");
#pragma warning restore CA1416 // Validate platform compatibility
		}

		public async Task ListenAsync()
		{
			while (!_cancellationToken.IsCancellationRequested
				&& _ewh.WaitOne()
				&& await DoListen()) { }
		}

		private async Task<bool> DoListen()
		{
				byte[] buffer;
				int streamSize = 0;
				bool isCancel = false;
				try
				{
					buffer = await GetByteArrayFromStreamAsync(_pipeRead, sizeof(int));
					streamSize = BitConverter.ToInt32(buffer, 0);
					if (streamSize == 0)
						return false;
				}
				catch (Exception e)
				{
					Logger.LogError(e);
					return false;
				}

				buffer = await GetByteArrayFromStreamAsync(_pipeRead, streamSize);
				string request = Encoding.UTF8.GetString(buffer);
				Logger.LogDebug(request);
				isCancel = request == close_channel_method_name;
				var response = isCancel
					? "OK"
					: await _messageProcessor(request, _cancellationToken);
				Logger.LogDebug(response);
				buffer = Encoding.UTF8.GetBytes(response);
#if NETCOREAPP
				await _pipeWrite.WriteAsync(BitConverter.GetBytes(buffer.Length).AsMemory(0, sizeof(int)), _cancellationToken);
				await _pipeWrite.WriteAsync(buffer.AsMemory(0, buffer.Length), _cancellationToken);
#else
				await _pipeWrite.WriteAsync(BitConverter.GetBytes(buffer.Length), 0, sizeof(int), _cancellationToken);
				await _pipeWrite.WriteAsync(buffer, 0, buffer.Length, _cancellationToken);
#endif
			return !isCancel;
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

		public void Dispose()
		{
			Logger.LogInfo($"Client {_pipeRead?.SafePipeHandle.DangerousGetHandle().ToInt32()}-{_pipeWrite?.SafePipeHandle.DangerousGetHandle().ToInt32()} disposing");
			_pipeRead?.Dispose();
			_pipeWrite.Dispose();
			Logger.Flush();
		}
	}
}
