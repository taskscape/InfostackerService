using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using Serilog.Core;
using ShareAPI.Services;

WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);
string? MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
string seqServer = string.IsNullOrWhiteSpace(builder.Configuration.GetValue<string>("SeqServer")) ? "https://localhost:5341" : builder.Configuration.GetValue<string>("SeqServer");

Logger? logger = new LoggerConfiguration()
    .Enrich.WithProperty("Application", "InfostackerService")
    .WriteTo.Console()
    .WriteTo.File("Logs/logs.txt",
        rollingInterval: RollingInterval.Day)
    .WriteTo.Seq(seqServer ?? "https://localhost:5341")
    .MinimumLevel.Information()
    .CreateLogger();

// Add services to the container.
builder.Configuration.AddJsonFile("version.json");
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins("app://obsidian.md")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});
builder.Services.AddTransient<ISharingService, SharingService>();
builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);

WebApplication? app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCors(MyAllowSpecificOrigins);

app.MapControllers();

app.Run();
