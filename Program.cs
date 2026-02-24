using System.Threading.RateLimiting;
using Infostacker.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using SharingService = Infostacker.Services.SharingService;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
const string AllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Configuration.AddJsonFile("version.json", optional: true);

string seqServer = builder.Configuration.GetValue<string>("SeqServer")?.Trim() ?? string.Empty;
if (string.IsNullOrWhiteSpace(seqServer))
{
    seqServer = "https://localhost:5341";
}

builder.Host.UseSerilog((_, _, loggerConfiguration) =>
{
    loggerConfiguration
        .Enrich.WithProperty("Application", "InfostackerService")
        .WriteTo.Console()
        .WriteTo.File("Logs/logs.txt", rollingInterval: RollingInterval.Day)
        .WriteTo.Seq(seqServer)
        .MinimumLevel.Information();
});

builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddTransient<ISharingService, SharingService>();

long maxRequestBodySize = builder.Configuration.GetValue<long?>("MaxRequestBodySizeInBytes") is > 0
    ? builder.Configuration.GetValue<long>("MaxRequestBodySizeInBytes")
    : 104857600L;

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxRequestBodySize;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxRequestBodySize;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

int readRequestsPerMinute = builder.Configuration.GetValue<int?>("RateLimiting:ReadRequestsPerMinute") is > 0
    ? builder.Configuration.GetValue<int>("RateLimiting:ReadRequestsPerMinute")
    : 120;
int writeRequestsPerMinute = builder.Configuration.GetValue<int?>("RateLimiting:WriteRequestsPerMinute") is > 0
    ? builder.Configuration.GetValue<int>("RateLimiting:WriteRequestsPerMinute")
    : 30;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        Log.Warning(
            "Rate limit exceeded for {IpAddress} on {RequestPath}.",
            context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            context.HttpContext.Request.Path.Value);

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync("{\"message\":\"Rate limit exceeded. Try again later.\"}", cancellationToken)
            .ConfigureAwait(false);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        string ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        bool isWriteMethod = HttpMethods.IsPost(httpContext.Request.Method)
                             || HttpMethods.IsPut(httpContext.Request.Method)
                             || HttpMethods.IsDelete(httpContext.Request.Method);

        int permitLimit = isWriteMethod ? writeRequestsPerMinute : readRequestsPerMinute;
        string partitionKey = $"{ipAddress}:{(isWriteMethod ? "write" : "read")}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(AllowSpecificOrigins, policy =>
    {
        policy.WithOrigins("app://obsidian.md")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors(AllowSpecificOrigins);
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();
