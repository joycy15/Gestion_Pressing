using System;
using System.Configuration;
using Microsoft.Data.SqlClient;

namespace Pressing.DAL;

public sealed class DbConnectionFactory
{
    private const string DefaultConnectionString =
        @"Server=.\SQLEXPRESS;Database=GestionPressing;Trusted_Connection=True;TrustServerCertificate=True;";

    private readonly string _connectionString;

    public DbConnectionFactory(string? connectionString = null)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _connectionString = connectionString;
            return;
        }

        string? configured = ConfigurationManager.ConnectionStrings["PressingDb"]?.ConnectionString;
        _connectionString = !string.IsNullOrWhiteSpace(configured) ? configured : DefaultConnectionString;
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
