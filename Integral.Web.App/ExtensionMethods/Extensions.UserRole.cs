using System;

namespace Integral.Web {

  public static class ExternalIntegrationExtensions {

    /// <summary>
    /// Creates an ExternalIntegrationUserId from a UserRole enum and user GUID.
    /// Uses the first letter of the role name as prefix (Admin->A, Coach->C, Client->C, Leader->L, Unset->U).
    /// Note: Coach and Client both use 'C' prefix to match Amplitude implementation.
    /// </summary>
    /// <param name="userRole">The user role enum</param>
    /// <param name="userGuid">User GUID</param>
    /// <returns>ExternalIntegrationUserId with format: [Role]-[GUID]</returns>
    public static ExternalUserId? ToExternalUserId(this ConfigHelper.UserRole userRole, Guid? userGuid) {
      if (!userGuid.HasValue) {
        return null;
      }

      var roleString = userRole.ToString();
      char rolePrefix = !string.IsNullOrEmpty(roleString) && roleString.Length > 0
        ? roleString[0]
        : 'U';

      return new ExternalUserId(rolePrefix, userGuid.Value);
    }

  }
}
