namespace Integral.Web {
  /// <summary>
  /// Constants for ApplicationInsights custom property names specific to database layer operations.
  /// All property names are prefixed with "db." to distinguish them from application-tier properties.
  /// </summary>
  public static class DalApplicationInsightsConstants {

    // Survey Properties
    public const string SurveyId = "db.SurveyId";
    public const string SurveyUniqueId = "db.SurveyUniqueId";
    public const string SurveyUID = "db.SurveyUID";

    // User & Organization Properties
    public const string UserId = "db.UserId";
    public const string OrgId = "db.OrgId";
    public const string FirstName = "db.FirstName";
    public const string LastName = "db.LastName";
    public const string Email = "db.Email";
    public const string EmailAddress = "db.EmailAddress";

    // User Role Properties
    public const string IsPractitioner = "db.IsPractitioner";
    public const string IsClient = "db.IsClient";
    public const string IsParticipant = "db.IsParticipant";

    // Coachee Properties
    public const string CoacheeId = "db.CoacheeId";
    public const string DisableNudges = "db.DisableNudges";
    public const string PulseSurveyEnabled = "db.PulseSurveyEnabled";

    // Coach Properties
    public const string CoachUserId = "db.CoachUserId";
    public const string PartnerBioId = "db.PartnerBioId";

    // Subscription Properties
    public const string SubscriptionId = "db.SubscriptionId";
    public const string UserSubscriptionId = "db.UserSubscriptionId";
    public const string HasAICoachEnabled = "db.HasAICoachEnabled";
    public const string ExistingSubscriptionId = "db.ExistingSubscriptionId";
    public const string NewSubscriptionId = "db.NewSubscriptionId";

    // Program Properties
    public const string ProgramJobId = "db.ProgramJobId";
    public const string ProgramJobNumber = "db.ProgramJobNumber";
    public const string ProgramJobName = "db.ProgramJobName";
    public const string CompanyId = "db.CompanyId";

    // Invitation / Invitee Properties
    public const string InvitedByUserId = "db.InvitedByUserId";
    public const string InviteeUserId = "db.InviteeUserId";
    public const string InviteeEmail = "db.InviteeEmail";
    public const string InviteeFirstName = "db.InviteeFirstName";
    public const string InviteeLastName = "db.InviteeLastName";
    public const string HasInviteCode = "db.HasInviteCode";
    public const string IntakeCode = "db.IntakeCode";

    // Participant Properties
    public const string PartId = "db.PartId";
    public const string PartName = "db.PartName";
    public const string PartEmail = "db.PartEmail";
    public const string InvitationBouncedUTC = "db.InvitationBouncedUTC";
    public const string IsBounceAutoReply = "db.IsBounceAutoReply";
    public const string RaterForParticipantId = "db.RaterForParticipantId";

    // Workshop Properties
    public const string WorkshopTitle = "db.WorkshopTitle";

    // Database Properties
    public const string CommandText = "db.CommandText";
    public const string TableName = "db.TableName";

    // Error & Retry Properties
    public const string AttemptNumber = "db.AttemptNumber";
    public const string Tries = "db.Tries";
    public const string RetryCount = "db.RetryCount";
    public const string IsDuplicateKey = "db.IsDuplicateKey";
    public const string IsUniqueKeyError = "db.IsUniqueKeyError";
    public const string UniqueId = "db.UniqueId";

    // Answer Type Properties
    public const string AnswerTypeId = "db.AnswerTypeId";
    public const string GblCodeId = "db.GblCodeId";
    public const string Code = "db.Code";
    public const string Value = "db.Value";
  }
}
