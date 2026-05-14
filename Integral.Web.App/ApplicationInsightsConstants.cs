using System;

namespace Integral.Web {
  /// <summary>
  /// Constants for ApplicationInsights custom property names to avoid magic strings
  /// </summary>
  public static class ApplicationInsightsConstants {

    // ============================================
    // Core System Properties
    // ============================================

    /// <summary>Environment name (Production, Staging, Development, etc.)</summary>
    public const string Environment = "Environment";

    /// <summary>Additional context about the operation being performed</summary>
    public const string OperationContext = "OperationContext";

    /// <summary>Generic message text</summary>
    public const string Message = "Message";

    // ============================================
    // HTTP / Request Properties
    // ============================================

    /// <summary>URL being accessed or processed</summary>
    public const string Url = "Url";

    /// <summary>Page URL</summary>
    public const string PageUrl = "PageUrl";

    /// <summary>Error URL (for JavaScript errors)</summary>
    public const string ErrorUrl = "ErrorUrl";

    /// <summary>HTTP method (GET, POST, etc.)</summary>
    public const string HttpMethod = "HttpMethod";

    /// <summary>HTTP referrer</summary>
    public const string Referrer = "Referrer";

    /// <summary>Query string key being searched for</summary>
    public const string FindKey = "FindKey";

    /// <summary>Query string value found</summary>
    public const string QueryStringValue = "QueryStringValue";

    /// <summary>Full query string</summary>
    public const string QueryString = "QueryString";

    /// <summary>Browser User Agent string</summary>
    public const string BrowserUserAgent = "BrowserUserAgent";

    /// <summary>User Agent from HTTP request</summary>
    public const string RequestUserAgent = "Request.UserAgent";

    // ============================================
    // JavaScript Error Properties
    // ============================================

    /// <summary>JavaScript stack trace</summary>
    public const string JavaScriptStackTrace = "JavaScriptStackTrace";

    /// <summary>Flag indicating if error is from JavaScript</summary>
    public const string IsJavaScriptError = "IsJavaScriptError";

    /// <summary>Line number where error occurred</summary>
    public const string ErrorLine = "ErrorLine";

    /// <summary>Column number where error occurred</summary>
    public const string ErrorColumn = "ErrorColumn";

    // ============================================
    // User & Organization Properties
    // ============================================

    /// <summary>Internal user ID (database primary key)</summary>
    public const string InternalUserId = "InternalUserId";

    /// <summary>External user ID (used for integrations like Intercom, Amplitude, Application Insights)</summary>
    public const string ExternalUserId = "ExternalUserId";

    /// <summary>User ID (generic/legacy)</summary>
    public const string UserId = "UserId";

    /// <summary>User email address</summary>
    public const string UserEmail = "UserEmail";

    /// <summary>User role</summary>
    public const string UserRole = "UserRole";

    /// <summary>Organization ID</summary>
    public const string OrgId = "OrgId";

    /// <summary>Organization ID (alternative form)</summary>
    public const string OrganisationId = "OrganisationId";

    /// <summary>Organization name</summary>
    public const string OrganisationName = "OrganisationName";

    /// <summary>First name</summary>
    public const string FirstName = "FirstName";

    /// <summary>Last name</summary>
    public const string LastName = "LastName";

    /// <summary>Email address</summary>
    public const string Email = "Email";

    /// <summary>Email address (alternative form)</summary>
    public const string EmailAddress = "EmailAddress";

    /// <summary>Recipient email address</summary>
    public const string RecipientEmail = "RecipientEmail";

    /// <summary>Rater email address</summary>
    public const string RaterEmail = "RaterEmail";

    /// <summary>Rater name</summary>
    public const string RaterName = "RaterName";

    // ============================================
    // Coachee Properties
    // ============================================

    /// <summary>Coachee ID</summary>
    public const string CoacheeId = "CoacheeId";

    /// <summary>Coachee email address</summary>
    public const string CoacheeEmail = "CoacheeEmail";

    /// <summary>Coachee first name</summary>
    public const string CoacheeFirstName = "CoacheeFirstName";

