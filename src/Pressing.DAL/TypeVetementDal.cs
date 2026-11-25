using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Pressing.DAL;

public sealed class TypeVetementItem
{
    public int IdTypeVetement { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public decimal PrixUnitaire { get; set; }
}

public sealed class TypeVetementDal
{
    private readonly DbConnectionFactory _connectionFactory;

    public TypeVetementDal(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public List<TypeVetementItem> GetActifs()
    {
        List<TypeVetementItem> items = new();

        const string sql = @"
            SELECT IdTypeVetement, Libelle, PrixUnitaire
            FROM TypeVetement
            WHERE Actif = 1
            ORDER BY Libelle ASC;";

        using SqlConnection connection = _connectionFactory.CreateConnection();
        using SqlCommand command = new(sql, connection);

        connection.Open();
        using SqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new TypeVetementItem
            {
                IdTypeVetement = reader.GetInt32(0),
                Libelle = reader.GetString(1),
                PrixUnitaire = reader.GetDecimal(2)
            });
        }

        return items;
    }
}
