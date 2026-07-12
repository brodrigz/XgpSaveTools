using System.Text;

namespace XgpSaveTools.Extensions
{
    public static class BinaryReaderExtensions
    {
        /// <summary>
        /// Represents the starting point of Windows FILETIME timestamps.
        /// </summary>
        public static readonly DateTime FileTimeEpoch =
        new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Reads a UTF‑16LE encoded string of a given length (in characters) from the stream.
        /// If <paramref name="length"/> is null, reads a 32‑bit little‑endian integer
        /// first to determine the character count.
        /// <para>Trims any trailing null characters ('\0') before returning.</para>
        /// </summary>
        /// <param name="reader">The binary reader to read from.</param>
        /// <param name="length">
        /// Optional character count.  If null, the next 4 bytes are read as an <c>int</c>
        /// and used as the count of UTF‑16 characters to follow.
        /// </param>
        /// <returns>The decoded string, without trailing nulls.</returns>
        public static string ReadUtf16(this BinaryReader reader, int? length = null)
        {
            int count = length ?? reader.ReadInt32();
            var bytes = reader.ReadBytes(count * 2);
            return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        }

        public static void WriteUtf16(this BinaryWriter bw, string s)
        {
            // write 32‑bit length, then exactly that many UTF‑16 code‐units
            bw.Write(s.Length);
            bw.Write(Encoding.Unicode.GetBytes(s));
        }

        /// <summary>
        /// Reads an 8‑byte Windows FILETIME (number of 100‑nanosecond ticks since
        /// January 1 1601 UTC) and converts it to a <see cref="DateTime"/> in UTC.
        /// </summary>
        /// <param name="reader">The binary reader to read from.</param>
        /// <returns>A UTC <see cref="DateTime"/> corresponding to the FILETIME value.</returns>
        public static DateTime ReadFiletime(this BinaryReader reader)
        {
            ulong filetime = reader.ReadUInt64();
            double seconds = filetime / 10_000_000.0;
            return FileTimeEpoch.AddSeconds(seconds);
        }
    }
}