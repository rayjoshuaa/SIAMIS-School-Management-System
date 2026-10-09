using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAMIS.Application.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

internal static class EmployeeRegistrationIdempotency
{
    internal const string Operation = "Employee.Register.v1";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static byte[] Hash(CreateEmployeeRequest request)
    {
        var node = JsonSerializer.SerializeToNode(request, Json)!.AsObject();
        // Extension fields ignored by the business contract are not part of its semantics.
        // Retired server-owned fields are still rejected by existing validation.
        foreach (var key in request.AdditionalProperties?.Keys ?? Enumerable.Empty<string>()) node.Remove(key);
        foreach (var collection in new[] { "contacts", "addresses", "emergencyContacts" })
            if (node[collection] is null) node[collection] = new JsonArray();
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes)) WriteCanonical(writer, node);
        return SHA256.HashData(bytes.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            writer.WriteStartObject();
            foreach (var pair in obj.OrderBy(x => x.Key, StringComparer.Ordinal))
            { writer.WritePropertyName(pair.Key); WriteCanonical(writer, pair.Value); }
            writer.WriteEndObject();
        }
        else if (node is JsonArray array)
        {
            writer.WriteStartArray(); foreach (var value in array) WriteCanonical(writer, value); writer.WriteEndArray();
        }
        else if (node is JsonValue value && value.TryGetValue<string>(out var text))
        { if (string.IsNullOrWhiteSpace(text)) writer.WriteNullValue(); else writer.WriteStringValue(text.Trim()); }
        else if (node is JsonValue number && number.TryGetValue<decimal>(out var decimalValue))
            writer.WriteRawValue(decimalValue.ToString("G29", CultureInfo.InvariantCulture));
        else if (node is null) writer.WriteNullValue();
        else node.WriteTo(writer);
    }

    internal static async Task<bool> LockAsync(SIAMISDbContext db, Guid actorId, Guid key, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandTimeout = 10;
        command.CommandText = "DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000; SELECT @r;";
        command.Parameters.Add(new SqlParameter("@resource", $"{Operation}:{actorId:D}:{key:D}"));
        int result = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        ct.ThrowIfCancellationRequested();
        if (result is -1 or -2 or -3) return false;
        if (result < 0) throw new InvalidOperationException("Registration lock could not be established.");
        return true;
    }
}
