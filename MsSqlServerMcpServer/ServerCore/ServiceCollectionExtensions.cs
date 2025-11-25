using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using ServerCore.Interfaces;
using ServerCore.Services;

namespace ServerCore;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the database service to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing connection strings</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddDatabaseService(this IServiceCollection services, IConfiguration configuration)
    {
        // Validate that connection string exists
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("DefaultConnection string is required in configuration");
        }

        services.AddScoped<IDatabaseService, DatabaseServiceBase>();
        return services;
    }

    /// <summary>
    /// Adds the database service to the dependency injection container with a custom connection string name
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing connection strings</param>
    /// <param name="connectionStringName">Name of the connection string in configuration</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddDatabaseService(this IServiceCollection services, IConfiguration configuration, string connectionStringName)
    {
        // Validate that connection string exists
        var connectionString = configuration.GetConnectionString(connectionStringName);
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException($"Connection string '{connectionStringName}' is required in configuration");
        }

        // Create a configuration wrapper that maps the custom connection string to DefaultConnection
        var configurationWrapper = new ConfigurationWrapper(configuration, connectionStringName);
        
        services.AddScoped<IDatabaseService>(provider => 
            new DatabaseServiceBase(configurationWrapper, provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DatabaseServiceBase>>(), provider.GetRequiredService<IPiiFilterService>()));
        
        return services;
    }
}

/// <summary>
/// Configuration wrapper to map custom connection string names to DefaultConnection
/// </summary>
internal class ConfigurationWrapper(IConfiguration configuration, string connectionStringName) : IConfiguration
{
    public string? this[string key] 
    { 
        get => configuration[key]; 
        set => configuration[key] = value; 
    }

    public IEnumerable<IConfigurationSection> GetChildren() => configuration.GetChildren();

    public IChangeToken GetReloadToken() => configuration.GetReloadToken();

    public IConfigurationSection GetSection(string key) => configuration.GetSection(key);

    public string? GetConnectionString(string name)
    {
        return configuration.GetConnectionString(name == "DefaultConnection" ? connectionStringName : name);
    }
}