using ServerCore;
using ServerCore.Interfaces;
using ServerCore.Services;

namespace ServerWebApi;

public class Program
{
    public static void Main(string[] args)
    {
        // Get the directory where the executable is located
        var executableDirectory = AppContext.BaseDirectory;

        var builder = WebApplication.CreateBuilder(args);

        // Explicitly configure the configuration builder to look in the executable directory
        builder.Configuration.Sources.Clear();
        builder.Configuration
            .SetBasePath(executableDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddCommandLine(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        
        // Log configuration information for debugging
        builder.Services.AddSingleton<IConfiguration>(provider => builder.Configuration);

        // Add PII Filter Service - IMPORTANT: Register before database service
        builder.Services.AddSingleton<IPiiFilterService, PiiFilterService>();

        // Add database service with explicit configuration validation
        try
        {
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.Error.WriteLine($"ERROR: DefaultConnection string not found in configuration.");
                Console.Error.WriteLine($"Looking for appsettings.json in: {executableDirectory}");
                Console.Error.WriteLine($"Files in directory: {string.Join(", ", Directory.GetFiles(executableDirectory, "*.json"))}");
                Environment.Exit(1);
            }
    
            Console.Error.WriteLine($"INFO: Found connection string, server: {new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString).DataSource}");
            builder.Services.AddDatabaseService(builder.Configuration);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Failed to configure database service: {ex.Message}");
            Environment.Exit(1);
        }
        
        // Add database service
        builder.Services.AddDatabaseService(builder.Configuration);
        
        // Add MCP server
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();
        
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        try
        {
            Console.Error.WriteLine("INFO: MCP Server starting with PII filtering enabled...");
            app.Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: Failed to start MCP server: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}