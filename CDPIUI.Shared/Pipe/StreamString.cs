using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Shared.Pipe
{
    public class StreamString(Stream ioStream)
    {
        private readonly Stream ioStream = 
            ioStream ?? throw new ArgumentNullException(nameof(ioStream));
        private readonly Encoding streamEncoding = Encoding.Unicode;

        public async Task<string> ReadStringAsync(CancellationToken token = default)
        {
            byte[] lenBuffer = new byte[2];
            await ioStream.ReadAsync(lenBuffer, 0, 2, token).ConfigureAwait(false);

            int len = lenBuffer[0] * 256 + lenBuffer[1];

            byte[] inBuffer = new byte[len];
            await ioStream.ReadAsync(inBuffer, 0, len, token).ConfigureAwait(false);

            return streamEncoding.GetString(inBuffer);
        }

        public async Task<int> WriteStringAsync(string outString, CancellationToken token = default)
        {
            byte[] outBuffer = streamEncoding.GetBytes(outString ?? string.Empty);
            int len = Math.Min(outBuffer.Length, ushort.MaxValue);

            byte[] header = new byte[2] { (byte)(len / 256), (byte)(len & 255) };

            await ioStream.WriteAsync(header, 0, 2, token).ConfigureAwait(false);
            await ioStream.WriteAsync(outBuffer, 0, len, token).ConfigureAwait(false);
            await ioStream.FlushAsync(token).ConfigureAwait(false);

            return len + 2;
        }
    }
}
