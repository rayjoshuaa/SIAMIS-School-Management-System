using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

internal static class F51BSupportingTests
{
    public static async Task RunAsync(HttpClient admin, HttpClient hr, HttpClient payroll, HttpClient anonymous, Guid employee, Guid other, Guid addressType)
    {
        int checks = 0;
        void Check(bool pass, string label) { if (!pass) throw new InvalidOperationException("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }
        string Root(string kind) => $"/api/employees/{employee}/{kind}";
        foreach (var kind in new[] { "contacts", "addresses", "emergency-contacts" })
        {
            Check((await anonymous.GetAsync(Root(kind))).StatusCode == HttpStatusCode.Unauthorized, "anonymous supporting read denied " + kind);
            Check((await payroll.GetAsync(Root(kind))).StatusCode == HttpStatusCode.Forbidden, "PayrollAdmin has no employee supporting access " + kind);
            Check((await payroll.PostAsJsonAsync(Root(kind), new { })).StatusCode == HttpStatusCode.Forbidden, "PayrollAdmin cannot mutate supporting records " + kind);
            Check((await hr.GetAsync(Root(kind))).StatusCode == HttpStatusCode.OK, "HR supporting read supported " + kind);
        }
        string csrf = admin.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single();
        admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Check((await admin.PostAsJsonAsync(Root("contacts"), new { mobile = "0100000000" })).StatusCode == HttpStatusCode.BadRequest, "supporting mutation requires real CSRF");
        admin.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        Check((await hr.PostAsJsonAsync(Root("contacts"), new { })).StatusCode == HttpStatusCode.BadRequest, "empty contact rejected");
        Check((await hr.PostAsJsonAsync(Root("contacts"), new { workEmail = "invalid" })).StatusCode == HttpStatusCode.BadRequest, "invalid email rejected");
        Check((await hr.PostAsJsonAsync(Root("addresses"), new { addressLine1 = "Synthetic" })).StatusCode == HttpStatusCode.BadRequest, "address type required");
        Check((await hr.PostAsJsonAsync(Root("emergency-contacts"), new { name = "Synthetic", relationship = "Relative" })).StatusCode == HttpStatusCode.BadRequest, "emergency phone required");
        var records = new[] {
            (Kind: "contacts", Id: "employeeContactId", Body: (object)new { workEmail = "synthetic@example.invalid", mobile = "0100000000", isPrimary = true }),
            (Kind: "addresses", Id: "employeeAddressId", Body: (object)new { addressTypeId = addressType, addressLine1 = "Synthetic address", isPrimary = true }),
            (Kind: "emergency-contacts", Id: "emergencyContactId", Body: (object)new { name = "Synthetic Relative", relationship = "Relative", phone = "0100000001", alternativePhone = "0100000002", address = "Synthetic emergency address", isPrimary = true })
        };
        foreach (var record in records)
        {
            var create = await hr.PostAsJsonAsync(Root(record.Kind), record.Body);
            Check(create.StatusCode == HttpStatusCode.Created, "supporting record create " + record.Kind);
            var created = await create.Content.ReadFromJsonAsync<JsonElement>();
            Guid id = created.GetProperty(record.Id).GetGuid();
            var update = await hr.PutAsJsonAsync(Root(record.Kind) + "/" + id, record.Body);
            Check(update.StatusCode == HttpStatusCode.OK, "supporting record update " + record.Kind);
            Check((await hr.PutAsJsonAsync($"/api/employees/{other}/{record.Kind}/{id}", record.Body)).StatusCode == HttpStatusCode.NotFound, "cross-employee child ID rejected " + record.Kind);
            var detail = await hr.GetFromJsonAsync<JsonElement>($"/api/employees/{employee}");
            string collection = record.Kind == "emergency-contacts" ? "emergencyContacts" : record.Kind;
            var child = detail.GetProperty(collection).EnumerateArray().Single(x => x.GetProperty(record.Id).GetGuid() == id);
            Check(child.GetProperty("isPrimary").GetBoolean(), "profile reflects supporting primary " + record.Kind);
            if (record.Kind == "emergency-contacts")
            {
                Check(child.GetProperty("alternativePhone").GetString() == "0100000002" && child.GetProperty("address").GetString() == "Synthetic emergency address", "additive emergency DTO exposes persisted fields");
                Check((await hr.PutAsJsonAsync(Root(record.Kind) + "/" + id, new { alternativePhone = "", address = "" })).StatusCode == HttpStatusCode.OK, "emergency optional values clear through existing contract");
            }
            if (record.Kind == "contacts")
            {
                Check((await hr.PutAsJsonAsync(Root(record.Kind) + "/" + id, new { mobile = "" })).StatusCode == HttpStatusCode.OK, "contact blank update follows preservation contract");
                var preserved = await hr.GetFromJsonAsync<JsonElement[]>(Root(record.Kind));
                Check(preserved!.Single(x => x.GetProperty(record.Id).GetGuid() == id).GetProperty("mobile").GetString() == "0100000000", "contact blank does not silently clear existing value");
            }
            Check((await hr.DeleteAsync(Root(record.Kind) + "/" + id)).StatusCode == HttpStatusCode.NoContent, "existing supporting deletion regression " + record.Kind);
        }
        var documents = await admin.GetAsync($"/api/employees/{employee}/documents");
        Check(documents.StatusCode == HttpStatusCode.Forbidden, "SystemAdmin does not bypass HRDocuments (HTTP " + (int)documents.StatusCode + ")");
        Check((await hr.GetAsync("/api/payroll-components")).StatusCode == HttpStatusCode.Forbidden, "HRAdmin has no Payroll.Read");
        Console.WriteLine($"PASS: {checks} F5.1B supporting API/ownership/security assertions; disposable database only.");
    }
}
