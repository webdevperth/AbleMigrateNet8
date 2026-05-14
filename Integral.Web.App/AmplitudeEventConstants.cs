namespace Integral.Web {

  /// <summary>
  /// Constants for Amplitude event names to maintain consistency across the application.
  /// </summary>
  public static class AmplitudeEventConstants {

    /// <summary>
    /// Event fired when a user logs in.
    /// </summary>
    public const string Login = "Login";

    /// <summary>
    /// Event fired when a new user registers.
    /// </summary>
    public const string UserRegistration = "User Registration";

    /// <summary>
    /// Event fired when a coaching session is booked.
    /// </summary>
    public const string SessionBooked = "Session Booked";

    /// <summary>
    /// Event fired when a user interacts with the AI coach chatbot.
    /// </summary>
    public const string AICoachInteraction = "AI Coach Interaction";
  }

  /// <summary>
  /// Constants for Amplitude event property names to maintain consistency across the application.
  /// </summary>
  public static class AmplitudePropertyConstants {

    // Session-related properties
    public const string SessionId = "session_id";
    public const string SessionDate = "session_date";
    public const string DurationMins = "duration_mins";
    public const string Source = "source";

    // User-related properties
    public const string CoachId = "coach_id";
    public const string CoacheeId = "coachee_id";
    public const string UserRole = "user_role";

    // Message-related properties
    public const string MessageId = "message_id";
    public const string MessageLength = "message_length";
    public const string InteractionType = "interaction_type";

    // Registration-related properties
    public const string RegistrationSource = "registration_source";
  }

  /// <summary>
  /// Constants for Amplitude user property names to maintain consistency across the application.
  /// </summary>
  public static class AmplitudeUserPropertyConstants {

    public const string UserName = "user_name";
    public const string FirstName = "first_name";
    public const string LastName = "last_name";
    public const string Email = "email";
    public const string Environment = "environment";
    public const string UserRole = "user_role";
    public const string InternalId = "internal_id";
    public const string OrgId = "org_id";
    public const string OrgName = "org_name";
    public const string ClientCompanyId = "client_company_id";
    public const string ClientCompanyName = "client_company_name";
  }
}