    /// <summary>Coachee last name</summary>
    public const string CoacheeLastName = "CoacheeLastName";

    /// <summary>Whether nudges are disabled for coachee</summary>
    public const string DisableNudges = "DisableNudges";

    /// <summary>Whether pulse survey is enabled for coachee</summary>
    public const string PulseSurveyEnabled = "PulseSurveyEnabled";

    // ============================================
    // Coach Properties
    // ============================================

    /// <summary>Coach user ID</summary>
    public const string CoachId = "CoachUserId";

    /// <summary>Admin user ID</summary>
    public const string AdminId = "AdminId";

    /// <summary>Client user ID</summary>
    public const string ClientId = "ClientId";

    /// <summary>Partner bio ID</summary>
    public const string PartnerBioId = "PartnerBioId";

    /// <summary>Whether user is a practitioner</summary>
    public const string IsPractitioner = "IsPractitioner";

    /// <summary>Whether user is a client</summary>
    public const string IsClient = "IsClient";

    /// <summary>Whether user is a participant</summary>
    public const string IsParticipant = "IsParticipant";

    /// <summary>Contact user ID</summary>
    public const string ContactUserId = "ContactUserId";

    // ============================================
    // Invitation / Invitee Properties
    // ============================================

    /// <summary>Email of person being invited</summary>
    public const string InviteeEmail = "InviteeEmail";

    /// <summary>First name of person being invited</summary>
    public const string InviteeFirstName = "InviteeFirstName";

    /// <summary>Last name of person being invited</summary>
    public const string InviteeLastName = "InviteeLastName";

    /// <summary>User ID of person doing the inviting</summary>
    public const string InvitedByUserId = "InvitedByUserId";

    /// <summary>Whether invitee has an invite code</summary>
    public const string HasInviteCode = "HasInviteCode";

    /// <summary>Intake code</summary>
    public const string IntakeCode = "IntakeCode";

    /// <summary>Whether user was invited</summary>
    public const string IsInvited = "IsInvited";

    // ============================================
    // Program Properties
    // ============================================

    /// <summary>Program job ID</summary>
    public const string ProgramJobId = "ProgramJobId";

    /// <summary>Program job number</summary>
    public const string ProgramJobNumber = "ProgramJobNumber";

    /// <summary>Program job name</summary>
    public const string ProgramJobName = "ProgramJobName";

    /// <summary>Program content ID</summary>
    public const string ProgramContentId = "ProgramContentId";

    /// <summary>Program status ID</summary>
    public const string ProgramStatusId = "ProgramStatusId";

    // ============================================
    // Project Properties
    // ============================================

    /// <summary>Project ID</summary>
    public const string ProjectId = "ProjectId";

    // ============================================
    // Company Properties
    // ============================================

    /// <summary>Company ID</summary>
    public const string CompanyId = "CompanyId";

    /// <summary>Company name</summary>
    public const string CompanyName = "CompanyName";

    /// <summary>Department ID</summary>
    public const string DeptId = "DeptId";

    /// <summary>Department name</summary>
    public const string DeptName = "DeptName";

    // ============================================
    // Content Properties
    // ============================================

    /// <summary>Content title</summary>
    public const string ContentTitle = "ContentTitle";

    /// <summary>Content ID</summary>
    public const string ContentId = "ContentId";

    /// <summary>Nudge content ID</summary>
    public const string NudgeContentId = "NudgeContentId";

    /// <summary>Module ID</summary>
    public const string ModuleId = "ModuleId";

    // ============================================
    // Survey Properties
    // ============================================

    /// <summary>Survey ID</summary>
    public const string SurveyId = "SurveyId";

    /// <summary>Survey unique ID</summary>
    public const string SurveyUniqueId = "SurveyUniqueId";

    /// <summary>Survey UID</summary>
    public const string SurveyUID = "SurveyUID";

    /// <summary>New survey ID</summary>
    public const string NewSurveyId = "NewSurveyId";

