using Integral.Integrations.Intercom.Builders;
using Integral.Web;
using Integral.Web.Services;
using Intercom.Data;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace Integral.Integrations.Intercom {
  /// <summary>
  /// Interface for JWT signing functionality
  /// </summary>

  /// <summary>
  /// Intercom metadata keys (internal so they can be used by related classes in this assembly)
  /// </summary>
  internal static class IntercomMetadata {
    internal const string MetadataKey_UserProfileType = "user_profile_type";
    internal const string MetadataKey_SubscriptionPlan = "subscription_plan";
    internal const string MetadataKey_IsAbleAdmin = "is_able_admin";
    internal const string MetadataKey_IsOrgAdmin = "is_org_admin";
    internal const string MetadataKey_Environment = "environment";

    // Common metadata values
    internal const string MetadataValue_None = "none";
    internal const string MetadataValue_True = "true";
    internal const string MetadataValue_False = "false";
  }

  /// <summary>
  /// Implementation of JWT service for Intercom frontend authentication
  /// </summary>
  public class IntercomJwtService : IIntercomJwtService {
    private const int MinimumSecretKeyLength = 32;

    private readonly string _secretKey;
    private readonly int _expirationMinutes;

    public bool IsConfigured { get; }

    public IntercomJwtService(string frontendApiKey, int expirationMinutes = 10) {

      try {
        bool hasFrontendKey = !string.IsNullOrWhiteSpace(frontendApiKey);
        IsConfigured = hasFrontendKey;

        if (!IsConfigured) {
          LogHelper.WriteStartupLogLine("Intercom JWT service disabled - missing frontend API key");
          _secretKey = null;
          _expirationMinutes = 0;
          return;
        }

        // Validate secret key length
        if (frontendApiKey.Length < MinimumSecretKeyLength) {
          throw new ArgumentException($"Frontend API key must be at least {MinimumSecretKeyLength} characters long",
            nameof(frontendApiKey));
        }

        // Validate expiration minutes
        if (expirationMinutes <= 0) {
          throw new ArgumentException("Expiration minutes must be > 0", nameof(expirationMinutes));
        }

        _secretKey = frontendApiKey;
        _expirationMinutes = expirationMinutes;

        LogHelper.WriteStartupLogLine("Intercom JWT service initialized");

      } catch (Exception ex) {

        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("IntercomJwtService_Constructor")
          .Track();

        IsConfigured = false;
        _secretKey = null;
        _expirationMinutes = 0;
      }
    }

    public string CreateJwt(Dictionary<string, string> claims) {

      if (!IsConfigured || string.IsNullOrEmpty(_secretKey)) {
        return string.Empty;
      }

      var tokenHandler = new JwtSecurityTokenHandler();
      var key = Encoding.UTF8.GetBytes(_secretKey);

      var claimsList = claims
        .Where(x => !string.IsNullOrEmpty(x.Key) && !string.IsNullOrEmpty(x.Value))
        .Select(kvp => new Claim(kvp.Key, kvp.Value)).ToList();

      var tokenDescriptor = new SecurityTokenDescriptor {
        Subject = new ClaimsIdentity(claimsList),
        Expires = DateTimeOffset.UtcNow.AddMinutes(_expirationMinutes).DateTime,
        SigningCredentials = new SigningCredentials(
          new SymmetricSecurityKey(key),
          SecurityAlgorithms.HmacSha256Signature)
      };

      // Create and sign token
      var token = tokenHandler.CreateToken(tokenDescriptor);
      return tokenHandler.WriteToken(token);
    }

    public void Shutdown() {
      // no-op
    }
  }

  /// <summary>
  /// Implementation of event service for Intercom backend event tracking
  /// </summary>
  public class IntercomEventService : IIntercomEventService {
    // Intercom event name constants
    private const string EventName_SubscriptionUpdated = "subscription_updated";
    private const string EventName_SubscriptionAssigned = "subscription_assigned";
    private const string EventName_UserSignedUp = "user_signed_up";
    private const string EventName_CalendlyUrlUpdated = "calendly_url_updated";
    private const string EventName_CoacheeInvited = "coachee_invited";
    private const string EventName_CoachingSessionCreated = "coaching_session_created";
    private const string EventName_CoachingSessionDeleted = "coaching_session_deleted";
    private const string EventName_CoachingSessionUpdated = "coaching_session_updated";
    private const string EventName_SurveyCreated = "survey_created";
    private const string EventName_SurveyShared = "survey_shared";
    private const string EventName_QuoteCreated = "quote_created";
    private const string EventName_QuoteUpdated = "quote_updated";
    private const string EventName_QuoteAccepted = "quote_accepted";
    private const string EventName_TeamMemberInvited = "team_member_invited";
    private const string EventName_ParticipantCreated = "participant_created";
    private const string EventName_ContractCompleted = "contract_completed";
    private const string EventName_OrientationCompleted = "orientation_completed";
    private const string EventName_ProfileCompleted = "profile_completed";
    private const string EventName_ProfilePhotoUploaded = "profile_photo_uploaded";
    private const string EventName_MicrolearningCreated = "microlearning_created";
    private const string EventName_ProgramStatusChanged = "program_status_changed";
    private const string EventName_ProjectCreated = "project_created";
    private const string EventName_WorkshopCreated = "workshop_created";

    private readonly IntercomEventQueue _queue;

    /// <summary>
    /// Create a new Intercom event service with the specified queue
    /// </summary>
    /// <param name="queue">The event queue to use for enqueuing events</param>
    public IntercomEventService(IntercomEventQueue queue) {
      if (queue == null) {
        throw new ArgumentNullException(nameof(queue));
      }

      _queue = queue;

      LogHelper.WriteStartupLogLine("Intercom event service initialized with injected queue");
    }

    public void Shutdown() {
      // no-op
    }

    // Builder creation methods - directly create builders with the queue
    public SubscriptionUpdatedBuilder SubscriptionUpdated() {
      return new SubscriptionUpdatedBuilder(_queue, EventName_SubscriptionUpdated);
    }

    public SubscriptionAssignedBuilder SubscriptionAssigned() {
      return new SubscriptionAssignedBuilder(_queue, EventName_SubscriptionAssigned);
    }

    public UserSignedUpBuilder UserSignedUp() {
      return new UserSignedUpBuilder(_queue, EventName_UserSignedUp);
    }

    public CalendlyUrlUpdatedBuilder CalendlyUrlUpdated() {
      return new CalendlyUrlUpdatedBuilder(_queue, EventName_CalendlyUrlUpdated);
    }

    public CoacheeInvitedBuilder CoacheeInvited() {
      return new CoacheeInvitedBuilder(_queue, EventName_CoacheeInvited);
    }

    public CoachingSessionCreatedBuilder CoachingSessionCreated() {
      return new CoachingSessionCreatedBuilder(_queue, EventName_CoachingSessionCreated);
    }

    public CoachingSessionDeletedBuilder CoachingSessionDeleted() {
      return new CoachingSessionDeletedBuilder(_queue, EventName_CoachingSessionDeleted);
    }

    public CoachingSessionUpdatedBuilder CoachingSessionUpdated() {
      return new CoachingSessionUpdatedBuilder(_queue, EventName_CoachingSessionUpdated);
    }

    public SurveyCreatedBuilder SurveyCreated() {
      return new SurveyCreatedBuilder(_queue, EventName_SurveyCreated);
    }

    public SurveySharedBuilder SurveyShared() {
      return new SurveySharedBuilder(_queue, EventName_SurveyShared);
    }

    public QuoteCreatedBuilder QuoteCreated() {
      return new QuoteCreatedBuilder(_queue, EventName_QuoteCreated);
    }

    public QuoteUpdatedBuilder QuoteUpdated() {
      return new QuoteUpdatedBuilder(_queue, EventName_QuoteUpdated);
    }

    public QuoteAcceptedBuilder QuoteAccepted() {
      return new QuoteAcceptedBuilder(_queue, EventName_QuoteAccepted);
    }

    public TeamMemberInvitedBuilder TeamMemberInvited() {
      return new TeamMemberInvitedBuilder(_queue, EventName_TeamMemberInvited);
    }

    public ParticipantCreatedBuilder ParticipantCreated() {
      return new ParticipantCreatedBuilder(_queue, EventName_ParticipantCreated);
    }

    public ContractCompletedBuilder ContractCompleted() {
      return new ContractCompletedBuilder(_queue, EventName_ContractCompleted);
    }

    public OrientationCompletedBuilder OrientationCompleted() {
      return new OrientationCompletedBuilder(_queue, EventName_OrientationCompleted);
    }

    public ProfileCompletedBuilder ProfileCompleted() {
      return new ProfileCompletedBuilder(_queue, EventName_ProfileCompleted);
    }

    public ProfilePhotoUploadedBuilder ProfilePhotoUploaded() {
      return new ProfilePhotoUploadedBuilder(_queue, EventName_ProfilePhotoUploaded);
    }

    public MicrolearningCreatedBuilder MicrolearningCreated() {
      return new MicrolearningCreatedBuilder(_queue, EventName_MicrolearningCreated);
    }

    public ProgramStatusChangedBuilder ProgramStatusChanged() {
      return new ProgramStatusChangedBuilder(_queue, EventName_ProgramStatusChanged);
    }

    public ProjectCreatedBuilder ProjectCreated() {
      return new ProjectCreatedBuilder(_queue, EventName_ProjectCreated);
    }

    public WorkshopCreatedBuilder WorkshopCreated() {
      return new WorkshopCreatedBuilder(_queue, EventName_WorkshopCreated);
    }
  }

  public class IntercomEvent {
    public string EventName { get; set; }
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public ExternalUserId? UserId { get; set; }

    public int InternalId { get; set; }
    public string Email { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    public Event Build() {
      var ev = new Event {
        event_name = EventName,
        created_at = CreatedAt,
        user_id = UserId?.ToString(),
        email = Email,
        metadata = new Metadata()
      };

      foreach (var kvp in Metadata) {
        if (string.IsNullOrEmpty(kvp.Key) || kvp.Value == null) {
          continue;
        }

        // Add value directly - Intercom SDK's Metadata.Add handles different types
        ev.metadata.Add(kvp.Key, kvp.Value);
      }

      return ev;
    }
  }


  /// <summary>
  /// A no-op queue that discards all events (used when Intercom is not configured)
  /// </summary>
  public class NoOpIntercomEventQueue : IntercomEventQueue {
    public override void Enqueue(IntercomEvent intercomEvent) {
      // No-op: discard the event
    }
  }

  public abstract class BaseEventBuilder<TBuilder> where TBuilder : BaseEventBuilder<TBuilder> {
    protected readonly IntercomEvent Event;
    protected readonly IntercomEventQueue Queue;

    protected BaseEventBuilder(IntercomEventQueue queue, string eventName) {
      Queue = queue; // Allow null for no-op mode
      Event = new IntercomEvent { EventName = eventName };

      // Automatically add environment metadata to all events
      AddMetadata(IntercomMetadata.MetadataKey_Environment, ConfigHelper.EnvironmentType);
    }

    /// <summary>
    /// Set the user ID using ExternalIntegrationUserId value object (format: [Role]-[GUID])
    /// </summary>
    public TBuilder WithUser(int id, ExternalUserId? userId) {
      if (!userId.HasValue) {
        return (TBuilder)this;
      }

      Event.UserId = userId.Value;
      Event.InternalId = id;

      return (TBuilder)this;
    }

    public TBuilder WithEmail(string email) {
      Event.Email = email;
      return (TBuilder)this;
    }

    /// <summary>
    /// Add user profile type based on role flags (admin, provider, client, or leader)
    /// </summary>
    public TBuilder WithUserProfileType(bool isAbleCoach, bool isAbleClient, bool isParticipant,
      bool isAbleAdmin = false) {
      string profileType =
        UserProfileTypeHelper.GetProfileTypeString(isAbleAdmin, isAbleCoach, isAbleClient, isParticipant);
      AddMetadata(IntercomMetadata.MetadataKey_UserProfileType, profileType);
      return (TBuilder)this;
    }

    /// <summary>
    /// Add subscription plan name
    /// </summary>
    public TBuilder WithSubscriptionPlan(string subscriptionName) {
      AddMetadata(IntercomMetadata.MetadataKey_SubscriptionPlan,
        subscriptionName ?? IntercomMetadata.MetadataValue_None);
      return (TBuilder)this;
    }

    /// <summary>
    /// Add admin status metadata
    /// </summary>
    public TBuilder WithAdminStatus(bool isAbleAdmin, bool isOrgAdmin) {
      AddMetadata(IntercomMetadata.MetadataKey_IsAbleAdmin,
        isAbleAdmin ? IntercomMetadata.MetadataValue_True : IntercomMetadata.MetadataValue_False);
      AddMetadata(IntercomMetadata.MetadataKey_IsOrgAdmin,
        isOrgAdmin ? IntercomMetadata.MetadataValue_True : IntercomMetadata.MetadataValue_False);
      return (TBuilder)this;
    }

    /// <summary>
    /// Enqueue the event for background processing with retry logic.
    /// No-op if Intercom is not configured.
    /// This method is exception-safe and will never throw - any errors are logged to ApplicationInsights.
    /// </summary>
    public void Send() {
      // No-op if queue is null (Intercom not configured)
      if (Queue == null) {
        return;
      }

      try {
        Validate();

        // Enqueue event instead of sending directly
        Queue.Enqueue(Event);

        // Log successful enqueue
        LogHelper.DebugWrite(
          $"Intercom event queued: {Event.EventName} (UserId: {Event.UserId ?? "null"}, Email: {Event.Email ?? "null"})");
      } catch (Exception ex) {
        // Log validation or enqueue errors to ApplicationInsights but don't throw
        // This ensures calling code never needs try/catch around Intercom event tracking
        var telemetry = ServiceLocator.Instance.GetService<ITelemetryService>();
        var builder = telemetry?.Exception(ex)
          .WithOperation("Intercom_EventBuilder_Send")
          .WithOperationContext("ValidationOrEnqueueError")
          .WithProperty("EventName", Event?.EventName)
          .WithProperty(ApplicationInsightsConstants.UserEmail, Event?.Email);

        if (builder != null && Event?.UserId != null) {
          builder.AddExternalUserId(ExternalUserKind.CurrentUser, Event.UserId.Value);
        }

        builder?.Track();

        LogHelper.LogError($"Failed to enqueue Intercom event {Event?.EventName}: {ex.Message}");
      }
    }

    protected void ValidateRequiredMetadata(string key) {
      if (!Event.Metadata.ContainsKey(key)) {
        throw new InvalidOperationException($"Required key {key} is not set.");
      }
    }

    protected virtual void Validate() {
      // Intentionally marked as protected to allow child classes to add additional validation if required, at the moment I'm not using
      // it anywhere, but we may want to add it in the future.

      if (string.IsNullOrWhiteSpace(Event.EventName)) {
        throw new InvalidOperationException("Event name is required");
      }

      if (string.IsNullOrWhiteSpace(Event.UserId) && string.IsNullOrWhiteSpace(Event.Email)) {
        throw new InvalidOperationException("Either UserId or Email must be provided");
      }
    }

    protected void AddMetadata(string key, string value) {
      Event.Metadata[key] = value;
    }

    protected void AddMetadata(string key, int value) {
      Event.Metadata[key] = $"{value}";
    }

    /// <summary>
    /// Add a rich link metadata (displays as clickable link in Intercom UI)
    /// </summary>
    protected void AddMetadataRichLink(string key, string url, string displayValue) {
      if (string.IsNullOrEmpty(url)) {
        return;
      }

      Event.Metadata[key] = new global::Intercom.Data.Metadata.RichLink(url, displayValue ?? url);
    }

    /// <summary>
    /// Add a monetary amount metadata (displays as formatted currency in Intercom UI)
    /// Amount should be in cents (e.g., 2999 for $29.99 AUD)
    /// </summary>
    protected void AddMetadataMonetary(string key, int amount, string currency = "aud") {
      Event.Metadata[key] = new global::Intercom.Data.Metadata.MonetaryAmount(amount, currency.ToLowerInvariant());
    }

    /// <summary>
    /// Add a date as ISO-8601 UTC timestamp string metadata (e.g., "2024-01-15T10:30:00Z")
    /// </summary>
    protected void AddMetadataDate(string key, DateTimeOffset date) {
      // Convert to UTC and format as ISO-8601 string
      Event.Metadata[key] = date.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
  }

  /// <summary>
  /// Helper class for user profile type conversions
  /// </summary>
  internal static class UserProfileTypeHelper {
    // User profile type string constants
    private const string ProfileType_Admin = "admin";
    private const string ProfileType_Provider = "provider";
    private const string ProfileType_Client = "client";
    private const string ProfileType_Leader = "leader";
    private const string ProfileType_Unknown = "unknown";

    /// <summary>
    /// Converts user role flags to a profile type string.
    /// Priority order: IsAbleAdmin (Admin) > IsAbleCoach (Provider) > IsAbleClient (Client) > IsParticipant (Leader) > Unknown
    /// </summary>
    public static string GetProfileTypeString(bool isAbleAdmin, bool isAbleCoach, bool isAbleClient,
      bool isParticipant) {
      if (isAbleAdmin) {
        return ProfileType_Admin;
      } else if (isAbleCoach) {
        return ProfileType_Provider;
      } else if (isAbleClient) {
        return ProfileType_Client;
      } else if (isParticipant) {
        return ProfileType_Leader;
      }

      return ProfileType_Unknown;
    }
  }
}
