using Integral.Integrations.Intercom;

namespace Integral.Web {

  /// <summary>
  /// Extension methods for Intercom event builders to simplify adding user information.
  /// </summary>
  public static class IntercomEventBuilderExtensions {

    /// <summary>
    /// Automatically populates all available session details from SessionHelper.
    /// Adds: UserId, Email, UserProfileType, SubscriptionPlan, AdminStatus.
    /// If no user is logged in, this method does nothing.
    /// Use this when working with the current authenticated session user.
    /// </summary>
    /// <param name="builder">The event builder</param>
    /// <returns>The builder for method chaining</returns>
    public static TBuilder FromSession<TBuilder>(this TBuilder builder)
      where TBuilder : BaseEventBuilder<TBuilder> {

      var userInfo = SessionHelper.GetUserInfoOrNull();
      if (userInfo == null) return builder;

      return builder
        .WithUser(SessionHelper.UserInfo.UserId, SessionHelper.AsExternalUserId())
        .WithEmail(userInfo.EmailAddress)
        .WithUserProfileType(userInfo.IsAbleCoach, userInfo.IsAbleClient, userInfo.IsParticipant, userInfo.IsAbleAdmin)
        .WithSubscriptionPlan(userInfo.UserSubscription?.SubscriptionName)
        .WithAdminStatus(userInfo.IsAbleAdmin, userInfo.IsTenantOrgAdmin);
    }
  }
}
