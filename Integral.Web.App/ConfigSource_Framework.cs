#if NETFRAMEWORK

using System.Configuration;
using Integral.Web.Services;

namespace Integral.Web {

  /// <summary>
  /// .NET Framework implementation of IConfigSource backed by System.Configuration.
  /// Default constructor reads from the active ConfigurationManager (normal IIS / IIS Express operation).
  /// Pass a Configuration to read from an explicitly-loaded config file — e.g. unit tests
  /// that open web.config via ConfigurationManager.OpenMappedExeConfiguration.
  /// </summary>
  public sealed class ConfigSource_Framework : IConfigSource {

    private readonly Configuration _configuration;

    public ConfigSource_Framework() : this(null) { }

    public ConfigSource_Framework(Configuration configuration) {
      _configuration = configuration;
    }

    public string GetConnectionString(string name) {
      if (_configuration != null) {
        return _configuration.ConnectionStrings.ConnectionStrings[name]?.ConnectionString;
      }
      return ConfigurationManager.ConnectionStrings[name]?.ConnectionString;
    }

    public string GetAppSetting(string key) {
      if (_configuration != null) {
        return _configuration.AppSettings.Settings[key]?.Value;
      }
      return ConfigurationManager.AppSettings[key];
    }
  }
}

#endif
