using System;
using System.Collections.Generic;
using System.Linq;
using Integral.Integrations.Intercom;
using Integral.Web.Services;
using Newtonsoft.Json;

namespace Integral.Web.PortalSite.AppCode {

  /// <summary>
  /// Helper methods for Intercom integration that require access to user types
  /// </summary>
  public static class IntercomHelpers {

    /// <summary>
    /// Safely sends an Intercom event with automatic exception handling.
    /// Guards against exceptions during the entire builder chain and Send() call.
    /// Uses GetService for telemetry (not GetRequiredService) as it may not be available depending on timing.
    /// This method is exception-safe and will never throw.
    /// </summary>
    /// <param name="buildEvent">Callback that builds the event. Returns the builder (don't call .Send() - the helper does that automatically).</param>
    /// <param name="operationName">Name for telemetry tracking (e.g., "CreateProject")</param>
    /// <param name="request">Optional HttpRequest for logging the page URL</param>
    /// <param name="telemetryProperties">Optional properties to include in telemetry if an exception occurs</param>
    public static void SendEvent(
      Func<IIntercomEventService, dynamic> buildEvent,
      string operationName,
      string requestRawUrl = null,
      Dictionary<string, object> telemetryProperties = null) {

      if (ConfigHelper.IsDevServer) return;

      try {
        var intercom = ServiceLocator.Instance.GetRequiredService<IIntercomEventService>();
        dynamic builder = buildEvent(intercom);
        builder?.Send();
      } catch (Exception ex) {
        // Telemetry service might not be available yet depending on timing
        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        if (telemetry != null) {
          var telemetryBuilder = telemetry.Exception(ex)
            .WithOperation(operationName);

          if (requestRawUrl != null) {
            telemetryBuilder.WithPageUrl(requestRawUrl);
          }

          if (telemetryProperties != null) {
            foreach (var prop in telemetryProperties) {
              telemetryBuilder.WithProperty(prop.Key, prop.Value);
            }
          }

          telemetryBuilder.Track();
        } else {
          // Fallback to debug logging if telemetry service is not available
          LogHelper.DebugWrite($"Failed to send Intercom event '{operationName}': {ex.Message}");
        }
      }
    }

    /// <summary>
    /// Convenience overload that automatically captures Request from current Http Context.
    /// </summary>
    public static void SendEvent(
      Func<IIntercomEventService, dynamic> buildEvent,
      string operationName,
      Dictionary<string, object> telemetryProperties) {

      if (ConfigHelper.IsDevServer) return;

      SendEvent(buildEvent, operationName, SystemWeb.RequestRawUrl, telemetryProperties);
    }

    /// <summary>
    /// Convenience overload with no telemetry properties.
    /// </summary>
    public static void SendEvent(
      Func<IIntercomEventService, dynamic> buildEvent,
      string operationName) {

      if (ConfigHelper.IsDevServer) return;

      SendEvent(buildEvent, operationName, SystemWeb.RequestRawUrl, null);
    }

    // Reserved Intercom JWT payload keys (cannot be overridden by additional payload)
    private static readonly IList<string> IntercomReservedPayloadKeys = new List<string>() {
      IntercomMetadataConstants.JwtUserId,
      IntercomMetadataConstants.JwtEmail,
      IntercomMetadataConstants.JwtName,
      IntercomMetadataConstants.JwtCompanies
    };

    /// <summary>
    /// Creates an Intercom companies JSON array from user's client company and organization information.
    /// Returns null if user has neither a client company nor an organization.
    /// </summary>
    private static string CreateCompaniesJson(int? clientCompanyId, string clientCompanyName, int orgId, string orgName) {
      var companies = new List<object>();

      // Add client company if it exists
      if (clientCompanyId.HasValue && clientCompanyId.Value > 0) {
        companies.Add(new {
          type = "company",
          id = $"{clientCompanyId.Value}",
          name = clientCompanyName ?? "",
          created_at = 0
        });
      }

      // Add organization if it exists
      if (orgId > 0) {
        companies.Add(new {
          type = "company",
          id = $"{orgId}",
          name = orgName ?? "",
          created_at = 0
        });
      }

      // Return null if no companies to add
      if (companies.Count == 0) {
        return null;
      }

      return JsonConvert.SerializeObject(companies);
    }

