using Integral.Web;
using Integral.Web.Services;
using System.Runtime.InteropServices.ComTypes;

namespace Integral.Web.PortalSite.AppCode {

  /// <summary>
  /// Extension methods for ApplicationInsights builders to simplify adding user information.
  /// </summary>
  public static class ApplicationInsightsExtensions {

    /// <summary>
    /// Automatically populates all available session details from SessionHelper.
    /// Adds: UserId, OrgId, UserEmail, UserRole.
    /// If no user is logged in, this method does nothing.
    /// Use this when working with the current authenticated session user.
    /// </summary>
    /// <param name="builder">The telemetry builder</param>
    /// <returns>The builder for method chaining</returns>
    public static T FromSession<T>(this T builder)
      where T : ITelemetryBuilder<T> {

      var service = ServiceLocator.Instance.GetService<ITelemetryService>();
      var userInfo = SessionHelper.GetUserInfoOrNull();
      if (userInfo != null && service != null) {
        builder.AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId());
      }
      return builder;
    }
  }
}
