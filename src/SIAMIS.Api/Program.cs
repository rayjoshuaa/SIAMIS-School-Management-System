using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.MasterData;
using SIAMIS.Application.Payroll;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});
builder.Services.AddDbContext<SIAMISDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SIAMIS")));
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IEmployeeContactsService, EmployeeContactsService>();
builder.Services.AddScoped<IEmployeeAddressesService, EmployeeAddressesService>();
builder.Services.AddScoped<IEmployeeEmergencyContactsService, EmployeeEmergencyContactsService>();
builder.Services.AddScoped<IEmployeeContractDocumentService, EmployeeContractDocumentService>();
builder.Services.AddScoped<IEmployeeCompensationService, EmployeeCompensationService>();
builder.Services.AddScoped<IEmployeeHistoryService, EmployeeHistoryService>();
builder.Services.AddScoped<IEmployeeAttendanceService, EmployeeAttendanceService>();
builder.Services.AddScoped<IEmployeeLeaveService, EmployeeLeaveService>();
builder.Services.AddScoped<IEmployeePerformanceService, EmployeePerformanceService>();
builder.Services.AddScoped<IMasterDataService, MasterDataService>();
builder.Services.AddScoped<IPayrollComponentService, PayrollComponentService>();
builder.Services.AddScoped<IPayrollPeriodService, PayrollPeriodService>();
builder.Services.AddScoped<IPayrollRuleService, PayrollRuleService>();
builder.Services.AddScoped<IPayrollGenerationService, PayrollGenerationService>();
builder.Services.AddScoped<IPayrollCalculationService, PayrollCalculationService>();
builder.Services.AddScoped<IPayrollPreviewService, PayrollPreviewService>();
builder.Services.AddScoped<IEmployeePayrollService, EmployeePayrollService>();
builder.Services.AddScoped<IEmployeePayrollComponentAssignmentService, EmployeePayrollComponentAssignmentService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "SIAMIS API",
    timestampUtc = DateTimeOffset.UtcNow
})).WithName("GetHealth").WithTags("Status");

app.Run();
