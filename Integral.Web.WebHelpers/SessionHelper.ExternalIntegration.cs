using Integral.Integrations;
using System;

namespace Integral.Web {

  /// <summary>
  /// SessionHelper extension for external integration helpers
  /// </summary>
  public partial class SessionHelper {

    /// <summary>
    /// Creates an ExternalIntegrationUserId from the currently logged in user.
    /// </summary>
    /// <returns>ExternalIntegrationUserId for the current user, or default value if no user is logged in</returns>
    public static ExternalUserId? AsExternalUserId() {
      var userInfo = GetUserInfoOrNull();

      if (userInfo == null) {
        // Return a default ExternalIntegrationUserId for unknown/not logged in users
        return new ExternalUserId('U', Guid.Empty);
      }

      var userRole = GetUserRole();
      return userRole.ToExternalUserId(userInfo.UserGuid);
    }
  }
}
