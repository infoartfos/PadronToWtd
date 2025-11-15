using System.Configuration;
using PadronWtd.UI.Constants;

namespace PadronWtd.UI.Configuration
{
    internal static class AppSettings
    {
        public static string ApiUrl =>
            ConfigurationManager.AppSettings[AppConstants.ConfigApiUrl] ?? "";

        public static string DbConnection =>
            ConfigurationManager.ConnectionStrings[AppConstants.ConfigDbConnection]?.ConnectionString ?? "";
    }
}
