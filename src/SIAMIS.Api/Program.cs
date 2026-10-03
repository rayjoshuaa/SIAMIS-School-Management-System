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
builder.Services.AddScoped<EmployeeLeaveService>();
builder.Services.AddScoped<IEmployeeLeaveService>(sp => sp.GetRequiredService<EmployeeLeaveService>());
builder.Services.AddScoped<SIAMIS.Application.Leave.ILeaveEvidenceSandwichService>(sp => sp.GetRequiredService<EmployeeLeaveService>());
builder.Services.AddScoped<SIAMIS.Application.Leave.ILeaveFoundationService, LeaveFoundationService>();
builder.Services.AddScoped<IEmployeePerformanceService, EmployeePerformanceService>();
builder.Services.AddScoped<IMasterDataService, MasterDataService>();
builder.Services.AddScoped<IEmploymentStatusService, EmploymentStatusService>();
builder.Services.AddScoped<IEmploymentLifecycleService, EmploymentLifecycleService>();
builder.Services.AddScoped<IEmploymentResolver, EmploymentLifecycleService>();
builder.Services.AddScoped<IPayrollEmploymentContextService, PayrollEmploymentContextService>();
builder.Services.AddScoped<IPayrollComponentService, PayrollComponentService>();
builder.Services.AddScoped<IPayrollPeriodService, PayrollPeriodService>();
builder.Services.AddScoped<IPayrollRuleService, PayrollRuleService>();
builder.Services.AddScoped<IPayrollRuleTargetService, PayrollRuleTargetService>();
builder.Services.AddScoped<IPayrollRuleEvaluator, PayrollRuleEvaluator>();
builder.Services.AddScoped<IPayrollSettingsService, PayrollSettingsService>();
builder.Services.AddScoped<StatutoryPolicyService>();
builder.Services.AddScoped<IStatutoryPolicyService>(sp => sp.GetRequiredService<StatutoryPolicyService>());
builder.Services.AddScoped<IStatutoryPolicyResolver>(sp => sp.GetRequiredService<StatutoryPolicyService>());
builder.Services.AddScoped<IEmployeeStatutoryService, EmployeeStatutoryService>();
builder.Services.AddScoped<ISection33ContributionWageResolver, Section33ContributionWageResolver>();
builder.Services.AddScoped<IPitIncomeResolver, PitIncomeResolver>();
builder.Services.AddScoped<IPitCalculator, PitCalculator>();
builder.Services.AddScoped<IPitPaymentScheduleService, PitPaymentScheduleService>();
builder.Services.AddScoped<IPitPayrollService, PitPayrollService>();
builder.Services.AddScoped<IPitCalculationPreviewService, PitCalculationPreviewService>();
builder.Services.AddScoped<ISection33Calculator, Section33Calculator>();
builder.Services.AddScoped<ISection33PayrollService, Section33PayrollService>();
builder.Services.AddScoped<IPayrollGenerationService, PayrollGenerationService>();
builder.Services.AddScoped<IPayrollCalculationService, PayrollCalculationService>();
builder.Services.AddScoped<IBasicSalaryEntitlementService, BasicSalaryEntitlementService>();
builder.Services.AddScoped<IPayrollPreviewService, PayrollPreviewService>();
builder.Services.AddScoped<IEmployeePayrollService, EmployeePayrollService>();
builder.Services.AddScoped<IOrganizationProfileService, OrganizationProfileService>();
builder.Services.AddScoped<PayrollOperationsService>();
builder.Services.AddScoped<IPayrollOperationsService>(sp => sp.GetRequiredService<PayrollOperationsService>());
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