    /// <summary>Intake number</summary>
    public const string IntakeNumber = "IntakeNumber";

    // ============================================
    // Participant Properties
    // ============================================

    /// <summary>Participant ID</summary>
    public const string ParticipantId = "ParticipantId";

    /// <summary>Participant ID (short form)</summary>
    public const string PartId = "PartId";

    /// <summary>Participant UID</summary>
    public const string PartUID = "PartUID";

    /// <summary>Participant name</summary>
    public const string PartName = "PartName";

    /// <summary>Participant email</summary>
    public const string PartEmail = "PartEmail";

    /// <summary>Participant this rater is rating</summary>
    public const string RaterForParticipantId = "RaterForParticipantId";

    /// <summary>When participant invitation bounced (UTC)</summary>
    public const string InvitationBouncedUTC = "InvitationBouncedUTC";

    /// <summary>Whether bounce was an auto-reply</summary>
    public const string IsBounceAutoReply = "IsBounceAutoReply";

    // ============================================
    // Subscription Properties
    // ============================================

    /// <summary>Subscription ID</summary>
    public const string SubscriptionId = "SubscriptionId";

    /// <summary>User subscription ID</summary>
    public const string UserSubscriptionId = "UserSubscriptionId";

    /// <summary>Whether user has AI coach enabled</summary>
    public const string HasAICoachEnabled = "HasAICoachEnabled";

    /// <summary>Existing subscription ID (for updates)</summary>
    public const string ExistingSubscriptionId = "ExistingSubscriptionId";

    /// <summary>New subscription ID (for updates)</summary>
    public const string NewSubscriptionId = "NewSubscriptionId";

    // ============================================
    // Product / Purchase Properties
    // ============================================

    /// <summary>Product ID</summary>
    public const string ProductId = "ProductId";

    /// <summary>Product description</summary>
    public const string ProductDescription = "ProductDescription";

    /// <summary>Unit price</summary>
    public const string UnitPrice = "UnitPrice";

    /// <summary>Purchase data length</summary>
    public const string PurchaseDataLength = "PurchaseDataLength";

    /// <summary>Panel type</summary>
    public const string PanelType = "PanelType";

    /// <summary>Session quantity</summary>
    public const string SessionQuantity = "SessionQuantity";

    // ============================================
    // Quote Properties
    // ============================================

    /// <summary>Quote item ID</summary>
    public const string QuoteItemId = "QuoteItemId";

    // ============================================
    // Workshop Properties
    // ============================================

    /// <summary>Workshop title</summary>
    public const string WorkshopTitle = "WorkshopTitle";

    // ============================================
    // Session Properties
    // ============================================

    /// <summary>Session value from session storage</summary>
    public const string SessionValue = "SessionValue";

    /// <summary>Session GUID</summary>
    public const string SessionGuid = "SessionGuid";

    /// <summary>Session settings JSON</summary>
    public const string SettingsJson = "SettingsJson";

    /// <summary>Number of sessions allocated</summary>
    public const string SessionsAllocated = "SessionsAllocated";

    /// <summary>Whether this is a new session</summary>
    public const string IsNewSession = "IsNewSession";

    /// <summary>Session ID</summary>
    public const string SessionId = "SessionId";

    /// <summary>Session type slug</summary>
    public const string SessionTypeSlug = "SessionTypeSlug";

    /// <summary>Duration in minutes</summary>
    public const string DurationMins = "DurationMins";

    /// <summary>Event location</summary>
    public const string EventLocation = "EventLocation";

    /// <summary>New start time for rescheduling</summary>
    public const string NewStartTime = "NewStartTime";

    // ============================================
    // Component Properties
    // ============================================

    /// <summary>Number of components</summary>
    public const string ComponentCount = "ComponentCount";

    // ============================================
    // Email Properties
    // ============================================

    /// <summary>Email subject</summary>
    public const string Subject = "Subject";

    /// <summary>Number of email recipients</summary>
    public const string ToAddressCount = "ToAddressCount";

    /// <summary>SMTP server used</summary>
    public const string SMTPServer = "SMTPServer";

