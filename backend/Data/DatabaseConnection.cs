using System.Net;
using Npgsql;

namespace EducationCrm.Api.Data;

public static class DatabaseConnection
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Normalize(configured);
        }

        var databaseUrl = configuration["DATABASE_URL"] ?? configuration["NEON_DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            return Normalize(databaseUrl);
        }

        return "Host=localhost;Port=5432;Database=counselmate_dev;Username=postgres;Password=postgres";
    }

    private static string Normalize(string rawConnectionString)
    {
        if (!rawConnectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !rawConnectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return rawConnectionString;
        }

        var uri = new Uri(rawConnectionString);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = WebUtility.UrlDecode(userInfo.ElementAtOrDefault(0) ?? string.Empty);
        var password = WebUtility.UrlDecode(userInfo.ElementAtOrDefault(1) ?? string.Empty);
        var database = uri.AbsolutePath.TrimStart('/');

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }
}
