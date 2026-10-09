using System.Data;
using Microsoft.Data.SqlClient;

namespace SIAMIS.Infrastructure.Services;

public static class EmployeeNumberAllocator
{
    public static async Task<string> ReserveAsync(string connectionString, Guid employeeId, CancellationToken ct)
    {
        // Separate, non-enlisted connection: registration rollback must not erase reservations.
        var configuration = new SqlConnectionStringBuilder(connectionString) { Enlist = false };
        await using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("dbo.ReserveEmployeeNumber", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add(new SqlParameter("@EmployeeId", SqlDbType.UniqueIdentifier) { Value = employeeId });
        return (string)(await command.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Employee number reservation failed."));
    }
}
