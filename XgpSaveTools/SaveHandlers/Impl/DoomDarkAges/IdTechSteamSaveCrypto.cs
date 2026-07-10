using System.Security.Cryptography;
using System.Text;

namespace XgpSaveTools.SaveHandlers.Impl.DoomDarkAges;

public static class IdTechSteamSaveCrypto
{
	public const int NonceLength = 12;
	public const int TagLength = 16;
	public const int EncryptionOverhead = NonceLength + TagLength;
	public const string DoomGameCode = "MANCUBUS";

	public static bool IsValidSteamId64(string value)
	{
		return !string.IsNullOrEmpty(value)
			&& value.Length == 17
			&& value.All(c => c >= '0' && c <= '9');
	}

	public static string? ValidateSteamId64(string value)
	{
		return IsValidSteamId64(value)
			? null
			: "SteamID64 must be a 17-digit numeric identifier.";
	}

	public static byte[] Encrypt(
		ReadOnlySpan<byte> plaintext,
		string fileName,
		string steamId64,
		string gameCode = DoomGameCode)
	{
		if (!IsValidSteamId64(steamId64)) throw new ArgumentException(ValidateSteamId64(steamId64), nameof(steamId64));
		var nonce = new byte[NonceLength];
		RandomNumberGenerator.Fill(nonce);
		return Encrypt(plaintext, fileName, steamId64, gameCode, nonce);
	}

	internal static byte[] Encrypt(
		ReadOnlySpan<byte> plaintext,
		string fileName,
		string steamId64,
		string gameCode,
		ReadOnlySpan<byte> nonce)
	{
		if (nonce.Length != NonceLength) throw new ArgumentException($"Nonce must be {NonceLength} bytes.", nameof(nonce));
		var material = CreateMaterial(fileName, steamId64, gameCode);
		var key = DeriveKey(material);
		var ciphertext = new byte[plaintext.Length];
		var tag = new byte[TagLength];

#pragma warning disable SYSLIB0053
		using (var aes = new AesGcm(key))
#pragma warning restore SYSLIB0053
		{
			aes.Encrypt(nonce, plaintext, ciphertext, tag, material);
		}

		var output = new byte[plaintext.Length + EncryptionOverhead];
		nonce.CopyTo(output);
		ciphertext.CopyTo(output.AsSpan(NonceLength));
		tag.CopyTo(output.AsSpan(NonceLength + ciphertext.Length));
		return output;
	}

	public static byte[] Decrypt(
		ReadOnlySpan<byte> encrypted,
		string fileName,
		string steamId64,
		string gameCode = DoomGameCode)
	{
		if (!IsValidSteamId64(steamId64)) throw new ArgumentException(ValidateSteamId64(steamId64), nameof(steamId64));
		if (encrypted.Length < EncryptionOverhead)
			throw new InvalidDataException("Encrypted save file is too short.");

		var material = CreateMaterial(fileName, steamId64, gameCode);
		var key = DeriveKey(material);
		var nonce = encrypted[..NonceLength];
		var ciphertext = encrypted[NonceLength..^TagLength];
		var tag = encrypted[^TagLength..];
		var plaintext = new byte[ciphertext.Length];

		try
		{
#pragma warning disable SYSLIB0053
			using var aes = new AesGcm(key);
#pragma warning restore SYSLIB0053
			aes.Decrypt(nonce, ciphertext, tag, plaintext, material);
		}
		catch (CryptographicException ex)
		{
			throw new CryptographicException(
				$"Could not decrypt '{fileName}'. Verify the source SteamID64 and file integrity.", ex);
		}

		return plaintext;
	}

	private static byte[] CreateMaterial(string fileName, string steamId64, string gameCode)
	{
		if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("A leaf filename is required.", nameof(fileName));
		if (Path.GetFileName(fileName) != fileName) throw new ArgumentException("Only the leaf filename may be used for key derivation.", nameof(fileName));
		return Encoding.ASCII.GetBytes(steamId64 + gameCode + fileName);
	}

	private static byte[] DeriveKey(ReadOnlySpan<byte> material)
	{
		var digest = SHA256.HashData(material);
		return digest[..16];
	}
}
