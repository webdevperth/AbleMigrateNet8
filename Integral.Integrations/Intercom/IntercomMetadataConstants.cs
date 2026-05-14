namespace Integral.Integrations.Intercom {
  /// <summary>
  /// Constants for Intercom event metadata field names to avoid magic strings
  /// </summary>
  public static class IntercomMetadataConstants {
    // ============================================
    // Program Properties
    // ============================================

    /// <summary>Program ID</summary>
    public const string ProgramId = "program_id";

    /// <summary>Program name</summary>
    public const string ProgramName = "program_name";

    // ============================================
    // Company Properties
    // ============================================

    /// <summary>Company ID</summary>
    public const string CompanyId = "company_id";

    /// <summary>Company name</summary>
    public const string CompanyName = "company_name";

    /// <summary>Client company ID</summary>
    public const string ClientCompanyId = "client_company_id";

    /// <summary>Client company name</summary>
    public const string ClientCompanyName = "client_company_name";

    // ============================================
    // Organisation Properties
    // ============================================

    /// <summary>Organisation ID</summary>
    public const string OrganisationId = "organisation_id";

    /// <summary>Organisation name</summary>
    public const string OrganisationName = "organisation_name";

    // ============================================
    // Participant Properties
    // ============================================

    /// <summary>Participant ID</summary>
    public const string ParticipantId = "participant_id";

    /// <summary>Participant email</summary>
    public const string ParticipantEmail = "participant_email";

    /// <summary>Participant name</summary>
    public const string ParticipantName = "participant_name";

    /// <summary>Participant count</summary>
    public const string ParticipantCount = "participant_count";

    // ============================================
    // Workshop Properties
    // ============================================

    /// <summary>Workshop ID</summary>
    public const string WorkshopId = "workshop_id";

    /// <summary>Friendly workshop ID</summary>
    public const string FriendlyWorkshopId = "friendly_workshop_id";

    /// <summary>Workshop title</summary>
    public const string WorkshopTitle = "workshop_title";

    /// <summary>Workshop location</summary>
    public const string Location = "location";

    /// <summary>Whether workshop is virtual</summary>
    public const string IsVirtual = "is_virtual";

    /// <summary>Key facilitator user ID</summary>
    public const string KeyFacilitatorUserId = "key_facilitator_user_id";

    // ============================================
    // Project Properties
    // ============================================

    /// <summary>Project ID</summary>
    public const string ProjectId = "project_id";

    /// <summary>Project name</summary>
    public const string ProjectName = "project_name";

    /// <summary>Project job number</summary>
    public const string ProjectJobNumber = "project_job_number";

    /// <summary>Project type</summary>
    public const string ProjectType = "project_type";

    // ============================================
    // User Properties
    // ============================================

    /// <summary>User role</summary>
    public const string UserRole = "user_role";

    /// <summary>Whether user was invited</summary>
    public const string IsInvited = "is_invited";

    /// <summary>Email address</summary>
    public const string EmailAddress = "email_address";

    // ============================================
    // Invitation Properties
    // ============================================

    /// <summary>Invited user ID</summary>
    public const string InvitedUserId = "invited_user_id";

    /// <summary>Invited user name</summary>
    public const string InvitedName = "invited_name";

    /// <summary>Invited user email</summary>
    public const string InvitedEmail = "invited_email";

    /// <summary>Invited user role</summary>
    public const string InvitedRole = "invited_role";

    // ============================================
    // Profile Properties
    // ============================================

    /// <summary>Profile completion percentage</summary>
    public const string CompletionPercentage = "completion_percentage";

    /// <summary>Profile type</summary>
    public const string ProfileType = "profile_type";

    /// <summary>Photo ID</summary>
    public const string PhotoId = "photo_id";

    /// <summary>Photo (rich link)</summary>
    public const string Photo = "photo";

    // ============================================
    // Survey Properties
    // ============================================

    /// <summary>Survey ID</summary>
    public const string SurveyId = "survey_id";

    /// <summary>Survey title</summary>
    public const string SurveyTitle = "survey_title";

    /// <summary>Survey type</summary>
    public const string SurveyType = "survey_type";

    /// <summary>User ID survey was shared with</summary>
    public const string SharedWithUserId = "shared_with_user_id";

    /// <summary>Email survey was shared with</summary>
    public const string SharedWithEmail = "shared_with_email";

    /// <summary>Name survey was shared with</summary>
    public const string SharedWithName = "shared_with_name";

    // ============================================
    // Coaching Session Properties
    // ============================================

    /// <summary>Coaching session ID</summary>
    public const string SessionId = "session_id";

    /// <summary>Coach user ID</summary>
    public const string CoachUserId = "coach_user_id";

    /// <summary>Coach name</summary>
    public const string CoachName = "coach_name";

    /// <summary>Session start time (UTC)</summary>
    public const string SessionStartUtc = "session_start_utc";

    /// <summary>Session duration in minutes</summary>
    public const string DurationMins = "duration_mins";

    /// <summary>Source of the action</summary>
    public const string Source = "source";

    /// <summary>Old start time</summary>
    public const string OldStartTime = "old_start_time";

    /// <summary>New start time</summary>
    public const string NewStartTime = "new_start_time";

    // ============================================
    // Microlearning Properties
    // ============================================

    /// <summary>Microlearning ID</summary>
    public const string MicrolearningId = "microlearning_id";

    /// <summary>Title</summary>
    public const string Title = "title";

    /// <summary>Category</summary>
    public const string Category = "category";

    /// <summary>Content type</summary>
    public const string ContentType = "content_type";

    // ============================================
    // Orientation Properties
    // ============================================

    /// <summary>Orientation ID</summary>
    public const string OrientationId = "orientation_id";

    /// <summary>Duration in minutes</summary>
    public const string DurationMinutes = "duration_minutes";

    // ============================================
    // Quote Properties
    // ============================================

    /// <summary>Quote ID</summary>
    public const string QuoteId = "quote_id";

    /// <summary>Quote title</summary>
    public const string QuoteTitle = "quote_title";

    /// <summary>Quote total value</summary>
    public const string QuoteValue = "quote_value";

    // ============================================
    // Contract Properties
    // ============================================

    /// <summary>Contract ID</summary>
    public const string ContractId = "contract_id";

    /// <summary>Contract type</summary>
    public const string ContractType = "contract_type";

    // ============================================
    // Calendly Properties
    // ============================================

    /// <summary>Calendly URL name</summary>
    public const string CalendlyUrlName = "calendly_url_name";

    /// <summary>Old Calendly URL name</summary>
    public const string OldCalendlyUrlName = "old_calendly_url_name";

    // ============================================
    // Status Properties
    // ============================================

    /// <summary>Old status</summary>
    public const string OldStatus = "old_status";

    /// <summary>New status</summary>
    public const string NewStatus = "new_status";

    // ============================================
    // Date Properties
    // ============================================

    /// <summary>Start date</summary>
    public const string StartDate = "start_date";

    /// <summary>End date</summary>
    public const string EndDate = "end_date";

    /// <summary>Start date (UTC)</summary>
    public const string StartDateUtc = "start_date_utc";

    /// <summary>Completed date</summary>
    public const string CompletedDate = "completed_date";

    /// <summary>Created date</summary>
    public const string CreatedDate = "created_date";

    /// <summary>Upload date</summary>
    public const string UploadDate = "upload_date";

    // ============================================
    // Financial Properties
    // ============================================

    /// <summary>Revenue (monetary)</summary>
    public const string Revenue = "revenue";

    // ============================================
    // Subscription Properties
    // ============================================

    /// <summary>Subscription type/name</summary>
    public const string SubscriptionType = "subscription_type";

    /// <summary>Subscription quantity purchased</summary>
    public const string SubscriptionQuantity = "subscription_quantity";

    /// <summary>Subscription unit price</summary>
    public const string SubscriptionUnitPrice = "subscription_unit_price";

    /// <summary>Subscription total value (unit_price * quantity)</summary>
    public const string SubscriptionTotalValue = "subscription_total_value";

    // ============================================
    // JWT Payload Keys (Frontend Identity Verification)
    // ============================================

    /// <summary>User ID (JWT payload key)</summary>
    public const string JwtUserId = "user_id";

    public const string JwtInternalId = "internal_id";

    /// <summary>Email (JWT payload key)</summary>
    public const string JwtEmail = "email";

    /// <summary>Name (JWT payload key)</summary>
    public const string JwtName = "name";

    /// <summary>Companies array (JWT payload key)</summary>
    public const string JwtCompanies = "companies";

    /// <summary>IANA timezone (JWT payload key)</summary>
    public const string JwtIanaTimeZone = "iana_timezone";

    /// <summary>Organisation ID (JWT payload key)</summary>
    public const string JwtOrgId = "org_id";

    /// <summary>User profile type (JWT payload key)</summary>
    public const string JwtUserRole = "user_role";

    /// <summary>Plan type (JWT payload key)</summary>
    public const string JwtPlanType = "plan_type";

    /// <summary>Environment (JWT payload key)</summary>
    public const string JwtEnvironment = "environment";

    /// <summary>Value indicating 'none' for optional fields</summary>
    public const string ValueNone = "none";

  }
}