    // ============================================
    // File Properties
    // ============================================

    /// <summary>File name</summary>
    public const string FileName = "FileName";

    /// <summary>File path</summary>
    public const string FilePath = "FilePath";

    // ============================================
    // Payload / Webhook Properties
    // ============================================

    /// <summary>Payload ID</summary>
    public const string PayloadId = "PayloadId";

    /// <summary>Body JSON length</summary>
    public const string BodyJsonLength = "BodyJsonLength";

    // ============================================
    // Database Properties
    // ============================================

    /// <summary>SQL command text</summary>
    public const string CommandText = "CommandText";

    /// <summary>Database table name</summary>
    public const string TableName = "TableName";

    /// <summary>Answer type ID</summary>
    public const string AnswerTypeId = "AnswerTypeId";

    /// <summary>Global code ID</summary>
    public const string GblCodeId = "GblCodeId";

    /// <summary>Code value</summary>
    public const string Code = "Code";

    /// <summary>Generic value</summary>
    public const string Value = "Value";

    /// <summary>Unique ID</summary>
    public const string UniqueId = "UniqueId";

    /// <summary>Description</summary>
    public const string Description = "Description";

    // ============================================
    // Entity Properties
    // ============================================

    /// <summary>Entity ID</summary>
    public const string EntityId = "EntityId";

    /// <summary>Entity type</summary>
    public const string EntityType = "EntityType";

    // ============================================
    // Error / Retry Properties
    // ============================================

    /// <summary>Attempt number for retries</summary>
    public const string AttemptNumber = "AttemptNumber";

    /// <summary>Number of tries/attempts</summary>
    public const string Tries = "Tries";

    /// <summary>Retry count</summary>
    public const string RetryCount = "RetryCount";

    /// <summary>Whether error is a duplicate key error</summary>
    public const string IsDuplicateKey = "IsDuplicateKey";

    /// <summary>Whether error is a unique key constraint error</summary>
    public const string IsUniqueKeyError = "IsUniqueKeyError";

    /// <summary>Original exception message</summary>
    public const string OriginalException = "OriginalException";

    // ============================================
    // Contract & Coach Properties
    // ============================================

    /// <summary>Contract type</summary>
    public const string ContractType = "ContractType";

    /// <summary>Coach email address</summary>
    public const string CoachEmail = "CoachEmail";

    /// <summary>Whether coach profile is hidden</summary>
    public const string HideProfile = "HideProfile";

    /// <summary>IANA time zone ID</summary>
    public const string TimeZoneIdIana = "TimeZoneIdIana";

    /// <summary>Number of tags</summary>
    public const string TagCount = "TagCount";

    // ============================================
    // Quote Properties (Extended)
    // ============================================

    /// <summary>Quote ID</summary>
    public const string QuoteId = "QuoteId";

    /// <summary>Quote GUID</summary>
    public const string QuoteGuid = "QuoteGuid";

    /// <summary>Quote title</summary>
    public const string QuoteTitle = "QuoteTitle";

    // ============================================
    // Project Properties (Extended)
    // ============================================

    /// <summary>Job number</summary>
    public const string JobNumber = "JobNumber";

    // ============================================
    // System/Application Properties
    // ============================================

    /// <summary>Whether application is terminating</summary>
    public const string IsTerminating = "IsTerminating";

    /// <summary>Source of the exception</summary>
    public const string ExceptionSource = "ExceptionSource";

    /// <summary>Whether exception was observed</summary>
    public const string Observed = "Observed";

    // ============================================
    // Xero / Financial Properties
    // ============================================

    /// <summary>Purchase order number</summary>
    public const string PONumber = "PONumber";

    /// <summary>Purchase order name</summary>
    public const string POName = "POName";

    /// <summary>Xero purchase order ID</summary>
    public const string XeroPurchaseOrderId = "XeroPurchaseOrderId";

    /// <summary>Item ID</summary>
    public const string ItemId = "ItemId";

    /// <summary>Item count</summary>
    public const string ItemCount = "ItemCount";

  }
}
