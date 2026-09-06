using System.Text;

namespace web.Infrastructure
{
    /// <summary>
    /// Splits a large WAV file into smaller, still-valid WAV byte buffers on sample-frame
    /// boundaries. Used to get uncompressed lecture recordings under AiGateway's own request
    /// body size cap (currently configured there as ~250 MB - a long, uncompressed WAV track
    /// can still exceed that) without needing an external audio library.
    /// </summary>
    public static class WavSplitter
    {
        public readonly record struct WavFormatInfo(int BlockAlign, long DataOffset, long DataLength, byte[] FmtChunk);

        /// <summary>
        /// Reads just the RIFF/fmt/data header info needed to split the file - does not load
        /// the audio payload into memory. Returns false for anything that isn't a plain RIFF/WAVE
        /// file (compressed formats, or a WAV with no readable fmt/data chunk).
        /// </summary>
        public static bool TryReadHeader(string path, out WavFormatInfo info)
        {
            info = default;

            using var fs = File.OpenRead(path);
            if (fs.Length < 44) return false;

            using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true);
            if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "RIFF") return false;
            br.ReadUInt32(); // overall RIFF size - recomputed per chunk on write, not needed here
            if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "WAVE") return false;

            byte[]? fmtChunk = null;
            long dataOffset = -1;
            long dataLength = 0;

            while (fs.Position + 8 <= fs.Length)
            {
                var id = Encoding.ASCII.GetString(br.ReadBytes(4));
                var size = br.ReadUInt32();

                if (id == "fmt ")
                {
                    fmtChunk = br.ReadBytes((int)size);
                }
                else if (id == "data")
                {
                    dataOffset = fs.Position;
                    dataLength = size;
                    fs.Seek(size, SeekOrigin.Current);
                }
                else
                {
                    fs.Seek(size, SeekOrigin.Current);
                }

                if (fs.Position % 2 == 1 && fs.Position < fs.Length) fs.Seek(1, SeekOrigin.Current); // chunks are word-padded

                if (fmtChunk is not null && dataOffset >= 0) break;
            }

            if (fmtChunk is null || fmtChunk.Length < 16 || dataOffset < 0) return false;

            var blockAlign = BitConverter.ToInt16(fmtChunk, 12);
            if (blockAlign <= 0) return false;

            // Clamp to what's actually left in the file, in case the header's data size lied.
            dataLength = Math.Min(dataLength, fs.Length - dataOffset);

            info = new WavFormatInfo(blockAlign, dataOffset, dataLength, fmtChunk);
            return true;
        }

        /// <summary>
        /// Yields one or more complete, independently-decodable WAV byte buffers, each at most
        /// ~maxChunkBytes, split only on whole sample-frame (BlockAlign) boundaries.
        /// </summary>
        public static IEnumerable<byte[]> SplitToChunks(string path, WavFormatInfo info, long maxChunkBytes)
        {
            const int HeaderOverhead = 64; // RIFF + fmt + data chunk headers, with margin to spare
            var maxDataBytes = Math.Max(info.BlockAlign, maxChunkBytes - HeaderOverhead - info.FmtChunk.Length);
            maxDataBytes -= maxDataBytes % info.BlockAlign;

            using var fs = File.OpenRead(path);
            fs.Seek(info.DataOffset, SeekOrigin.Begin);

            var remaining = info.DataLength;
            while (remaining > 0)
            {
                var thisChunkSize = (int)Math.Min(maxDataBytes, remaining);
                var pcm = new byte[thisChunkSize];
                var read = fs.Read(pcm, 0, thisChunkSize);
                if (read <= 0) yield break;
                if (read < thisChunkSize) Array.Resize(ref pcm, read);

                yield return BuildWavFile(info.FmtChunk, pcm);
                remaining -= read;
            }
        }

        private static byte[] BuildWavFile(byte[] fmtChunk, byte[] pcmData)
        {
            using var ms = new MemoryStream(44 + pcmData.Length);
            using var bw = new BinaryWriter(ms);

            var riffSize = 4 + (8 + fmtChunk.Length) + (8 + pcmData.Length);

            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(riffSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(fmtChunk.Length);
            bw.Write(fmtChunk);

            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(pcmData.Length);
            bw.Write(pcmData);

            return ms.ToArray();
        }
    }
}
