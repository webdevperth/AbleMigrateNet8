using System;
using System.Globalization;
using System.Net;
using Integral.Integrations.Amplitude;
using Integral.Integrations.Intercom;
using Integral.Web;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.PortalSite.Endpoints;
using Integral.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal class Program {

  private static Uri LaunchUrl;

  private static void Main(string[] args) {

    // Force TLS 1.2 / 1.3 for all outbound HTTPS — required for modern API integrations.
    ServicePointManager.SecurityProtocol =
      SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

    var builder = WebApplication.CreateBuilder(args);

    // Configuration sources (CreateBuilder already adds appsettings.json,
    // appsettings.{Environment}.json, env vars and user secrets in development).
    // Add appsettings.Local.json for developer-local secrets that should never be committed.
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

    var applicationUrls = builder.Configuration.GetValue<string>("ApplicationUrl");
    if (!applicationUrls.IsNullOrEmpty()) builder.WebHost.UseUrls(applicationUrls);

    LaunchUrl = builder.Configuration.GetValue<Uri>("LaunchUrl");

    // For localhost, this ensures that the domain in X-Forwarded-Host (like when using Cloudflare tunnel forwarding)
    // is recoggnised as the actual domain in the app request context, instead of localhost.
    // Helps when mimicing prod behaviour as one can assume a proper domain is being used.
    builder.Services.Configure<ForwardedHeadersOptions>(options => {
      options.ForwardedHeaders =
          ForwardedHeaders.XForwardedHost |
          ForwardedHeaders.XForwardedProto |
          ForwardedHeaders.XForwardedFor;
      // Optional for local development
      options.KnownNetworks.Clear();
      options.KnownProxies.Clear();
    });

    builder.Services.AddHsts(options => {
      options.MaxAge = TimeSpan.FromDays(365);
      options.IncludeSubDomains = true;
      options.Preload = false; // set true only if you intend to submit to browser preload lists
    });

    builder.Services.AddHttpContextAccessor();

    builder.Services.AddSingleton<ISystemWeb, SystemWeb_AspNetCore>();

    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<IAppCache, AppCache_AspNetCore>();

    builder.Services.AddSingleton<IConfigSource>(sp => new ConfigSource_AspNetCore(builder.Configuration));

    builder.Services.AddRazorPages().AddNewtonsoftJson();

    builder.Services.AddDistributedMemoryCache();

    builder.Services.AddSession(o => {
      o.IdleTimeout = TimeSpan.FromMinutes(120);
      o.Cookie.HttpOnly = true;
      o.Cookie.IsEssential = true;
      o.Cookie.SameSite = SameSiteMode.Strict;
    });

    builder.Services.AddApplicationInsightsTelemetry(options => {
      // Connection string priority: env var APPLICATIONINSIGHTS_CONNECTION_STRING (read by default),
      // then ApplicationInsights:ConnectionString in IConfiguration, then the legacy flat key.
      if (string.IsNullOrWhiteSpace(options.ConnectionString)) {
        options.ConnectionString = builder.Configuration["ApplicationInsightsConnectionString"];
      }
    });

    builder.Services.Configure<RequestLocalizationOptions>(o => {
      var au = new CultureInfo("en-AU");
      o.DefaultRequestCulture = new RequestCulture(au, au);
      o.SupportedCultures = new[] { au };
      o.SupportedUICultures = new[] { au };
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment()) {
      if (LaunchUrl != null) {
        app.UseForwardedHeaders();
      }
    } else {
      app.UseExceptionHandler("/Error");
      app.UseHsts();
    }

    app.UseHttpsRedirection();

    // Wire legacy ServiceLocator -> DI so existing call sites
    // (ServiceLocator.Instance.GetService<T>) keep working unchanged.
    ServiceLocator.Instance.Register(() => app.Services.GetRequiredService<ISystemWeb>());
    ServiceLocator.Instance.Register(() => app.Services.GetRequiredService<IAppCache>());
    ServiceLocator.Instance.Register(() => app.Services.GetRequiredService<IConfigSource>());

    // One-time DI registration formerly performed by IHttpModule.Init implementations.
    // Order: ApplicationInsights first so other modules can use ITelemetryService in their error paths.
    ApplicationInsightsBootstrap.Initialize();
    IntercomBootstrap.Initialize();
    AmplitudeBootstrap.Initialize();

    AppHelper.SetAppStartTime(DateTime.UtcNow);

    // Graceful shutdown — invoke the same Shutdown() calls the .NET Framework Application_End did.
    app.Lifetime.ApplicationStopping.Register(() => {

      try {
        ServiceLocator.Instance.GetService<IIntercomEventService>()?.Shutdown();
      } catch (Exception ex) {
        LogHelper.LogError($"Error shutting down IntercomEventService: {ex.Message}");
      }

      try {
        ServiceLocator.Instance.GetService<IIntercomJwtService>()?.Shutdown();
      } catch (Exception ex) {
        LogHelper.LogError($"Error shutting down IntercomJwtService: {ex.Message}");
      }

      try {
        ServiceLocator.Instance.GetService<ITelemetryService>()?.Shutdown();
      } catch (Exception ex) {
        LogHelper.LogError($"Error shutting down ApplicationInsights: {ex.Message}");
      }

      try {
        ServiceLocator.Instance.GetService<IAmplitudeService>()?.Shutdown();
      } catch (Exception ex) {
        LogHelper.LogError($"Error shutting down Amplitude: {ex.Message}");
      }
    });

    // Apply LaunchUrl
    if (LaunchUrl != null) {
      app.Use(async (context, next) => {
        if (!context.Request.Host.Host.Equals(LaunchUrl.Host)) {
          context.Response.Redirect(LaunchUrl.ToString());
          return;
        }
        await next();
      });
    }

    app.Use(async (context, next) => {
      AppHelper.SetRequestStartTimeUtcNow();
      await next();
    });

    // Cache-busting filename rewrite: "/foo__v1.2.3.css" -> "/foo.css". Mirrors IIS rule
    // "CachedResourceVersioning". Must run before UseStaticFiles so static files are served
    // from the rewritten path.
    app.UseRewriter(new RewriteOptions()
      .AddRewrite(@"^(.*)__v[0-9.]+\.(css|js|gif|jpg|png|ico)$", "$1.$2", skipRemainingRules: true));

    app.UseRequestLocalization();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseSession();

    // EndRequest equivalent: runs after the endpoint executes. Needs session to be available,
    // hence placed after UseSession.
    app.Use(async (context, next) => {

      await next();

      if (PathHelper.IsCurrentUrlPartial()
        && SessionHelper.CanShowDebugMessages
        && !SystemWeb.IsResponseContentTypeJson) {
        try {
          SystemWeb.ResponseWriteLine("<script>" + LogHelper.GetResponseLogJS() + "\n</script>");
        } catch (Exception) {
          // ignore — response may already be flushed
        }
      }

      AmplitudeBootstrap.OnEndRequest();
    });

    app.MapRazorPages();

    AjaxEndpoints.MapEndpoints(app);
    ApiEndpoints.MapEndpoints(app);

    app.MapGet("/health", () => "ok");

    app.Run();
  }
}
