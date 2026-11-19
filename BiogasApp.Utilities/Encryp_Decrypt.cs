using System.Text;

namespace BiogasApp.Utilities;

/// <summary>
/// Same scheme as the Cyclone app: UTF-8 bytes -> Base64.
/// NOTE: this is reversible encoding, not hashing.
/// </summary>
public class Encryp_Decrypt
{
    public static string Encryptdata(string password)
    {
        if (string.IsNullOrEmpty(password))
            return string.Empty;

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
    }

    public static string Decryptdata(string encryptpwd)
    {
        if (string.IsNullOrEmpty(encryptpwd))
            return string.Empty;

        return Encoding.UTF8.GetString(Convert.FromBase64String(encryptpwd));
    }
}