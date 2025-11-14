using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Swashbuckle.AspNetCore.Filters;
using System.Text;
using WebApplication1.Controllers;
using WebApplication1.Data;
using WebApplication1.Middleware;
using WebApplication1.Repositories;
using WebApplication1.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
var port = Environment.GetEnvironmentVariable("PORT") ?? "5025";
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File($"logs/app-{port}.log", rollingInterval: RollingInterval.Day);
});

// Configure Kestrel
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add logging services
builder.Services.AddLogging();

// Existing services
builder.Services.AddMemoryCache();
builder.Services.AddLogging(logging => logging.AddConsole());
// Make sure this scans the right assembly (yours with Features/Notifications)
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);  // main assembly
    cfg.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());  // Scans ALL (including Features.Notifications)
});

// Configure HttpClient for ChatController with longer timeout
builder.Services.AddHttpClient<ChatController>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Configure HttpClient for EmbeddingService with longer timeout
builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2); // 2 minutes for embedding requests
});

// Add vector store and RAG services
builder.Services.AddSingleton<IVectorStore, InMemoryVectorStore>();
builder.Services.AddScoped<IRAGService, RAGService>();

// Configure HttpClient for RAGService with much longer timeout for AI operations
builder.Services.AddHttpClient<RAGService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for LLM requests
});

// Database configuration
builder.Services.AddDbContext<authdbcontext>(options =>
{
    options.UseSqlite("Data Source=authdb.sqlite")
    .EnableSensitiveDataLogging()
    .EnableDetailedErrors()
    .LogTo(Console.WriteLine, LogLevel.Information);
});

// Identity and Authorization
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<authdbcontext>();

// User services
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<UserService>();

// Controllers and API
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Add JWT Authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is missing.");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Swagger Configuration
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Log Analysis API",
        Version = "v1",
        Description = "AI-powered log analysis and summarization API with Ollama integration"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter 'Bearer' followed by a space and then your JWT token.\nExample: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\n\n**Note:** The access token is valid for 30 minutes.",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
    options.ExampleFilters();
});

builder.Services.AddSwaggerExamplesFromAssemblyOf<Program>();

// Redis Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
    options.InstanceName = "WebApplication1_";
});


var app = builder.Build();

// Seed roles
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<authdbcontext>();
    dbContext.Database.EnsureCreated();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var roles = new[] { "Admin", "User" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}

// Seed RAG knowledge base
using (var scope = app.Services.CreateScope())
{
    var ragService = scope.ServiceProvider.GetRequiredService<IRAGService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var sampleDocuments = new[]
        {
            new
            {
                Content = @"Log analysis and monitoring are critical components of system observability. Common log levels include DEBUG (detailed diagnostic info), INFO (general information), WARNING (potentially harmful situations), ERROR (error events that might still allow application to continue), and CRITICAL (very serious error events that might cause application to abort). Proper log analysis helps identify performance bottlenecks, security issues, and system failures before they impact users.",
                Metadata = new Dictionary<string, object>
                {
                    ["category"] = "Logging",
                    ["source"] = "System Administration Guide",
                    ["topic"] = "Log Analysis"
                }
            },
            new
            {
                Content = @"Database connection errors are among the most common issues in web applications. Common causes include network timeouts, connection pool exhaustion, incorrect connection strings, database server downtime, and firewall blocking. Monitoring database connection health through logs helps maintain application reliability and performance.",
                Metadata = new Dictionary<string, object>
                {
                    ["category"] = "Database",
                    ["source"] = "Database Administration Manual",
                    ["topic"] = "Connection Issues"
                }
            },
            new
            {
                Content = @"Memory management issues can cause application instability. High memory usage warnings typically indicate memory leaks, inefficient algorithms, or insufficient system resources. Critical memory issues can lead to out-of-memory exceptions and application crashes. Regular monitoring of memory usage patterns through logs helps prevent system failures.",
                Metadata = new Dictionary<string, object>
                {
                    ["category"] = "Performance",
                    ["source"] = "Performance Monitoring Guide",
                    ["topic"] = "Memory Management"
                }
            }
        };

        foreach (var doc in sampleDocuments)
        {
            logger.LogInformation("Indexing log analysis document: {Content}", doc.Content[..Math.Min(100, doc.Content.Length)]);
            var documentId = await ragService.IndexDocumentAsync(doc.Content, doc.Metadata);
            logger.LogInformation("Indexed document with ID: {DocumentId}", documentId);
        }

        logger.LogInformation("Successfully seeded RAG knowledge base with log analysis documents");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to seed RAG knowledge base - this is normal if Ollama is not running");
    }
}

// Configure middleware pipeline
app.UseSerilogRequestLogging();
app.UseExceptionMiddleware();

// Swagger configuration
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Log Analysis API V1");
    c.OAuthAppName("Log Analysis API");
    c.DocumentTitle = "Log Analysis & Summarization API";
});

// Root redirect
app.MapGet("/", async context =>
{
    context.Response.Redirect("/swagger");
    await Task.CompletedTask;
});

// Middleware order is important
app.UseTokenValidationMiddleware();
app.MapIdentityApi<IdentityUser>();

// HTTPS redirection check
var hasHttps = app.Urls.Any(url => url.StartsWith("https", StringComparison.OrdinalIgnoreCase));
if (hasHttps)
{
    app.UseHttpsRedirection();
}
else
{
    Console.WriteLine("? Skipping HTTPS redirection (no HTTPS binding detected).");
}

app.UseRateLimitingMiddleware();
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<WebApplication1.Hubs.NotificationHub>("/notificationHub");
app.MapControllers();

var boundUrls = app.Urls.ToList();
Console.WriteLine("====================================");
Console.WriteLine("?? Log Analysis API is running!");
foreach (var url in boundUrls)
{
    if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
        Console.WriteLine($"?? Secure: {url}");
    else
        Console.WriteLine($"?? Non-secure: {url}");
}
Console.WriteLine("?? Swagger UI: /swagger");
Console.WriteLine("?? Ollama Integration: Enabled");
Console.WriteLine("?? Log Summarization: /api/RAG/summarize-logs");
Console.WriteLine("Press Ctrl+C to shut down.");
Console.WriteLine("====================================");

app.Run();