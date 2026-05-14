using System.Collections.Generic;
using Integral.Integrations.Intercom.Builders;

namespace Integral.Integrations.Intercom {
  /// <summary>
  /// Service for creating JWT tokens for Intercom frontend authentication
  /// Used by the Intercom JavaScript widget for identity verification
  /// </summary>
  public interface IIntercomJwtService {
    /// <summary>
    /// Create a signed JWT token for authenticating users with Intercom frontend
    /// </summary>
    /// <param name="claims">Claims to include in the JWT payload</param>
    /// <returns>Signed JWT string</returns>
    string CreateJwt(Dictionary<string, string> claims);

    /// <summary>
    /// Returns true if Intercom JWT service is configured and enabled
    /// </summary>
    bool IsConfigured { get; }

    void Shutdown();
  }

  /// <summary>
  /// Service for tracking events to Intercom backend
  /// Provides factory methods for creating event builders
  /// Note: Returns concrete builder types to support fluent API pattern
  /// </summary>
  public interface IIntercomEventService {
    SubscriptionUpdatedBuilder SubscriptionUpdated();
    SubscriptionAssignedBuilder SubscriptionAssigned();
    UserSignedUpBuilder UserSignedUp();
    CalendlyUrlUpdatedBuilder CalendlyUrlUpdated();
    CoacheeInvitedBuilder CoacheeInvited();
    CoachingSessionCreatedBuilder CoachingSessionCreated();
    CoachingSessionDeletedBuilder CoachingSessionDeleted();
    CoachingSessionUpdatedBuilder CoachingSessionUpdated();
    SurveyCreatedBuilder SurveyCreated();
    SurveySharedBuilder SurveyShared();
    QuoteCreatedBuilder QuoteCreated();
    QuoteUpdatedBuilder QuoteUpdated();
    QuoteAcceptedBuilder QuoteAccepted();
    TeamMemberInvitedBuilder TeamMemberInvited();
    ParticipantCreatedBuilder ParticipantCreated();
    ContractCompletedBuilder ContractCompleted();
    OrientationCompletedBuilder OrientationCompleted();
    ProfileCompletedBuilder ProfileCompleted();
    ProfilePhotoUploadedBuilder ProfilePhotoUploaded();
    MicrolearningCreatedBuilder MicrolearningCreated();
    ProgramStatusChangedBuilder ProgramStatusChanged();
    ProjectCreatedBuilder ProjectCreated();
    WorkshopCreatedBuilder WorkshopCreated();

    void Shutdown();
  }

}
