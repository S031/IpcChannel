using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IpcChannel
{
	internal static class Util
	{
		public static unsafe byte[] GetBytes(this string value)
		{
			int size = value.Length * sizeof(char);
			byte[] _buffer = new byte[size];
			if (size > 0)
			{
				fixed (char* source = value)
				fixed (byte* b = &_buffer[0])
				{
					Buffer.MemoryCopy(source, b, size, size);
				}
			}
			return _buffer;
		}
		public static unsafe string GetString(this byte[] buffer)
		{
			int size = buffer.Length;
			if (size > 0)
			{
				fixed (char* dest = new char[size])
				fixed (byte* b = &buffer[0])
				{
					Buffer.MemoryCopy(b, dest, size, size);
					return new string(dest);
				};
			}
			return string.Empty;

		}
	}
}
