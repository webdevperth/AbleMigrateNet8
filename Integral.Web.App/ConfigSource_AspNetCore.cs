using System;
using Integral.Web.Services;
using Microsoft.Extensions.Configuration;

namespace Integral.Web {

  /// <summary>
  /// .NET 10 implementation of IConfigSource backed by Microsoft.Extensions.Configuration.IConfiguration.
  /// Drop-in replacement for ConfigSource_Framework after migrating off ASP.NET Web Forms / .NET Framework 4.8.
  ///
  /// Expects appsettings.json structured with connection strings under "ConnectionStrings" (standard
  /// IConfiguration.GetConnectionString convention) and appSetting keys at the top level. If appSettings
  /// are nested under a section instead (e.g. "AppSettings"), construct with
  /// new ConfigSource_AspNetCore(rootConfig, rootConfig.GetSection("AppSettings")).
  /// </summary>
  public sealed class ConfigSource_AspNetCore : IConfigSource {

    private readonly IConfiguration _connectionStringSource;
    private readonly IConfiguration _appSettingsSource;

    public ConfigSource_AspNetCore(IConfiguration configuration)
      : this(configuration, configuration) { }

    public ConfigSource_AspNetCore(IConfiguration connectionStringSource, IConfiguration appSettingsSource) {
      _connectionStringSource = connectionStringSource ?? throw new ArgumentNullException(nameof(connectionStringSource));
      _appSettingsSource = appSettingsSource ?? throw new ArgumentNullException(nameof(appSettingsSource));
    }

    public string GetConnectionString(string name) =>
      _connectionStringSource.GetConnectionString(name);

    public string GetAppSetting(string key) =>
      _appSettingsSource[key];
  }
}
