using System.Text.Json.Serialization;
using AskARabbi.Api.Authentication;
using AskARabbi.Api.Configuration;
using AskARabbi.Api.Development;
using AskARabbi.Api.Errors;
using AskARabbi.Api.Persistence;
using AskARabbi.Api.Usage;
using AskARabbiLIB.Conversations;
using AskARabbiLIB.ConversationSettings;
using AskARabbiLIB.Usage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

var localDevelopmentOptions = builder.Configuration.GetSection(LocalDevelopmentOptions.SectionName).Get<LocalDevelopmentOptions>() ?? new LocalDevelopmentOptions();
localDevelopmentOptions.Validate(builder.Environment.EnvironmentName);
var allowedFrontendOrigins = (builder.Configuration.GetSection(FrontendCorsOptions.SectionName).Get<FrontendCorsOptions>() ?? new FrontendCorsOptions()).GetAllowedOrigins(builder.Environment.IsDevelopment());
if (localDevelopmentOptions.UseDemoServices)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<ConversationSettingsService>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<WorkOsCookieAuthenticationEvents>();
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsOptions.PolicyName, policy =>
{
    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    if (allowedFrontendOrigins.Count > 0)
    {
        policy.WithOrigins([.. allowedFrontendOrigins]);
    }
    else
    {
        policy.SetIsOriginAllowed(_ => false);
    }
}));

var usageOptions = builder.Configuration.GetSection(MonthlyUsageOptions.SectionName).Get<MonthlyUsageOptions>() ?? new MonthlyUsageOptions();
usageOptions.Validate();
builder.Services.AddSingleton(usageOptions);
builder.Services.AddScoped(provider => new MonthlyUsageService(provider.GetRequiredService<IUsageStore>(), usageOptions.MonthlyAnswerLimit, provider.GetRequiredService<TimeProvider>()));

if (localDevelopmentOptions.UseDemoServices)
{
    builder.Services.AddAskRabbiLocalDevelopment(builder.Configuration);
}
else
{
    builder.Services.AddAskRabbiPersistence(builder.Configuration);
    builder.Services.AddAskRabbiAuthentication(builder.Configuration, builder.Environment);
}
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "AskRabbi.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.EventsType = typeof(WorkOsCookieAuthenticationEvents);
});
builder.Services.AddAuthorization();
if (localDevelopmentOptions.UseDemoServices)
{
    var localKeyDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "AskARabbi", "LocalDevelopment-DataProtectionKeys"));
    builder.Services.AddDataProtection().PersistKeysToFileSystem(localKeyDirectory).SetApplicationName("AskARabbi.LocalDevelopment");
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(FrontendCorsOptions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

/// <summary>Provides the application entry point to integration tests.</summary>
public partial class Program;
