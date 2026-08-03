using System.Security.Cryptography;
using System.Text;

namespace ARTR.Veyra.Core.Security;

public static class ApiKeyHasher
{
    public static string HashSha256Hex(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
#pragma warning disable CA1308 // Normalize your strings to uppercase - lowercase hex is required for API key hashes.
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
#pragma warning restore CA1308
    }

    public static bool FixedTimeEqualsHex(string leftHex, string rightHex)
    {
        ArgumentNullException.ThrowIfNull(leftHex);
        ArgumentNullException.ThrowIfNull(rightHex);

        if (leftHex.Length != rightHex.Length)
        {
            return false;
        }

        if (!IsValidSha256Hex(leftHex) || !IsValidSha256Hex(rightHex))
        {
            return false;
        }

        var leftBytes = Convert.FromHexString(leftHex);
        var rightBytes = Convert.FromHexString(rightHex);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static bool IsValidSha256Hex(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            var isDigit = character is >= '0' and <= '9';
            var isLowerHex = character is >= 'a' and <= 'f';
            var isUpperHex = character is >= 'A' and <= 'F';
            if (!isDigit && !isLowerHex && !isUpperHex)
            {
                return false;
            }
        }

        return true;
    }
}
