using Integral.Web.Services;
using System;
using System.Web;
using Integral.Integrations.Intercom;
using Integral.Integrations.Amplitude;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite {

  public partial class Global : HttpApplication {

    private DateTime? lastApplicationError = null;

    protected void Application_Start() {

      // Force TLS 1.2 and TLS 1.3 for all outbound HTTPS connections, this is a requirement for
      // all modern API integrations as anything < 1.2 is a security vulnerability and should have been phased out
      // since 2022.
      System.Net.ServicePointManager.SecurityProtocol =
        System.Net.SecurityProtocolType.Tls12 |
        System.Net.SecurityProtocolType.Tls13;

      // Register ISystemWeb FIRST — other services depend on it indirectly.
      var systemWeb = new SystemWeb_Framework();
      ServiceLocator.Instance.Register<ISystemWeb>(() => systemWeb);

#if NETFRAMEWORK
      var appCache = new AppCache.AppCache_Framework();
      ServiceLocator.Instance.Register<AppCache.IAppCache>(() => appCache);
#endif
#if NET10_0_OR_GREATER
      // This goes in program.cs
      builder.Services.AddMemoryCache();
      builder.Services.AddSingleton<IAppCache, AppCache.AppCache_AspNetCore>();
#endif

      // One-time DI registration formerly performed by IHttpModule.Init implementations.
      // Order: ApplicationInsights first so other modules can use ITelemetryService in their error paths.
      ApplicationInsightsBootstrap.Initialize();
      IntercomBootstrap.Initialize();
      AmplitudeBootstrap.Initialize();

      AppHelper.SetAppStartTime(DateTime.UtcNow);
    }

    protected void Application_End() {

      try {
        var intercomEvent = ServiceLocator.Instance.GetService<IIntercomEventService>();
        intercomEvent?.Shutdown();
      } catch (Exception x) {
        LogHelper.LogError($"Error shutting down IntercomService: {x.Message}");
      }

      try {
        var intercomJwt = ServiceLocator.Instance.GetService<IIntercomJwtService>();
        intercomJwt?.Shutdown();
      } catch (Exception x) {
        LogHelper.LogError($"Error shutting down IntercomService: {x.Message}");
      }

      try {
        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        telemetry?.Shutdown();
      } catch (Exception ex) {
        LogHelper.LogError($"Error shutting down ApplicationInsights: {ex.Message}");
      }

      try {
        var amplitude = ServiceLocator.Instance.GetService<IAmplitudeService>();
        amplitude?.Shutdown();
      } catch (Exception ex) {
        LogHelper.LogError($"Error shutting down Amplitude: {ex.Message}");
      }
    }

    protected void Application_BeginRequest(object sender, EventArgs e) {

      AppHelper.SetRequestStartTimeUtcNow();

      // Add HSTS header to all requests, except on dev.
      if (!ConfigHelper.IsDevServer) {
        switch (Request.Url.Scheme.ToLower()) {
          case "https":
            Response.AddHeader("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
            break;
          case "http":
            var path = "https://" + Request.Url.Host + Request.Url.PathAndQuery;
            Response.StatusCode = 301; // Moved Permanently
            Response.AddHeader("Location", path);
            break;
        }
      }
    }

    protected void Application_EndRequest(object sender, EventArgs e) {

      var startTimeUtc = AppHelper.GetRequestStartTimeUtc();
      if (startTimeUtc != null) {
        int ms = (DateTime.UtcNow - (DateTime)startTimeUtc).Milliseconds;
      }

      // Show time since last error.
      if (lastApplicationError == null) {
      } else {
      }

      // Add script to show log items if this is a partial and not expecting json.
      // (Log items for full-page requests are output in site.master)
      if (PathHelper.IsCurrentUrlPartial()
        && SessionHelper.CanShowDebugMessages
        && !SystemWeb.IsResponseContentTypeJson) {
        try {
          SystemWeb.ResponseWriteLine("<script>" + LogHelper.GetResponseLogJS() + "\n</script>");
        } catch (Exception) {
          // ignore
        }
      }

      AmplitudeBootstrap.OnEndRequest();
    }

    private string GetErrorPageHtml(string bodyHtml) {

      string html = string.Empty;

      if (!PathHelper.IsCurrentUrlPartial()) {
        html += $@"
        <html>
          <head>
            <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"">
            <meta charset=""UTF-8"">
            <meta http-equiv=""X-UA-Compatible"" content=""IE=edge,chrome=1"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no"" />
            <link href=""/favicon.ico"" type=""image/x-icon"" rel=""shortcut icon"" />
            <link href=""/favicon.ico"" type=""image/x-icon"" rel=""icon"" />
            <link rel=""apple-touch-icon"" href=""{PathHelper.UrlPath.Images}apple-touch-icon.png"" />
            <link rel=""apple-touch-icon-precomposed"" href=""{PathHelper.UrlPath.Images}apple-touch-icon.png"" />
            <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap/3.3.7/css/bootstrap.min.css"" />    <!-- Bootstrap 3.3.6 -->
            <link rel=""stylesheet"" href=""{PathHelper.UrlPath.CSS}AdminLTE-2.3.11-no-importants.min.css"" />
            <link href=""https://fonts.googleapis.com/css2?family=Open+Sans:ital,wght@0,300;0,400;0,500;0,700;0,800;1,400&amp;display=swap"" rel=""stylesheet"">
            <link rel=""stylesheet"" href=""{PathHelper.UrlPath.CSS}portal-site.css"" />
            <link rel=""stylesheet"" href=""{PathHelper.UrlPath.CSS}adminlte-custom-2022.css"" />
            <style>
              .page-error .oops {{ color: var(--site-logo-color); }}
              .modal-body .page-error .oops {{ margin-top: 0; }}
            </style>
          </head>
          <body>
            <div class=""wrapper"">";
      }

      html += $@"
        <div class=""page-error"">
          <h2 class=""oops"">Oops!</h2>
          {bodyHtml}
        </div>";

      if (!PathHelper.IsCurrentUrlPartial()) {
        html += $@"
            </div>
          </body>
        </html>";
      }

      return html;
    }

    // Note that Application_Error firing requires:
    // <system.web>
    //   <customErrors mode="Off"/>
    // </system.web>
    protected void Application_Error(object sender, EventArgs e) {

    }
  }
}
