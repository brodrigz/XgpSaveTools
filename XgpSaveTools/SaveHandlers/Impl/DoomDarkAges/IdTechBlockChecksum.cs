using System.Buffers.Binary;
using System.Security.Cryptography;

namespace XgpSaveTools.SaveHandlers.Impl.DoomDarkAges;

public static class IdTechBlockChecksum
{
	public static uint Compute(ReadOnlySpan<byte> data)
	{
		var digest = MD5.HashData(data);
		return BinaryPrimitives.ReadUInt32LittleEndian(digest.AsSpan(0, 4))
			^ BinaryPrimitives.ReadUInt32LittleEndian(digest.AsSpan(4, 4))
			^ BinaryPrimitives.ReadUInt32LittleEndian(digest.AsSpan(8, 4))
			^ BinaryPrimitives.ReadUInt32LittleEndian(digest.AsSpan(12, 4));
	}

	public static byte[] CreateSidecar(ReadOnlySpan<byte> data)
	{
		var output = new byte[sizeof(ulong)];
		BinaryPrimitives.WriteUInt64LittleEndian(output, Compute(data));
		return output;
	}
}