    /// <summary>
    /// Gets the plan type string for a learner based on their subscription.
    /// Maps: FoundationFree -> "free", Embedding -> "embed", AI_Coaching -> "ai_coach", AnalyticsPro -> "analytics_pro"
    /// </summary>
    public static string GetLearnerPlanType(this DbHelper.AbleUser.AbleUserInfo userInfo) {
      if (userInfo?.UserSubscription == null) {
        return null;
      }

      switch (userInfo.UserSubscription.SubscriptionId) {
        case ConfigHelper.SubscriptionId.FoundationFree:
          return "free";
        case ConfigHelper.SubscriptionId.Embedding:
          return "embed";
        case ConfigHelper.SubscriptionId.AI_Coaching:
          return "ai_coach";
        case ConfigHelper.SubscriptionId.AnalyticsPro:
          return "analytics_pro";
        default:
          return null;
      }
    }

    /// <summary>
    /// Create an Intercom JWT for frontend identity verification from user info.
    /// Used for authenticating users with the Intercom JavaScript widget.
    /// </summary>
    /// <param name="userInfo">User information to include in the JWT. If null, returns empty string.</param>
    /// <param name="additionalPayload">Optional additional claims to include in the JWT. Cannot override reserved keys (user_id, email, name, companies).</param>
    /// <returns>Signed JWT string for Intercom frontend, or empty string if user is null or Intercom is not configured</returns>
    public static string CreateUserJwt(DbHelper.AbleUser.AbleUserInfo userInfo, Dictionary<string, string> additionalPayload = null) {

      var intercomJwtService = ServiceLocator.Instance.GetService<IIntercomJwtService>();
      if (intercomJwtService == null || !intercomJwtService.IsConfigured || userInfo == null) {
        return string.Empty;
      }

      if (additionalPayload != null && additionalPayload.Keys.Any(x => IntercomReservedPayloadKeys.Contains(x))) {
        throw new ArgumentException($"Cannot override reserved intercom payload keys [{string.Join(",", IntercomReservedPayloadKeys)}]", nameof(additionalPayload));
      }

      // Determine user profile type and create ExternalIntegrationUserId using session role
      var profileType = userInfo.GetUserProfileType();

      // Build JWT payload with user information
      // Values that are reserved by Intercom are here: https://developers.intercom.com/installing-intercom/web/attributes-objects
      // For custom values to work, they will need to be registered here: https://app.intercom.com/a/apps/yunr84s6/settings/data/people?tab=attributes
      var jwtPayload = new Dictionary<string, string> {
        [IntercomMetadataConstants.JwtUserId] = SessionHelper.AsExternalUserId(),
        [IntercomMetadataConstants.JwtInternalId] = $"{userInfo.UserId}",
        [IntercomMetadataConstants.JwtEmail] = userInfo.EmailAddress,
        [IntercomMetadataConstants.JwtName] = userInfo.GetFullName(),
        [IntercomMetadataConstants.JwtIanaTimeZone] = userInfo.TimeZoneIdIana,
        [IntercomMetadataConstants.JwtOrgId] = $"{userInfo.OrgId}",
        [IntercomMetadataConstants.JwtUserRole] = SessionHelper.GetUserRoleDisplayName(SessionHelper.GetUserRole()),
        [IntercomMetadataConstants.JwtEnvironment] = ConfigHelper.EnvironmentType ?? "Unknown"
      };

      // Add companies array with both client company and organization
      var companiesJson = CreateCompaniesJson(userInfo.ClientCompanyId, userInfo.ClientCompanyName, userInfo.OrgId, userInfo.OrgName);
      if (!string.IsNullOrEmpty(companiesJson)) {
        jwtPayload[IntercomMetadataConstants.JwtCompanies] = companiesJson;
      }

      // Add plan type for learners
      if (profileType == DbHelper.AbleUser.UserProfileType.Leader) {
        var planType = userInfo.GetLearnerPlanType();
        if (!string.IsNullOrEmpty(planType)) {
          jwtPayload[IntercomMetadataConstants.JwtPlanType] = planType;
        }
      }

      // Merge in additional payload if provided
      if (additionalPayload != null) {
        foreach (var val in additionalPayload) {
          jwtPayload[val.Key] = val.Value;
        }
      }

      return intercomJwtService.CreateJwt(jwtPayload);
    }
  }
}
