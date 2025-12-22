using System;
using System.Text;
using System.Security.Cryptography;

public static class EncryptionHelper
{
    private static string key = "Padr0nWtd_Secret_Key";

    public static string Decrypt(string cipherText)
    {
        return cipherText;
        //if (string.IsNullOrEmpty(cipherText)) return "";
        //var base64EncodedBytes = Convert.FromBase64String(cipherText);
        //return Encoding.UTF8.GetString(base64EncodedBytes);
    }

    //public static string Encrypt(string plainText)
    //{
    //    var data = Encoding.UTF8.GetBytes(plainText);
    //    var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);
    //    return Convert.ToBase64String(encrypted);
    //}

    //public static string Decrypt(string cipherText)
    //{
    //    try
    //    {
    //        var data = Convert.FromBase64String(cipherText);
    //        var decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.LocalMachine);
    //        return Encoding.UTF8.GetString(decrypted);
    //    }
    //    catch { return "Error al desencriptar"; }
    //}

}