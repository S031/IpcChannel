﻿using System;
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
	///		optimize performance (exclude arrays copieng drom streams read/write
	///     CancelationToken use
	///     exception hanglig (IpcChannelException)
	///     client file not support pipe exceptions
	///     reload/unload channel support
	///     configuration channels
	/// </summary>
	public sealed class IpcPipeServer : IDisposable
	{
		private readonly AnonymousPipeServerStream _pipeRead = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
		private readonly AnonymousPipeServerStream _pipeWrite = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
		private readonly Process _workProcess;
		private readonly CancellationToken _cancellationToken;

		public IpcPipeServer(IpcPipeServerOptions options)
		{
			PipeReadHandle = _pipeRead.GetClientHandleAsString();
			PipeWriteHandle = _pipeWrite.GetClientHandleAsString();
			_cancellationToken = options.CancellationToken == default 
				? new CancellationTokenSource().Token 
				: options.CancellationToken;

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

		public void Dispose()
		{
			Logger.LogDebug($"Server {PipeReadHandle}-{PipeWriteHandle}  disposing");
			_workProcess?.WaitForExit();
			_workProcess?.Close();
			_pipeRead?.Dispose();
			_pipeWrite.Dispose();
		}
	}

	public class IpcPipeServerOptions
	{
		public string ProcessStartPath { get; set; }
		public CancellationToken CancellationToken { get; set; }
	}
}