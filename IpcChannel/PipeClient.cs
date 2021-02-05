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
		private readonly AnonymousPipeClientStream _pipeRead;
		private readonly AnonymousPipeClientStream _pipeWrite;
		private readonly Func<string, CancellationToken, Task<string>> _messageProcessor;
		private readonly CancellationToken _cancellationToken;

		public IpcPipeClient(string pipeServerReadHandle, string pipeServerWriteHandle, 
			Func<string, CancellationToken, Task<string>> messageProcessor, 
			CancellationToken token = default)
		{
			_pipeRead = new AnonymousPipeClientStream(PipeDirection.In, pipeServerWriteHandle);
			_pipeWrite = new AnonymousPipeClientStream(PipeDirection.Out, pipeServerReadHandle);
			_messageProcessor = messageProcessor;
			_cancellationToken = token == default ? new CancellationTokenSource().Token : token;
		}

		public async Task ListenAsync()
		{
			while (!_cancellationToken.IsCancellationRequested)
			{
				byte[] buffer;
				int streamSize = 0;
				try
				{
					buffer = await GetByteArrayFromStreamAsync(_pipeRead, sizeof(int));
					streamSize = BitConverter.ToInt32(buffer, 0);
					if (streamSize == 0)
						break;
				}
				catch (Exception e)
				{
					Logger.LogError(e);
				}

				buffer = await GetByteArrayFromStreamAsync(_pipeRead, streamSize);
				string request = Encoding.UTF8.GetString(buffer);
				Logger.LogDebug(request);
				var response = await _messageProcessor(request, _cancellationToken);
				Logger.LogDebug(response);
				buffer = Encoding.UTF8.GetBytes(response);
#if NETCOREAPP
				await _pipeWrite.WriteAsync(BitConverter.GetBytes(buffer.Length).AsMemory(0, sizeof(int)), _cancellationToken);
				await _pipeWrite.WriteAsync(buffer.AsMemory(0, buffer.Length), _cancellationToken);
#else
				await _pipeWrite.WriteAsync(BitConverter.GetBytes(buffer.Length), 0, sizeof(int), _cancellationToken);
				await _pipeWrite.WriteAsync(buffer, 0, buffer.Length, _cancellationToken);
#endif
			}
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
			Logger.LogDebug($"Client {_pipeRead?.SafePipeHandle}-{_pipeWrite?.SafePipeHandle} disposing");
			_pipeRead?.Dispose();
			_pipeWrite.Dispose();
		}
	}
}
