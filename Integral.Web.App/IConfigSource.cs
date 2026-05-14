namespace Integral.Web.Services {

  /// <summary>
  /// Abstraction over application configuration (appSettings + connection strings).
  /// Lets ConfigHelper read configuration without coupling to System.Configuration.ConfigurationManager,
  /// so an IConfiguration-backed implementation can be swapped in during the .NET 10 migration.
  /// </summary>
  public interface IConfigSource {

    /// <summary>
    /// Returns the named connection string, or null if not configured.
    /// </summary>
    string GetConnectionString(string name);

    /// <summary>
    /// Returns the value of the named appSetting, or null if not configured.
    /// An empty-string return value means the setting exists but is blank.
    /// </summary>
    string GetAppSetting(string key);
  }
}
