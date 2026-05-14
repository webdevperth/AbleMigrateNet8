using System;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web {

  public partial class SessionHelper {

    public class AppAccess {

      // Note that "IsUserRoleCoach" will automatically include Tenant Admins as they are also practitioners.

      public enum AccessType {
        ViewPage = 1
      }

      // Common functions used in session helper, not to be used externally:

      internal static bool IsUserAdmin(DbHelper.ClientCompanies.AlbertCompanyInfo company) {
        if (UserInfo == null || company == null) return false;
        return IsUserRoleAdmin
          || (IsUserTenantAdmin(company) && IsUserRoleTenantAdmin);
      }

      internal static bool IsUserAdmin(DbHelper.Content.ContentInfo content) {
        if (UserInfo == null || content == null) return false;
        return IsUserRoleAdmin
          || (IsUserTenantAdmin(content) && IsUserRoleTenantAdmin);
      }

      internal static bool IsUserAdmin(DbHelper.Modules.ModuleInfo module) {
        if (UserInfo == null || module == null) return false;
        return IsUserRoleAdmin
          || (IsUserTenantAdmin(module) && IsUserRoleTenantAdmin);
      }

      internal static bool IsUserAdmin(DbHelper.Projects.ProjectInfoBrief projectInfo) {
        if (UserInfo == null || projectInfo == null) return false;
        return IsUserRoleAdmin
          || (IsUserTenantAdmin(projectInfo) && IsUserRoleTenantAdmin);
      }

      internal static bool IsUserAdmin(DbHelper.AbleQuotes.QuoteInfo quote) {
        if (UserInfo == null || quote == null) return false;
        return IsUserRoleAdmin
          || (IsUserTenantAdmin(quote) && IsUserRoleTenantAdmin);
      }

      internal static bool IsUserAdmin(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
        if (UserInfo == null || programInfo == null) return false;
        return IsUserRoleAdmin
          || (IsUserTenantAdmin(programInfo) && IsUserRoleTenantAdmin);
      }

      internal static bool IsUserAdmin(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {
        if (UserInfo == null || coacheeInfo == null) return false;
        return IsUserRoleAdmin
          || (IsUserTenantAdmin(coacheeInfo) && IsUserRoleTenantAdmin);
      }

      internal static bool IsUserAdmin(DbHelper.AlbertCoachees.CoacheeListItem coacheeListItem) {
        if (UserInfo == null || coacheeListItem == null) return false;
        return IsUserRoleAdmin
          || (IsUserTenantAdmin(coacheeListItem) && IsUserRoleTenantAdmin);
      }

      internal static bool IsUserAdmin(DbHelper.AbleUser.UserIdentity forUser) {
        if (UserInfo == null || forUser == null) return false;
        return IsUserRoleAdmin
          || (IsUserTenantAdmin(forUser) && IsUserRoleTenantAdmin);
      }

      internal static bool IsUserAdminOrProgramManager(DbHelper.Projects.ProjectInfo projectInfo) {
        if (UserInfo == null || projectInfo == null) return false;
        return IsUserAdmin(projectInfo)
          || IsUserPCorPLCInProject(projectInfo.JobNumber);
      }

      internal static bool IsUserAdminOrProgramManager(DbHelper.AbleQuotes.QuoteInfo quote) {
        if (UserInfo == null || quote == null) return false;
        return IsUserAdmin(quote)
          || IsUserPCorPLCInProject(quote.JobNumber);
      }

      internal static bool IsUserAdminOrProgramManager(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
        if (UserInfo == null || programInfo == null) return false;
        return IsUserAdmin(programInfo)
          || IsUserPCorPLCInProject(programInfo.ProgramJobNumber);
      }

      internal static bool IsAdminOrProgramManager(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {
        if (UserInfo == null || coacheeInfo == null) return false;
        return IsUserAdmin(coacheeInfo)
          || IsUserPCorPLCInProject(coacheeInfo.ProgramJobNumber);
      }

      internal static bool IsUserAdminOrProgramManager(DbHelper.AlbertCoachees.CoacheeListItem coacheeListItem) {
        if (UserInfo == null || coacheeListItem == null) return false;
        return IsUserAdmin(coacheeListItem)
          || IsUserPCorPLCInProject(coacheeListItem.ProgramJobNumber);
      }

      internal static bool IsUserCreator(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {
        if (UserInfo == null || companyInfo == null) return false;
        return companyInfo.CreatedByUserId == UserInfo.UserId;
      }

      internal static bool IsUserCreator(DbHelper.Projects.ProjectInfo projectInfo) {
        if (UserInfo == null || projectInfo == null) return false;
        return projectInfo.CreatedByUserId == UserInfo.UserId;
      }

      internal static bool IsUserCreator(DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {
        if (UserInfo == null || surveyInfo == null) return false;
        return surveyInfo.CreatedByUserId == UserInfo.UserId;
      }

      internal static bool IsUserWorkshopFacilitator(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
        if (UserInfo == null || programInfo == null) return false;
        return programInfo.OwnWorkshopCount > 0
          || programInfo.CoFacWorkshopCount > 0;
      }

      internal static bool IsUserCoachInProgram(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
        if (UserInfo == null || programInfo == null) return false;
        return programInfo.ProgramCoachIds.Contains(UserInfo.UserId);
      }

      internal static bool UserHasConsultingItems(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
        if (UserInfo == null || programInfo == null) return false;
        return programInfo.OwnConsultingCount > 0;
      }

      internal static bool IsUserCoachee(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee) {
        if (UserInfo == null || coachee == null) return false;
        return coachee.UserId == UserInfo.UserId;
      }

      internal static bool IsUserCoachForCoachee(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee) {
        if (UserInfo == null || coachee == null) return false;
        return coachee.CoachUserId == UserInfo.UserId;
      }

      internal static int? GetUserId(DbHelper.AbleUser.AbleUserInfo userOrNullForCurrent = null) {
        var user = userOrNullForCurrent ?? GetUserInfoOrNull();
        if (user == null) return null;
        return user.UserId;
      }

      internal static bool IsUserTenantAdmin(DbHelper.ClientCompanies.AlbertCompanyInfo forCompany) {
        if (UserInfo == null || forCompany == null) return false;
        return UserInfo.IsTenantOrgAdmin && UserInfo.OrgId == forCompany.OrgId;
      }

      internal static bool IsUserTenantAdmin(DbHelper.Content.ContentInfo forContent) {
        if (UserInfo == null || forContent == null) return false;
        return UserInfo.IsTenantOrgAdmin && UserInfo.OrgId == forContent.AuthorTenantOrgId;
        // || UserInfo.UserId == forContent.AuthorTenantOrgOwnerUserId - TODO add to SQL for Content.
      }

      internal static bool IsUserTenantAdmin(DbHelper.Modules.ModuleInfo forModule) {
        if (UserInfo == null || forModule == null) return false;
        return UserInfo.IsTenantOrgAdmin && UserInfo.OrgId == forModule.AuthorTenantOrgId;
        // || UserInfo.UserId == forModule.AuthorTenantOrgOwnerUserId - TODO add to SQL for Module.
      }

      internal static bool IsUserTenantAdmin(DbHelper.Projects.ProjectInfoBrief forProject) {
        if (UserInfo == null || forProject == null) return false;
        return (UserInfo.IsTenantOrgAdmin && UserInfo.OrgId == forProject.TenantOrgId) // An admin within the Tenant.
          || UserInfo.UserId == forProject.TenantOrgOwnerUserId; // The Tenant owner.
      }

      internal static bool IsUserTenantAdmin(DbHelper.AblePrograms.AbleProgramInfo forProgram) {
        if (UserInfo == null || forProgram == null) return false;
        return (UserInfo.IsTenantOrgAdmin && UserInfo.OrgId == forProgram.TenantOrgId)
          || UserInfo.UserId == forProgram.TenantOrgOwnerUserId;
      }

      internal static bool IsUserTenantAdmin(DbHelper.AbleQuotes.QuoteInfo forQuote) {
        if (UserInfo == null || forQuote == null) return false;
        return (UserInfo.IsTenantOrgAdmin && UserInfo.OrgId == forQuote.TenantOrgId)
          || UserInfo.UserId == forQuote.TenantOrgOwnerUserId;
      }

      internal static bool IsUserTenantAdmin(DbHelper.AlbertCoachees.AlbertCoacheeInfo forCoachee) {
        if (UserInfo == null || forCoachee == null) return false;
        return (UserInfo.IsTenantOrgAdmin && UserInfo.OrgId == forCoachee.TenantOrgId)
          || UserInfo.UserId == forCoachee.TenantOrgOwnerUserId;
      }

      internal static bool IsUserTenantAdmin(DbHelper.AlbertCoachees.CoacheeListItem forCoacheeListItem) {
        if (UserInfo == null || forCoacheeListItem == null) return false;
        return (UserInfo.IsTenantOrgAdmin && UserInfo.OrgId == forCoacheeListItem.TenantOrgId)
          || UserInfo.UserId == forCoacheeListItem.TenantOrgOwnerUserId;
      }

      internal static bool IsUserTenantAdmin(DbHelper.AbleUser.UserIdentity forUser) {
        if (UserInfo == null || forUser == null) return false;
        return (UserInfo.IsTenantOrgAdmin && UserInfo.OrgId == forUser.OrgId)
          || UserInfo.UserId == forUser.OrgOwnerUserId;
      }

      internal static bool IsUserPC(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
        if (UserInfo == null || programInfo == null) return false;
        return UserInfo.UserId == programInfo.ProjectCoordinatorUserId;
      }

      // TODO: Remove this in favour of checking project object.
      internal static bool IsUserPCorPLCInProject(string projectJobNumber) {
        if (UserInfo == null || projectJobNumber.IsNullOrEmpty()) return false;
        return UserInfo.IsPCorPLCInProject(projectJobNumber);
      }

      // TODO: Remove this in favour of owner userids in project object.
      internal static bool IsUserQuoteOwnerInProject(DbHelper.Projects.ProjectInfo projectInfo) {
        if (UserInfo == null || projectInfo == null) return false;
        return UserInfo.IsQuoteOwnerInProject(projectInfo.JobNumber);
      }

      internal static bool IsUserQuoteOwnerInProject(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
        if (UserInfo == null || programInfo == null) return false;
        return UserInfo.IsQuoteOwnerInProject(programInfo.ProgramJobNumber);
      }

      internal static bool IsUserDeliveryInProject(DbHelper.Projects.ProjectInfo projectInfo) {
        return IsUserDeliveryInProject(projectInfo?.JobNumber);
      }

      internal static bool IsUserDeliveryInProject(string projectJobNumber) {
        if (UserInfo == null || projectJobNumber.IsNullOrEmpty()) return false;
        return UserInfo.IsDeliveryInProject(projectJobNumber);
      }

      internal static bool IsUserDeliveryInProgram(DbHelper.AblePrograms.AbleProgramInfo program) {
        if (UserInfo == null || program == null) return false;
        return IsUserCoachInProgram(program)
          || IsUserWorkshopFacilitator(program);
      }

      internal static bool IsUserInProjectAccess(DbHelper.Projects.ProjectInfo project) {
        if (UserInfo == null || project == null) return false;
        return UserInfo.IsInProjectAccess(project.JobNumber);
      }

      internal static bool IsUserInProjectAccess(DbHelper.AbleQuotes.QuoteInfo quote) {
        if (UserInfo == null || quote == null) return false;
        return UserInfo.IsInProjectAccess(quote.JobNumber);
      }

      internal static bool IsUserInProjectAccess(DbHelper.AblePrograms.AbleProgramInfo program) {
        if (UserInfo == null || program == null) return false;
        return UserInfo.IsInProjectAccess(program.ProgramJobNumber);
      }

      // Action-specific permissions for each app area:

      public class Participants {

        public static bool CanViewNonProfileTabs() => !IsUserRoleClient;

        public static bool CanAdd()
          => IsUserRoleAdmin
          || IsUserRoleCoach;

        public static bool CanSoftDelete(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => (IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)) && coachee.UserActivity.SessionsAllocated == 0;

        public static bool CanHardDelete(int coacheeId, List<DbHelper.ProgramComponents.ComponentInfo> allCoacheeComponents) {

          if (allCoacheeComponents == null) throw new ArgumentException($"{nameof(allCoacheeComponents)} cannot be null.");
          if (allCoacheeComponents.Exists(c => c.CoacheeId != coacheeId)) throw new ArgumentException($"Components in list must be for given coacheeId.");

          // Can't delete if locked components or pay run items are present.
          return !allCoacheeComponents.Exists(c => c.LockedDateUtc != null || c.HasPayrunItems);
        }

        public static bool CanEdit(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)
          || IsUserPCorPLCInProject(coachee?.ProgramJobNumber);

        public static bool LimitedEdit(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserCoachForCoachee(coachee) && !CanEdit(coachee);

        public static bool CanChangeProgramStatus(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)
          || IsUserPCorPLCInProject(coachee?.ProgramJobNumber);

        public static bool CanChangeCoach(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee) {
          return coachee.PrivateCoachNote.IsNullOrEmpty()
            && (IsUserRoleAdmin
            || IsUserTenantAdmin(coachee)
            || IsUserPCorPLCInProject(coachee?.ProgramJobNumber));
        }
        public static bool CanChangeMeetCoachDate(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)
          || IsUserPCorPLCInProject(coachee?.ProgramJobNumber);

        public static bool CanChangeCompany(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)
          || IsUserPCorPLCInProject(coachee?.ProgramJobNumber);

        public static bool CanApplyCoachingToProgram(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)
          || IsUserPCorPLCInProject(coachee?.ProgramJobNumber);

        public static bool CanApplySettingsToProgram(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)
          || IsUserPCorPLCInProject(coachee?.ProgramJobNumber);

        public static bool CanEditParticipantSettings(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)
          || IsUserPCorPLCInProject(coachee?.ProgramJobNumber)
          || IsUserCoachForCoachee(coachee);

        public static bool CanCreateQuoteForNewParticipant() => IsUserIntegral;

        public static bool CanChangeLockedSessionQuoteItem(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee) => false;

        public static bool CanViewSessionOverallRevenue(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdmin(programInfo)
          || IsUserPC(programInfo);
        }

        public static bool CanViewSessionPartnerRevenue(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdmin(programInfo)
          || IsUserPC(programInfo)
          || IsUserCoachInProgram(programInfo);
        }

        public static bool CanEditSessionQuoteItem(DbHelper.AblePrograms.AbleProgramInfo program)
          => IsUserAdminOrProgramManager(program);

        public static bool CanSendMeetCoachEmail(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)
          || IsUserPCorPLCInProject(coachee?.ProgramJobNumber);

        public static bool CanSendEmailToCoachee(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)
          || IsUserPCorPLCInProject(coachee?.ProgramJobNumber)
          || IsUserCoachForCoachee(coachee);

        public static bool CanAttachBookingLinkInEmail(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => coachee.HasCoaching
          && coachee.UserActivity?.SessionsBooked < coachee.UserActivity?.SessionsAllocated;

        public static bool CanAttachBookingLinkInEmail(DbHelper.AlbertCoachees.CoacheesForProgramEmail coachee)
          => coachee.HasCoaching
          && coachee.SessionsBooked < coachee.SessionsAllocated;

        public static bool CanUpdateCoaching(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)
          || IsUserPCorPLCInProject(coachee?.ProgramJobNumber);

        public static bool CanViewParticipantNotes(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)
          || IsUserPCorPLCInProject(coachee?.ProgramJobNumber)
          || IsUserCoachForCoachee(coachee);

        public static bool CanEditParticipantNotes(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserRoleAdmin
          || IsUserTenantAdmin(coachee)
          || IsUserPCorPLCInProject(coachee?.ProgramJobNumber)
          || IsUserCoachForCoachee(coachee);

        public static bool CanViewCoachingNotes(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserCoachForCoachee(coachee) || IsUserTenantAdmin(coachee);

        public static bool CanEditPrivateCoachNote(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserCoachForCoachee(coachee) || IsUserTenantAdmin(coachee);

        public static bool CanEditCoachAI(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserAdmin(coachee)
          && coachee.HasSubscription
          && coachee.UserSubscription.HasAICoaching;

        public static bool CanViewParticipantProfile(DbHelper.AlbertCoachees.CoacheeListItem coacheeListItem) {
          if (IsUserRoleClient) return false;
          return IsUserAdminOrProgramManager(coacheeListItem)
            || coacheeListItem.CoachUserId == UserInfo.UserId;
        }

        public static bool CanViewParticipantProfile(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {
          if (IsUserRoleClient) return false;
          return IsAdminOrProgramManager(coacheeInfo)
            || coacheeInfo.CoachUserId == UserInfo.UserId;
        }

        public static bool CanChangeUserIdIfEmailChanged() => IsUserRoleAdmin;

        public static bool CanUpdateSubscription(DbHelper.AblePrograms.AbleProgramInfo program, DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {
          if (program == null || coacheeInfo == null) return false;
          if (coacheeInfo.HasSubscription) {
            if (coacheeInfo.UserSubscription.HasQuoteAssigned) return false;
          }
          return IsUserAdminOrProgramManager(program) || IsUserInProjectAccess(program);
        }

        public static bool CanBookSession(DbHelper.AblePrograms.AbleProgramInfo program, DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee) {
          if (UserInfo == null || program == null || coachee == null) return false;
          return program.ProgramStatusId == DbHelper.AlbertProgramStatus.Ids.Active
            && coachee.ProgramStatusId <= DbHelper.CoacheeProgramStatus.Ids.Paused
            && coachee.HasCoaching && coachee.IsCoachAssigned;
        }

        public static bool CanViewParticipantSummary(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee) {
          if (UserInfo == null || coachee == null) return false;
          return IsUserRoleAdmin
            || IsUserTenantAdmin(coachee)
            || IsUserPCorPLCInProject(coachee.ProgramJobNumber)
            || IsUserCoachForCoachee(coachee)
            || coachee.UserId == UserInfo.UserId;
        }

        public static bool CanInteractWithAIChat(List<DbHelper.AlbertSurveys.SurveyInfo> openSurveys) {
          // A user cannot interact with AI Coach if they have an intake survey open.
          if (openSurveys == null) return true;
          return !openSurveys.Exists(s => s.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Intake);
        }

        public static bool CanSelfSelectCoach(DbHelper.AbleUser.AbleUserInfo userOrNullForCurrent = null) {
          var user = userOrNullForCurrent ?? GetUserInfoOrNull();
          // User must be in "leader" role and have an active coaching participant with a coach assigned.
          if (IsUserRoleLeader && user?.LatestCoachingInfo != null) {
            if (user.LatestCoachingInfo.CanSelfSelectCoach
              && !user.LatestCoachingInfo.IsCoachAssigned // Coach is not assigned
              && user.LatestCoachingInfo.SessionsAllocated > 0) {
              return true;
            }
          }
          return false;
        }

        public static bool CanViewSurveyDetails(DbHelper.AblePrograms.AbleProgramInfo program, DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee) {
          if (UserInfo == null || coachee == null || program == null) return false;
          return IsUserAdminOrProgramManager(program)
            || IsUserCoachForCoachee(coachee)
            || coachee.UserId == UserInfo.UserId;
        }
      }

      public class Content {

        public static bool CanAddContent()
          => IsUserRoleAdmin
          || IsUserRoleCoach;

        public static bool CanViewContentTopLevelMenuItem() {
          if (UserInfo == null) return false;
          return IsUserRoleAdmin
            || IsUserRoleCoach
            || (IsUserRoleLeader && UserInfo.HasSubscription && UserInfo.UserSubscription.HasMicrolearnings);
        }

        public static bool CanViewContentList()
          => IsUserRoleAdmin
          || IsUserRoleCoach;

        public static bool CanEditContentItem(DbHelper.Content.ContentInfo content) {
          if (UserInfo == null || content == null) return false;
          return IsUserAdmin(content)
            || (IsUserRoleCoach && content.AuthorUserId == UserInfo.UserId);
        }

        public static bool CanDeleteContentItem(DbHelper.Content.ContentInfo content) {
          if (UserInfo == null || content == null) return false;
          if (content.IsLinkedToProgramOrModule || content.IsRespondedQuiz) return false;
          return IsUserAdmin(content)
            || (IsUserRoleCoach && content.AuthorUserId == UserInfo.UserId);
        }

        public static bool CanViewContentItem(DbHelper.Content.ContentInfo contentInfo) {

          if (UserInfo == null || contentInfo == null) return false;

          if (IsUserAdmin(contentInfo)
            || (IsUserRoleCoach && contentInfo.AuthorUserId == UserInfo.UserId)) return true;

          if (IsUserRoleLeader) return contentInfo.IsAccessibleByUser;

          if (contentInfo.IsPublished) {
            // If content is published, anyone can view, with a couple of exceptions.
            if (IsUserRoleCoach && !contentInfo.ShowToPartners) return false;
            if (IsUserRoleLeader && !contentInfo.ShowToParticipants) return false;
            return true;
          }

          return false;
        }

        public static bool CanViewContentItem(DbHelper.Modules.ParticipantModuleInfo participantModuleInfo) {
          if (participantModuleInfo == null) return false;
          return IsUserRoleLeader;
        }

        public static bool CanEditIsPublished(DbHelper.Content.ContentInfo content) {
          if (UserInfo == null || content == null) return false;
          if (content.IsRespondedQuiz) return false;
          return IsUserRoleAdmin
            || content.AuthorUserId == UserInfo.UserId;
        }

        public static bool CanEditQuiz(DbHelper.Content.ContentInfo content) {
          if (UserInfo == null || content == null) return false;
          if (content.ContentType != DbHelper.Content.ContentTypeEnum.Quiz) return false;
          if (content.IsRespondedQuiz) return false;
          return IsUserRoleAdmin
            || content.AuthorUserId == UserInfo.UserId;
        }

        public static bool CanSubmitQuizResponses(DbHelper.Content.ContentInfo content, DbHelper.Content.UserContentInfo userContentInfo) {
          if (UserInfo == null || content == null || userContentInfo == null) return false;
          return IsUserRoleLeader
            && content.IsPublished
            && !userContentInfo.IsCompleted
            && content.ContentType == DbHelper.Content.ContentTypeEnum.Quiz;
        }

        public static bool CanSubmitLearningAction(DbHelper.Content.ContentInfo content, DbHelper.Content.UserContentInfo userContentInfo) {
          if (content == null || userContentInfo == null) return false;
          return IsUserRoleLeader
            && content.IsPublished
            && !userContentInfo.IsCompleted
            && content.ContentType == DbHelper.Content.ContentTypeEnum.LearningActions;
        }

        public static bool CanUpdateCompletedUtc(DbHelper.Content.ContentInfo content, DbHelper.Content.UserContentInfo userContentInfo) {
          if (UserInfo == null || content == null || userContentInfo == null) return false;
          return IsUserRoleLeader
            && content.IsPublished
            && !userContentInfo.IsCompleted
            && (content.ContentType == DbHelper.Content.ContentTypeEnum.Document || content.ContentType == DbHelper.Content.ContentTypeEnum.Image);
        }

        public static bool CanChangeAuthor(DbHelper.Content.ContentInfo content) {
          if (UserInfo == null || content == null) return false;
          return IsUserAdmin(content);
        }

        public static bool CanSearchContent()
          => IsUserRoleAdmin
          || IsUserRoleLeader
          || IsUserRoleCoach;

        public static bool CanSearchProgramContent(DbHelper.AblePrograms.AbleProgramInfo program)
          => IsUserAdminOrProgramManager(program);

        public static bool CanViewProgramContent(DbHelper.AblePrograms.AbleProgramInfo program)
          => IsUserAdminOrProgramManager(program)
          || IsUserCoachInProgram(program);

        public static bool CanAddContentToProgram(DbHelper.AblePrograms.AbleProgramInfo program)
          => IsUserAdminOrProgramManager(program)
          || (IsUserRoleCoach && IsUserCoachInProgram(program));

        public static bool CanEditProgramContent(DbHelper.AblePrograms.AbleProgramInfo program, DbHelper.Content.ContentInfo content, bool isNewContent) {
          if (UserInfo == null || program == null || content == null) return false;
          if (isNewContent || content.IsPublished) {
            return IsUserAdmin(program);
          } else {
            return (IsUserRoleCoach && content.AuthorUserId == UserInfo.UserId);
          }
        }

        public static bool CanRemoveContentFromProgram(DbHelper.AblePrograms.AbleProgramInfo program)
          => IsUserAdminOrProgramManager(program);

        public static bool CanSendContentEmail(DbHelper.AblePrograms.AbleProgramInfo program)
          => IsUserAdminOrProgramManager(program);

        public static bool CanSendContentEmailToCoachee(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee) {
          return IsAdminOrProgramManager(coachee)
            || IsUserCoachForCoachee(coachee);
        }

        public static bool CanSetPublicContentForParticipants(DbHelper.Content.ContentInfo content) {
          if (UserInfo == null || content == null) return false;
          return IsUserAdmin(content);
        }
      }

      public class Modules {

        public static bool CanAdd() =>
          IsUserRoleAdmin
          || IsUserRoleTenantAdmin
          || IsUserRoleCoach;

        public static bool CanEdit(DbHelper.Modules.ModuleInfo moduleInfo) {
          if (UserInfo == null || moduleInfo == null) return false;
          return IsUserAdmin(moduleInfo)
            || (IsUserRoleCoach && moduleInfo.AuthorUserId == UserInfo.UserId);
        }

        public static bool CanDeleteModule(DbHelper.Modules.ModuleInfo moduleInfo) {
          if (UserInfo == null || moduleInfo == null) return false;
          return IsUserAdmin(moduleInfo)
            || (IsUserRoleCoach && moduleInfo.AuthorUserId == UserInfo.UserId);
        }

        public static bool CanChangeAuthor(DbHelper.Modules.ModuleInfo moduleInfo) {
          if (UserInfo == null || moduleInfo == null) return false;
          return IsUserAdmin(moduleInfo)
            || (IsUserRoleCoach && moduleInfo.AuthorUserId == UserInfo.UserId);
        }

        // Q: Do these need TenantAdmin access?
        public static bool CanSetPublicForParticipants() => IsUserRoleAdmin;
        public static bool CanSetPublicForPartners() => IsUserRoleAdmin;

        public static bool CanEditIsPublished(DbHelper.Modules.ModuleInfo moduleInfo) {
          if (UserInfo == null || moduleInfo == null) return false;
          return IsUserAdmin(moduleInfo)
            || (IsUserRoleCoach && moduleInfo.AuthorUserId == UserInfo.UserId);
        }

        public static bool CanAddToProgram(DbHelper.AblePrograms.AbleProgramInfo program)
          => IsUserAdminOrProgramManager(program)
          || IsUserCoachInProgram(program);

        public static bool CanRemoveFromProgram(DbHelper.AblePrograms.AbleProgramInfo program)
          => IsUserAdminOrProgramManager(program);

        public static bool CanEditProgramContent(DbHelper.AblePrograms.AbleProgramInfo program, DbHelper.Modules.ModuleInfo moduleInfo, bool isNewModule) {
          if (UserInfo == null || program == null || moduleInfo == null) return false;
          if (isNewModule || moduleInfo.IsPublished) {
            return IsUserAdminOrProgramManager(program);
          } else {
            return IsUserAdmin(moduleInfo)
              || (IsUserRoleCoach && moduleInfo.AuthorUserId == UserInfo.UserId);
          }
        }

        public static bool CanAddContentItems(DbHelper.Modules.ModuleInfo moduleInfo) {
          if (UserInfo == null || moduleInfo == null) return false;
          return IsUserAdmin(moduleInfo)
            || (IsUserRoleCoach && moduleInfo.AuthorUserId == UserInfo.UserId);
        }

        public static bool CanDeleteContentItems(DbHelper.Modules.ModuleInfo moduleInfo) {
          if (UserInfo == null || moduleInfo == null) return false;
          return IsUserAdmin(moduleInfo)
            || (IsUserRoleCoach && moduleInfo.AuthorUserId == UserInfo.UserId);
        }

        public static bool CanUpdateContentDisplayOrder(DbHelper.Modules.ModuleInfo moduleInfo) {
          if (UserInfo == null || moduleInfo == null) return false;
          return IsUserAdmin(moduleInfo)
            || (IsUserRoleCoach && moduleInfo.AuthorUserId == UserInfo.UserId);
        }

        public static bool CanViewActionButtons()
          => IsUserRoleAdmin
          || IsUserRoleCoach;

        public static bool CanViewModule(DbHelper.Modules.ModuleInfo moduleInfo) {
          if (UserInfo == null || moduleInfo == null) return false;
          if (IsUserAdmin(moduleInfo)
            || (IsUserRoleCoach && moduleInfo.AuthorUserId == UserInfo.UserId)) return true;
          if (!moduleInfo.IsPublished) return false;
          return (IsUserRoleCoach && moduleInfo.ShowToPartners)
            || (IsUserRoleLeader && moduleInfo.ShowToParticipants);
        }

        public static bool CanViewModule(DbHelper.Modules.ParticipantModuleInfo participantModuleInfo) {
          if (UserInfo == null || participantModuleInfo?.ModuleInfo == null) return false;
          if (!IsUserRoleLeader) return false;
          return participantModuleInfo.ModuleInfo.ShowToParticipants
            || participantModuleInfo.IsLinkedToProgram
            || participantModuleInfo.IsUserEnrolled;
        }

        public static bool CanEnrolInModule(DbHelper.Modules.ModuleInfo moduleInfo, DbHelper.Modules.ParticipantModuleInfo participantModuleInfo) {
          if (UserInfo == null || moduleInfo == null) return false;
          if (!IsUserRoleLeader) return false;
          return participantModuleInfo == null
            || !participantModuleInfo.IsUserEnrolled
            || participantModuleInfo.IsEnrolmentPending;
        }

        public static bool CanNavigateToContentFromModule(DbHelper.Modules.ModuleInfo moduleInfo, DbHelper.Modules.ParticipantModuleInfo participantModuleInfo) {
          if (UserInfo == null || moduleInfo == null) return false;
          if (IsUserRoleLeader) {
            if (participantModuleInfo == null || !participantModuleInfo.IsUserEnrolled) return false;
            return participantModuleInfo.IsUserEnrolled;
          }
          return IsUserAdmin(moduleInfo)
            || (IsUserRoleCoach && moduleInfo.AuthorUserId == UserInfo.UserId);
        }
      }

      public class Sessions {

        public static bool CanAdd(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee) =>
          CanEdit(coachee) && coachee.CoachingTypeId != DbHelper.AlbertCoachingTypes.GetType_NoCoaching().CoachingTypeId;

        public static bool CanEdit(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee) {
          if (UserInfo == null || coachee == null) return false;
          return IsUserRoleAdmin
            || IsUserTenantAdmin(coachee)
            || IsUserPCorPLCInProject(coachee.ProgramJobNumber)
            || coachee.CoachUserId == UserInfo.UserId;
        }

        public static bool ReadOnly(DbHelper.CoachingSessions.AbleSessionInfo session)
          => UserInfo == null
          || session == null
          || session.ComponentLocked;

        public static bool CanEditNotes(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee) {
          if (UserInfo == null || coachee == null) return false;
          return IsUserAdmin(coachee)
            || coachee.CoachUserId == UserInfo.UserId; // Coach always edit notes, even if the rest is read-only.
        }

        public static bool CanDelete(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee, DbHelper.CoachingSessions.AbleSessionInfo session)
          => CanEdit(coachee)
          && !ReadOnly(session);
      }

      public class Users {

        public static bool CanResetPassword(DbHelper.AbleUser.AbleUserInfo userInfo) {
          return userInfo != null;
        }

        public static bool CanDisplayCompanyLogoInNavBar() {
          var userInfo = GetUserInfoOrNull();
          if (userInfo == null) return false;
          return userInfo.DisplayLogoInNavBar.GetValueOrDefault(false) && (IsUserRoleClient || IsUserRoleLeader);
        }

        public static bool CanNavigatePlatform() {
          if (UserInfo == null) return false;
          if (UserInfo.SelfRegisteredAsRoleId != null) {
            // Self registered Client cannot navigate platform until they have a call with a sales person.
            if (IsUserRoleClient && UserInfo.SelfRegisteredAsRoleId == GetUserRoleId(GetUserRole())) {
              // TODO: Check if the meeting has taken place. Not instructed by Jeroen yet.
              return false;
            }
            // Self registered Leader/Participant cannot navigate platform until they buy a subscription
            if (IsUserRoleLeader && !UserInfo.HasSubscription) {
              return false;
            }
          }
          return true;
        }

        public static bool CanCreateSubscription() {
          if (UserInfo == null) return false;
          return IsUserRoleLeader && !UserInfo.HasSubscription;
        }
      }

      public class Reports {

        public static bool CanViewOldIOSReport() {
          if (UserInfo == null) return false;
          return UserInfo.IsIOSReportViewer
            && !UserInfo.ViewOnlyIOSReportUniqueId.IsNullOrEmpty();
        }

        // Rules for "External" IOS report outside of normal Able UI (e.g. Stark Industries demo).
        public static bool CanViewExternalIOSReport(string surveyUniqueId) {
          if (UserInfo == null) return false;
          return IsUserRoleAdmin
            || IsUserRoleCoach
            || (UserInfo.IsIOSReportViewer && surveyUniqueId == UserInfo.ViewOnlyIOSReportUniqueId);
        }

        public static bool CanViewEvalsForProject(DbHelper.Projects.ProjectInfoBrief projectInfo) {
          if (projectInfo == null || !projectInfo.ForUser_WasProvided) return false;
          return IsUserAdmin(projectInfo)
            || projectInfo.ForUser_IsInProjectAccess
            || projectInfo.ForUser_IsPCOrPLC;
        }

        public static bool CanViewOrgPeopleDetails360Results(Guid surveyUserGuid) {
          if (UserInfo == null) return false;
          return IsUserRoleAdmin || UserInfo.UserGuid == surveyUserGuid;
        }

        public class CoacheeSurveySummaryReport {

          public static bool CanViewSurveySelector() => !IsUserRoleLeader;

          public static bool CanShowDevPlanSlideout(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {
            if (UserInfo == null || coacheeInfo == null) return false;
            return IsUserRoleLeader
              && coacheeInfo.HasSubscription
              && coacheeInfo.UserSubscription.HasDevelopmentPlan;
          }
        }

        public static bool CanViewPeopleDetails() {
          return CanViewPeopleDetails(UserInfo?.UserGuid);
        }
        public static bool CanViewPeopleDetails(DbHelper.OrganisationUsers.ProfileInfo profile) {
          return CanViewPeopleDetails(profile?.UserGuid);
        }
        public static bool CanViewPeopleDetails(DbHelper.OrganisationUsers.OrgUserInfo orgUser) {
          return CanViewPeopleDetails(orgUser?.UserGuid);
        }
        private static bool CanViewPeopleDetails(Guid? userGuid) {
          if (UserInfo == null || userGuid == null) return false;
          // Restricted for Clients and unrelated Participants.
          if (IsUserRoleClient) return false;
          if (IsUserRoleLeader && UserInfo.UserGuid != userGuid) return false;
          return true;
        }

        public static bool CanViewIndividualReport(DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee) {
          if (UserInfo == null || coachee == null) return false;
          return IsAdminOrProgramManager(coachee)
            || IsUserCoachForCoachee(coachee)
            || PublicReport.GetIsLoggedIn(coachee.CoacheeUID)
            || (IsUserRoleLeader && coachee.UserId == UserInfo.UserId);
        }

      }

      public class Companies {

        public static bool CanCreate()
          => IsUserRoleAdmin
          || IsUserRoleTenantAdmin
          || IsUserRoleCoach;

        public static bool CanEdit(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {
          if (UserInfo == null || companyInfo == null) return false;
          if (IsUserAdmin(companyInfo)) return true;
          if (IsUserRoleCoach && IsUserCreator(companyInfo)) return true; // A partner can edit a company they created
          if (IsUserRoleClient && UserInfo.ClientCompanyId == companyInfo.CompanyId) return true; // A client can edit their own company.
          return false;
        }

        public static bool CanUpdateDisplayLogoInNavBar(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {
          if (UserInfo == null || companyInfo == null) return false;
          if (IsUserAdmin(companyInfo)) return true;
          if (IsUserRoleCoach && IsUserCreator(companyInfo)) return true; // A partner can edit a company they created
          if (IsUserRoleClient && UserInfo.ClientCompanyId == companyInfo.CompanyId) return true; // A client can edit their own company.
          return false;
        }

        public static bool CanUpdateClientLead(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {
          if (UserInfo == null || companyInfo == null) return false;
          if (IsUserAdmin(companyInfo)) return true;
          if (IsUserRoleCoach && IsUserCreator(companyInfo)) return true; // A partner can edit a company they created
          return false;
        }

        public static bool CanViewOrganisationListView() {
          if (UserInfo == null) return false;
          return IsUserRoleAdmin
            || IsUserRoleTenantAdmin
            || IsUserRoleCoach
            || IsUserRoleClient;
        }

        public static bool CanViewOrganisationSettings(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo, bool isNewCompany = false) {
          if (UserInfo == null || companyInfo == null) return false;
          if (isNewCompany) return CanCreate();
          if (IsUserAdmin(companyInfo)) return true;
          if (IsUserRoleCoach && UserInfo.IsLinkedToCompany(companyInfo.CompanyId)) return true;
          if (IsUserRoleClient && UserInfo.ClientCompanyId == companyInfo.CompanyId) return true;
          return false;
        }

        public static bool CanViewOrganisationPeople(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {
          if (UserInfo == null || companyInfo == null) return false;
          if (IsUserAdmin(companyInfo)) return true;
          if (IsUserRoleCoach && UserInfo.PLCForCompanyIds?.IndexOf(companyInfo.CompanyId) >= 0) return true;
          if (IsUserRoleClient && UserInfo.ClientCompanyId == companyInfo.CompanyId) return true;
          return false;
        }

        public static bool CanViewOrganisationDepartments(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {
          if (UserInfo == null || companyInfo == null) return false;
          if (IsUserAdmin(companyInfo)) return true;
          if (IsUserRoleCoach && UserInfo.PLCForCompanyIds?.IndexOf(companyInfo.CompanyId) >= 0) return true;
          if (IsUserRoleClient && UserInfo.ClientCompanyId == companyInfo.CompanyId) return true;
          return false;
        }

        public static bool CanViewOrganisationOverview(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {
          if (UserInfo == null || companyInfo == null) return false;
          if (IsUserAdmin(companyInfo)) return true;
          if (IsUserRoleCoach && UserInfo.PLCForCompanyIds?.IndexOf(companyInfo.CompanyId) >= 0) return true;
          if (UserInfo.ClientCompanyId == companyInfo.CompanyId) return true;
          return false;
        }

        public static bool CanViewOrganisationProjects(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {
          if (UserInfo == null || companyInfo == null) return false;
          if (IsUserAdmin(companyInfo)) return true;
          if (IsUserRoleCoach && UserInfo.PLCForCompanyIds?.IndexOf(companyInfo.CompanyId) >= 0) return true;
          if (UserInfo.ClientCompanyId == companyInfo.CompanyId) return true;
          return false;
        }

        public static bool CanViewOrganisationCapabilities(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {
          if (UserInfo == null || companyInfo == null) return false;
          if (IsUserAdmin(companyInfo)) return true;
          if (IsUserRoleCoach && UserInfo.PLCForCompanyIds?.IndexOf(companyInfo.CompanyId) >= 0) return true;
          if (UserInfo.ClientCompanyId == companyInfo.CompanyId) return true;
          return false;
        }

        public static bool CanAddCompanyParticipants(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {
          if (UserInfo == null || companyInfo == null) return false;
          if (IsUserAdmin(companyInfo)) return true;
          if (IsUserRoleClient && companyInfo.CompanyId == UserInfo.ClientCompanyId) return true;
          if (companyInfo.ClientLeadUserId == UserInfo.UserId) return true;
          return false;
        }

        public static bool CanEditAIContext(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {
          if (UserInfo == null || companyInfo == null) return false;
          if (IsUserAdmin(companyInfo)) return true;
          if (IsUserRoleClient && companyInfo.CompanyId == UserInfo.ClientCompanyId) return true;
          return false;
        }

        public static bool CanViewOrganisationIOSReports(DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo) {
          // IOS survey at company level.
          var user = GetUserInfoOrNull();
          if (user == null || companyInfo == null) return false;
          if (IsUserAdmin(companyInfo)) return true;
          if (IsUserRoleCoach) {
            return companyInfo.IsCoachInCompany(user.UserId)
              || companyInfo.IsPLCPCCompany(user.UserId)
              || companyInfo.IsSalesPartnerInCompany(user.UserId)
              || companyInfo.IsFacilitatorInCompany(user.UserId);
          }
          if (IsUserRoleClient) {
            return user.ProjectAccessForCompanyIds.Contains(companyInfo.CompanyId)
              || GetUserInfoOrNull().ClientCompanyId == companyInfo.CompanyId;
          }
          return false;
        }

        public static bool CanViewProjectsLinkToInvoicing()
          => IsUserRoleAdmin;
      }

      public class Metrics { // Summary info boxes on various pages.

        public static bool CanViewOrgMetrics()
          => IsUserRoleClient;

        public static bool CanViewPartnerMetrics()
          => IsUserRoleAdmin
          || IsUserRoleCoach;
      }

      public class Coaches {

        public static bool CanViewCoachProfile(DbHelper.AbleUser.UserIdentity forUser) {
          if (UserInfo == null || forUser == null) return false;
          if (IsUserAdmin(forUser)) return true;
          if (IsUserRoleClient) return true; // Clients also can see any profile.
          if (UserInfo.UserId == forUser.UserId) return true; // Anyone can view their own profile.
          if (IsUserRoleCoach) return UserInfo.IsPartnerActive;
          return false;
        }

        public static bool CanViewCoachContactInfo(DbHelper.AbleUser.AbleUserBasicInfo forUser) {
          if (UserInfo == null || forUser == null) return false;
          if (IsUserRoleAdmin) return true;
          if (IsUserRoleCoach && UserInfo.UserId != forUser.UserId && !UserInfo.IsPartnerActive) return false;
          return (IsUserRoleCoach && UserInfo.IsPartnerActive)
            || IsUserRoleClient
            || UserInfo.UserId == forUser.UserId;
        }

        public static bool CanEditUserProfile(DbHelper.AbleUser.AbleUserBasicInfo forUser) {
          if (UserInfo == null || forUser == null) return false;
          return IsUserRoleAdmin
            || UserInfo.UserId == forUser.UserId;
        }

        public static bool CanEditCompany(DbHelper.AbleUser.AbleUserBasicInfo forUser) {
          if (UserInfo == null || forUser == null) return false;
          if (IsUserRoleAdmin) return true;
          if (IsUserRoleTenantAdmin || IsUserRoleCoach) {
            if (UserInfo.UserId != forUser.UserId) return false;
            return UserInfo.IsTenantOrgAdmin || UserInfo.IsOrgOwner;
          }
          return false;
        }

        public static bool CanViewPayRuns(DbHelper.AbleUser.AbleUserInfo loggedInUser, int viewCoachUserId) {
          if (UserInfo == null || loggedInUser == null) return false;
          // On admins can view pay run page, or the coach can view their own page.
          return IsUserRoleAdmin
            || loggedInUser.UserId == viewCoachUserId;
        }

        public static bool CanChangeTags(DbHelper.AbleUser.AbleUserBasicInfo forUser) {
          if (UserInfo == null || forUser == null) return false;
          return IsUserRoleAdmin
            || forUser.UserId == UserInfo.UserId;
        }

        public static bool CanViewPendingInvites(DbHelper.AbleUser.AbleUserBasicInfo forUser) {
          return true; // All users can view other users' pending invites.
        }

        public static bool CanInvitePartners(DbHelper.AbleUser.AbleUserBasicInfo forUser) {
          if (UserInfo == null || forUser == null) return false;
          // Can only invite partners for yourself (i.e. for your own company).
          return forUser.UserId == UserInfo.UserId;
        }

        public static bool CanViewContract(DbHelper.AbleUser.AbleUserBasicInfo forUser) {
          if (UserInfo == null || forUser == null) return false;
          // Integral only. Can only view own contract, unless admin.
          if (UserInfo.OrgId != ConfigHelper.IntegralTenantOrgId) return false;
          return IsUserRoleAdmin
            || forUser.UserId == UserInfo.UserId;
        }

        public static bool CanCreateContract(DbHelper.AbleUser.AbleUserBasicInfo forUser) {
          if (UserInfo == null || forUser == null) return false;
          // Can only create own contract, unless admin.
          return IsUserRoleAdmin
            || forUser.UserId == UserInfo.UserId;
        }

        public static bool CanViewInternalActions() {
          if (UserInfo == null) return false;
          return !IsUserRoleClient;
        }

        public static bool CanViewAIChat(DbHelper.AbleUser.AbleUserInfo userOrNullForCurrent = null) {
          var user = userOrNullForCurrent ?? GetUserInfoOrNull();
          return IsUserRoleLeader && user.HasSubscription && user.UserSubscription.HasAICoaching && user.UserSubscription.UserHasAICoachEnabled;
        }

        public static bool CanViewIntegralBio(DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo) {
          if (UserInfo == null || coachInfo == null) return false;
          return coachInfo.IsAbleCoach
            && coachInfo.OrgId == ConfigHelper.IntegralTenantOrgId;
        }

        public static bool CanViewNonProfileTabs(DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo) {
          if (UserInfo == null || coachInfo == null) return false;
          return IsUserRoleAdmin
            || coachInfo.IsAbleCoach;
        }

        public static bool CanViewParticipantsTabs(DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo) {
          if (UserInfo == null || coachInfo == null) return false;
          // Only participants can see settings tab.
          return IsUserRoleLeader
            && coachInfo.UserId == UserInfo.UserId
            && coachInfo.LatestCoacheeInfo != null;
        }

        public static bool CanEditParticipantsSettings(DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo) {
          if (UserInfo == null || coachInfo == null) return false;
          // Only participants edit their own settings.
          return IsUserRoleLeader
            && coachInfo.UserId == UserInfo.UserId
            && coachInfo.LatestCoacheeInfo != null;
        }

        public static bool CanUpdateCoachXeroContact(DbHelper.AbleUser.AbleUserBasicInfo forUser) {
          if (UserInfo == null || forUser == null) return false;
          // TODO: When XeroContacts are separated by tenant, then Tenant Admin can alter it if forUser is in the same tenant.
          return IsUserRoleAdmin
            && forUser.IsAbleCoach;
        }

        public static bool CanViewProfileUrls(DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo) {
          if (IsUserRoleAdmin || coachInfo.IsAbleCoach) return true;
          return false;
        }

        public static bool CanParticipantSelfSelect(Integral.Web.DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo) {
          return coachInfo.TagIdList.ContainsAny(ConfigHelper.PartnerTagId.RequiredTagsForCoachSelfSelectionByParticipant);
        }

        public static bool CanBeAssignedAsCoachForParticipant(Integral.Web.DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo, bool coacheeHas360WithRatersForCoachAllocation) {

          if (coachInfo == null) return false;
          if (coachInfo.UserId == ConfigHelper.UserId.Unassigned) return true;

          if (coachInfo.OrgId == ConfigHelper.IntegralTenantOrgId) {
            // Rules for Integral

            if (coacheeHas360WithRatersForCoachAllocation
              && coachInfo.TagIdList != null
              && coachInfo.TagIdList.Contains(ConfigHelper.PartnerTagId.Integral_ILP_LMP_LDP_PDP_EIPSL_IHELP_TagId))
              return true;

            if (!coacheeHas360WithRatersForCoachAllocation
              && coachInfo.TagIdList != null
              && ConfigHelper.PartnerTagId.RequiredTagsForCoachAssignment.Any(x => coachInfo.TagIdList.Contains(x)))
              return true;

            return false;
          }

          if (coachInfo.IsProfileHidden) return false;

          return true;
        }

        public static bool CanViewAndEditHideProfileToggle(DbHelper.AbleUser.AbleUserBasicInfo forUser) {
          if (UserInfo == null || forUser == null) return false;
          return IsUserRoleAdmin
            || UserInfo.UserId == forUser.UserId; // Can edit own setting.
        }

        public static bool CanViewStatusToggle()
          => IsUserRoleAdmin ||
          IsUserRoleCoach;

        // These are super-admin only.
        public static bool CanViewHiddenPartners() => IsUserRoleAdmin;
        public static bool CanViewInactivePartners() => IsUserRoleAdmin;
        public static bool CanViewAllUsers() => IsUserRoleAdmin;
        public static bool CanUpdateCoachRoleFlags() => IsUserRoleAdmin;
        public static bool CanDeleteUser() => IsUserRoleAdmin;
      }

      public class Surveys {

        // If a survey can be seen in a user's survey list.
        public static bool CanListParticipantSurvey(DbHelper.AlbertSurveys.SurveyInfo survey) {
          if (UserInfo == null || survey == null) return false;
          if (IsUserRoleCoach && survey.SurveyTypeCode == ConfigHelper.SurveyTypeCodes.IOS) return false; // Coaches can't see IOS surveys in pax's list.
          return true; // In all other cases, survey can be listed.
        }

        public static bool CanCompleteSurvey(DbHelper.AlbertSurveys.SurveyInfo survey) {
          if (UserInfo == null || survey?.FoundParticipantBrief == null) return false;
          if (survey.IsClosed || survey.IsClosedSelf) return false;
          return IsUserRoleLeader && survey.FoundParticipantBrief.UserId == UserInfo.UserId; // Only Participent ("Leader") role can fill out surveys.
        }

        public static bool CanViewIOSDetails(DbHelper.AlbertSurveys.SurveyInfo survey) {

          if (UserInfo == null || survey?.FoundParticipantBrief == null) return false;
          if (survey.SurveyType != DbHelper.AlbertSurveys.SurveyTypeEnum.IOS) return false;

          if (IsUserRoleAdmin) return true;

          // Client can view IOS surveys in their company (Integral mainly).
          if (IsUserRoleClient && GetUserInfoOrNull().ClientCompanyId == survey.CompanyId) return true;

          // Tenant admin can view IOS surveys in their tenant.
          if (IsUserRoleTenantAdmin && UserInfo.OrgId == survey.OrgId) return true;

          return false;
        }

        public static bool CanViewSharedSurveyDetails(DbHelper.SurveyShare.SharedSurveysInfo sharedSurveyInfo) {
          if (UserInfo == null || sharedSurveyInfo == null) return false;

          return sharedSurveyInfo.UserIdSharedWith == UserInfo.UserId; // Survey is shared with user
        }

        public static bool CanViewDetails(DbHelper.AblePrograms.AbleProgramInfo program, DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee, DbHelper.AlbertSurveys.SurveyInfo survey) {
          // Note that survey can't be null, but coachee and program may be null (e.g. if leader viewing their own survey list).
          if (UserInfo == null || survey?.FoundParticipantBrief == null) return false;
          // Check most granular (survey) to least granular (coachee then program).
          if (survey.IsSharedWithUser == true) return true;
          if (survey.FoundParticipantBrief.UserId == UserInfo.UserId) return true;
          if (IsUserCoachee(coachee)) return true;
          if (IsUserCoachForCoachee(coachee)) return true;
          if (IsUserAdminOrProgramManager(program)) return true;
          return false;
        }

        public static bool CanViewResponses(DbHelper.AblePrograms.AbleProgramInfo program, DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee, DbHelper.AlbertSurveys.SurveyInfo survey) {
          if (UserInfo == null || program == null || coachee == null || survey?.FoundParticipantBrief == null) return false;

          if (!CanViewDetails(program, coachee, survey)) return false;
          if (survey.FoundParticipantBrief.CompletedUtc != null) return true;
          if (survey.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.DevelopmentPlan) return true; // Can view DevPlan responses even if incomplete.
          return false;
        }

        public static bool CanShareSurvey(DbHelper.AlbertSurveys.SurveyInfo survey) {
          if (UserInfo == null || survey?.FoundParticipantBrief == null) return false;

          if (!IsUserRoleLeader) return false; // Only leader role can share.
          if (survey.SurveyTypeCode == ConfigHelper.SurveyTypeCodes.IOS) return false; // Can't share IOS surveys.
          if (survey.SurveyTypeCode == ConfigHelper.SurveyTypeCodes.DevPlan) return true; // Can always share DevPlan even if incomplete.
          if (survey.FoundParticipantBrief.CompletedUtc == null) return false; // Can only share completed surveys.
          return survey.FoundParticipantBrief.UserId == UserInfo.UserId; // User can only share their own surveys.
        }

        public static bool CanUnshareSurvey(DbHelper.SurveyShare.SharedSurveysInfo survey) {
          if (UserInfo == null || survey == null) return false;
          return IsUserRoleLeader && survey.UserIdSharedBy == UserInfo.UserId;
        }

        public static bool CanViewReports(DbHelper.AlbertSurveys.SurveyInfo survey) {
          return CanViewReports(null, null, null, survey);
        }

        public static bool CanViewReports(DbHelper.ClientCompanies.AlbertCompanyInfo company, DbHelper.AlbertSurveys.SurveyInfo survey) {
          return CanViewReports(company, null, null, survey); // Org level IOS surveys.
        }

        public static bool CanViewReports(
          DbHelper.AblePrograms.AbleProgramInfo program,
          DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee,
          DbHelper.AlbertSurveys.SurveyInfo survey) {

          return CanViewReports(null, program, coachee, survey);
        }

        // TODO: Simplify by splitting off the IOS part, separate method "CanViewIOSReport".
        private static bool CanViewReports(
          DbHelper.ClientCompanies.AlbertCompanyInfo company,
          DbHelper.AblePrograms.AbleProgramInfo program,
          DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee,
          DbHelper.AlbertSurveys.SurveyInfo survey) {

          // At least survey is required, other args depend on user role context.
          if (survey == null) return false;

          // IOS reports, being at org level, have slightly different access rules.
          // Can be viewed at company or program level.
          // A Client user can view if they have access to same company as survey.
          // Other roles are based on access to the same program.
          if (survey.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.IOS) {
            if (IsUserRoleClient) {
              return Insights.CanViewIOSReports(survey);
            } else if (company != null) {
              return Companies.CanViewOrganisationIOSReports(company);
            } else {
              return Programs.CanViewProgramIOSReports(program);
            }
          }

          var surveyPart = survey?.FoundParticipantBrief;

          // Viewing own reports as leader.
          if (IsUserRoleLeader) {
            if (surveyPart == null || surveyPart.UserId != UserInfo.UserId || !surveyPart.IsReportAvailable_Online) return false;
            if (surveyPart.CanLeaderView360Report) return true;
            if (survey.AllowReportWithoutRaters && surveyPart.IsSelfCompleted) return true;
            return false;
          }

          // Viewing reports in other roles depend on access to the program & coachee.
          if (IsUserAdminOrProgramManager(program) || IsUserCoachForCoachee(coachee)) return true;
          if (surveyPart != null && surveyPart.UserId == UserInfo.UserId) return true;
          return false;
        }

        public static bool CanChangeCloseDate(DbHelper.AblePrograms.AbleProgramInfo program)
          => IsUserAdminOrProgramManager(program)
          && !IsUserRoleLeader;

        // For logged in users not public surveys.
        public static bool CanViewRaters()
          => IsUserRoleAdmin; // Q: Should this be super-admin only, or also allow tenant admin?

        // For logged in users not public surveys.
        public static bool CanInviteRaters(DbHelper.AblePrograms.AbleProgramInfo program, DbHelper.AlbertSurveys.SurveyInfo survey) {
          if (UserInfo == null || program == null || survey == null) return false;
          if (survey.IsSelfOnly || survey.IsClosedRaters) return false;
          return IsUserAdminOrProgramManager(program);
        }

        public static bool CanEmailReports(DbHelper.AblePrograms.AbleProgramInfo program, DbHelper.AlbertCoachees.AlbertCoacheeInfo coachee)
          => IsUserAdminOrProgramManager(program)
          || IsUserCoachForCoachee(coachee);

        public static bool CanViewReportButtons() => !IsUserRoleLeader;

        public static bool CanViewOrgReportButtons() => IsUserRoleAdmin || IsUserRoleClient;

        public static bool CanEditInJarvis() => IsUserRoleAdmin;

        public static bool CanDeleteDevPlan(DbHelper.AlbertSurveys.SurveyInfo surveyInfo) {
          if (UserInfo == null || surveyInfo == null) return false;
          return IsUserCreator(surveyInfo);
        }

        // Dev only.
        public static bool CanViewRandomAnswersButton(List<DbHelper.Questions.SurveyQuestionInfo> questionList) {
          return ConfigHelper.IsDevServer
            && questionList.Exists(
              q => q.InputType == DbHelper.AnswerTypes.InputTypes.SCALE.Value
              || q.InputType == DbHelper.AnswerTypes.InputTypes.RANKED.Value); // Only works for scale & ranked questions.
        }

        // Dev only.
        public static bool CanViewDataEntryButton => ConfigHelper.IsDevServer;
      }

      public class Programs {

        public static bool CanCreateProgram(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          return IsUserAdmin(projectInfo)
            || IsUserQuoteOwnerInProject(projectInfo);
        }

        public static bool CanViewProgramSettings(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return CanEditProgramSettings(programInfo)
            || programInfo.OwnCoacheeCount > 0
            || programInfo.OwnWorkshopCount > 0
            || programInfo.CoFacWorkshopCount > 0;
        }

        public static bool CanEditProgramSettings(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo)
            || programInfo.ProjectCreatedByUserId == UserInfo.UserId;
        }

        public static bool CanViewProgramOverview(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo)
            || IsUserDeliveryInProgram(programInfo)
            || UserHasConsultingItems(programInfo)
            || IsUserInProjectAccess(programInfo)
            || IsUserQuoteOwnerInProject(programInfo);
        }

        public static bool CanNavigateFromOverviewTables() {
          return !IsUserRoleClient;
        }

        public static bool CanEditSettingsPercentages(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          return IsUserAdmin(programInfo)
            && !programInfo.HasLockedComponents;
        }

        public static bool CanViewSettingsPercentages(DbHelper.Projects.ProjectInfo projectInfo, bool isNewProgram) {
          if (isNewProgram) {
            return IsUserAdmin(projectInfo);
          } else {
            return true;
          }
        }

        public static bool CanViewSettingsDates(DbHelper.Projects.ProjectInfo projectInfo, bool isNewProgram) {
          if (isNewProgram) {
            return IsUserAdmin(projectInfo);
          } else {
            return true;
          }
        }

        public static bool CanViewAllProjectPrograms() {
          return IsUserRoleAdmin;
        }

        public static bool CanSendSurveys(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          return IsUserAdminOrProgramManager(programInfo);
        }

        public static bool CanSendProgramEmail(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          // Admin, PC, PLC, Workshop facilitator & co-facilitator.
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo)
            || programInfo.OwnWorkshopCount > 0
            || programInfo.CoFacWorkshopCount > 0
            || IsUserInProjectAccess(programInfo);
        }

        public static bool CanListProgramParticipants(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo)
            || IsUserCoachInProgram(programInfo)
            || IsUserWorkshopFacilitator(programInfo)
            || IsUserInProjectAccess(programInfo);
        }

        public static bool CanSendInviteToAddParticipants(DbHelper.AblePrograms.AbleProgramInfo programInfo, List<DbHelper.ProjectUserAccess.ProjectAccessInfo> programAccessUsers) {
          if (UserInfo == null || programInfo == null || programAccessUsers == null) return false;
          return CanEditProgramSettings(programInfo)
            && programAccessUsers.Count > 0;
        }

        public static bool CanViewProgramInsights(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo)
            || IsUserWorkshopFacilitator(programInfo)
            || IsUserCoachInProgram(programInfo)
            || IsUserInProjectAccess(programInfo)
            || IsUserQuoteOwnerInProject(programInfo)
            || UserHasConsultingItems(programInfo);
        }

        public class Revenue {

          public static bool CanViewTotalRevenue(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
            if (UserInfo == null || programInfo == null) return false;
            return IsUserAdmin(programInfo)
              || IsUserPC(programInfo);
          }

          public static bool CanViewAllDeliveryTeamRevenue(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
            if (UserInfo == null || programInfo == null) return false;
            return IsUserAdmin(programInfo)
              || IsUserPC(programInfo);
          }

          public static bool CanViewPartnerRevenue(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
            if (UserInfo == null || programInfo == null) return false;
            return IsUserAdmin(programInfo)
              || IsUserPC(programInfo)
              || IsUserDeliveryInProgram(programInfo)
              || UserHasConsultingItems(programInfo);
          }
        }

        public class Workshops {

          public static bool CanNavigateFromWorkshopTable(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
            if (UserInfo == null || programInfo == null) return false;
            return IsUserAdminOrProgramManager(programInfo)
              || IsUserRoleCoach
              || IsUserInProjectAccess(programInfo);
          }

          public static bool CanViewAllInProgram(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
            if (UserInfo == null || programInfo == null) return false;
            return IsUserAdminOrProgramManager(programInfo)
              || IsUserCoachInProgram(programInfo)
              || IsUserInProjectAccess(programInfo);
          }

          public static bool CanAdd(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
            if (UserInfo == null || programInfo == null) return false;
            return IsUserAdminOrProgramManager(programInfo);
          }

          public static bool CanView(DbHelper.AblePrograms.AbleProgramInfo programInfo, DbHelper.WorkshopEvents.WorkshopEventInfo workshopEventInfo) {
            if (UserInfo == null || programInfo == null || workshopEventInfo == null) return false;
            if (programInfo.ProgramJobId != workshopEventInfo.ProgramJobId) return false;
            return IsUserAdminOrProgramManager(programInfo)
              || IsUserCoachInProgram(programInfo)
              || IsUserInProjectAccess(programInfo)
              || IsFacilitator(workshopEventInfo);
          }

          public static bool CanViewWorkshops(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
            if (UserInfo == null || programInfo == null) return false;
            return IsUserAdminOrProgramManager(programInfo)
              || IsUserWorkshopFacilitator(programInfo)
              || IsUserCoachInProgram(programInfo)
              || IsUserInProjectAccess(programInfo);
          }

          public static bool LimitedEdit(DbHelper.AblePrograms.AbleProgramInfo programInfo, DbHelper.WorkshopEvents.WorkshopEventInfo workshopEventInfo) {
            if (UserInfo == null || programInfo == null || workshopEventInfo == null) return false;
            if (programInfo.ProgramJobId != workshopEventInfo.ProgramJobId) return false;
            // ONLY the assigned facilitator for the workshop.
            return !IsUserAdminOrProgramManager(programInfo)
              && IsFacilitator(workshopEventInfo);
          }

          public static bool ReadOnly(DbHelper.AblePrograms.AbleProgramInfo programInfo, DbHelper.WorkshopEvents.WorkshopEventInfo workshopEventInfo) {
            if (UserInfo == null || programInfo == null || workshopEventInfo == null) return false;
            if (programInfo.ProgramJobId != workshopEventInfo.ProgramJobId) return false;
            return (!IsUserAdminOrProgramManager(programInfo) && !IsFacilitator(workshopEventInfo))
              || workshopEventInfo.ComponentQuoteInfo.IsComponentLocked;
          }

          public static bool CanDelete(DbHelper.AblePrograms.AbleProgramInfo programInfo, DbHelper.WorkshopEvents.WorkshopEventInfo workshopEventInfo) {
            if (UserInfo == null || programInfo == null || workshopEventInfo == null) return false;
            if (programInfo.ProgramJobId != workshopEventInfo.ProgramJobId) return false;
            return !ReadOnly(programInfo, workshopEventInfo)
              && !LimitedEdit(programInfo, workshopEventInfo)
              && workshopEventInfo.EvalIntakeCodeId == null; // No eval survey attached.
          }

          public static bool CanCopyWorkshop(DbHelper.AblePrograms.AbleProgramInfo programInfo, DbHelper.WorkshopEvents.WorkshopEventInfo workshopEventInfo) {
            if (UserInfo == null || programInfo == null || workshopEventInfo == null) return false;
            if (programInfo.ProgramJobId != workshopEventInfo.ProgramJobId) return false;
            return (IsUserAdminOrProgramManager(programInfo)
              || IsFacilitator(workshopEventInfo));
          }

          // Internal use only:
          internal static bool IsFacilitator(DbHelper.WorkshopEvents.WorkshopEventInfo workshopEventInfo) {
            if (UserInfo == null || workshopEventInfo == null) return false;
            return UserInfo.UserId == workshopEventInfo.KeyFacilitatorUserId
              || UserInfo.UserId == workshopEventInfo.CoFacilitatorUserId;
          }
        }

        public class Consulting {

          public static bool CanNavigateFromConsultingTable(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
            if (UserInfo == null || programInfo == null) return false;
            return IsUserAdminOrProgramManager(programInfo)
              || IsUserRoleCoach;
          }

          public static bool CanListConsultingItems(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
            if (UserInfo == null || programInfo == null) return false;
            return IsUserAdminOrProgramManager(programInfo)
              || IsUserRoleCoach
              || IsUserInProjectAccess(programInfo);
          }

          public static bool CanViewAllInProgram(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
            if (UserInfo == null || programInfo == null) return false;
            return IsUserAdminOrProgramManager(programInfo)
              || IsUserInProjectAccess(programInfo);
          }

          public static bool CanAdd(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
            if (UserInfo == null || programInfo == null) return false;
            return IsUserAdminOrProgramManager(programInfo);
          }

          public static bool CanView(DbHelper.AblePrograms.AbleProgramInfo programInfo, DbHelper.ConsultingItems.ConsultingItemInfo consultingItemInfo) {
            if (programInfo == null || consultingItemInfo == null) return false;
            if (programInfo.ProgramJobId != consultingItemInfo.ProgramJobId) return false;
            return IsUserAdminOrProgramManager(programInfo)
              || IsConsultant(consultingItemInfo);
          }

          public static bool LimitedEdit(DbHelper.AblePrograms.AbleProgramInfo programInfo, DbHelper.ConsultingItems.ConsultingItemInfo consultingItemInfo) {
            if (UserInfo == null || programInfo == null || consultingItemInfo == null) return false;
            if (programInfo.ProgramJobId != consultingItemInfo.ProgramJobId) return false;
            return IsConsultant(consultingItemInfo)
              && !IsUserAdminOrProgramManager(programInfo);
          }

          public static bool ReadOnly(DbHelper.AblePrograms.AbleProgramInfo programInfo, DbHelper.ConsultingItems.ConsultingItemInfo consultingItemInfo) {
            if (UserInfo == null || programInfo == null || consultingItemInfo == null) return false;
            if (programInfo.ProgramJobId != consultingItemInfo.ProgramJobId) return false;
            if (consultingItemInfo.ComponentQuoteInfo.IsComponentLocked) return true;
            return (!IsUserAdminOrProgramManager(programInfo)
              && !IsConsultant(consultingItemInfo));
          }

          public static bool CanDelete(DbHelper.AblePrograms.AbleProgramInfo programInfo, DbHelper.ConsultingItems.ConsultingItemInfo consultingItemInfo) {
            if (UserInfo == null || programInfo == null || consultingItemInfo == null) return false;
            if (programInfo.ProgramJobId != consultingItemInfo.ProgramJobId) return false;
            if (consultingItemInfo.ComponentQuoteInfo.IsComponentLocked) return false;
            return IsUserAdminOrProgramManager(programInfo);
          }

          // Internal use only:
          private static bool IsConsultant(DbHelper.ConsultingItems.ConsultingItemInfo consultingItemInfo) {
            if (UserInfo == null || consultingItemInfo == null) return false;
            return UserInfo.UserId == consultingItemInfo.ConsultantUserId;
          }
        }

        public static bool CanListAllParticipants(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo)
            || IsUserWorkshopFacilitator(programInfo)
            || IsUserInProjectAccess(programInfo);
        }

        public static bool CanViewProgramParticipants(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo)
            || IsUserCoachInProgram(programInfo)
            || IsUserInProjectAccess(programInfo);
        }

        public static bool CanAddProgramParticipant(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo)
            || IsUserInProjectAccess(programInfo);
        }

        public static bool CanUpdateProgramParticipant(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo);
        }

        public static bool CanListCostItems(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo);
        }

        public static bool CanAddCostItem(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo);
        }

        public static bool CanDeleteCostItem(DbHelper.AblePrograms.AbleProgramInfo programInfo, DbHelper.ProgramCostItems.ProgramCostItemInfo costItemInfo) {
          if (UserInfo == null || programInfo == null || costItemInfo == null) return false;
          return CanEditCostItem(programInfo, costItemInfo);
        }

        public static bool CanEditCostItem(DbHelper.AblePrograms.AbleProgramInfo programInfo, DbHelper.ProgramCostItems.ProgramCostItemInfo costItemInfo = null) {
          // CostItemInfo can be null if adding new costitem.
          if (UserInfo == null || programInfo == null) return false;
          if (costItemInfo?.ComponentQuoteInfo?.IsComponentLocked == true) return false;
          return IsUserAdminOrProgramManager(programInfo);
        }

        public static bool CanEditCostAndQty(DbHelper.AblePrograms.AbleProgramInfo programInfo, DbHelper.ProgramCostItems.ProgramCostItemInfo costItemInfo = null) {
          if (UserInfo == null || programInfo == null) return false;
          return CanEditCostItem(programInfo, costItemInfo)
            && costItemInfo?.XeroPurchaseOrderId == null;
        }

        public static bool CanMoveToProgram(DbHelper.AblePrograms.AbleProgramInfo fromProgramInfo) {
          if (UserInfo == null || fromProgramInfo == null) return false;
          return IsUserAdminOrProgramManager(fromProgramInfo);
        }

        public static bool CanSetQuoteItem(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo);
        }

        public static bool CanSubmitPOtoXero(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          return IsUserAdmin(programInfo);
        }

        public static bool CanOverwriteUnitPrice(DbHelper.Projects.ProjectInfo projectInfo, DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || projectInfo == null || programInfo == null) return false;
          return IsUserAdminOrProgramManager(programInfo)
            && projectInfo.AllowCostItemUnitPriceManualOverwrite;
        }

        public static bool CanViewClientProgramSummary()
          => IsUserRoleClient;

        public static bool CanViewProgramIOSReports(DbHelper.AblePrograms.AbleProgramInfo program) {
          // IOS survey at program level.
          if (UserInfo == null || program == null) return false;
          return IsUserAdmin(program)
            || (IsUserRoleCoach && IsUserDeliveryInProject(program.ProgramJobNumber))
            || (IsUserRoleClient && UserInfo.ClientCompanyId == program.CompanyId);
        }

      }

      public class Invoices {

        public static bool CanDeleteInvoice() {
          if (UserInfo == null) return false;
          return IsUserRoleAdmin
            && UserInfo.UserId == ConfigHelper.UserId.CanDeleteInvoices;
        }

        public static bool CanSubmitInvoice() {
          if (UserInfo == null) return false;
          return IsUserRoleAdmin;
        }

        public static bool CanAddDeleteItems(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          return IsUserAdminOrProgramManager(projectInfo);
        }

        public static bool CanBulkAddInvoiceComponents(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          return IsUserAdminOrProgramManager(projectInfo)
            || (IsUserRoleClient && IsUserInProjectAccess(projectInfo));
        }

        public static bool CanSendInvoiceToXero(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          return !projectInfo.XeroAccountCode.IsNullOrEmptyOrWhitespace();
        }
      }

      public class Projects {

        public static AppHelper.ParamEnum.ProjectAccessLevel GetProjectAccessLevel() {
          if (UserInfo == null) return AppHelper.ParamEnum.ProjectAccessLevel.None;
          if (IsUserRoleAdmin) return AppHelper.ParamEnum.ProjectAccessLevel.All;
          if (IsUserRoleTenantAdmin) return AppHelper.ParamEnum.ProjectAccessLevel.TenantOrg; // Tenant admins can see all projects in tenant.
          return AppHelper.ParamEnum.ProjectAccessLevel.RelatedOrInvited; // Default, only related users (PLC, coach, etc) or via ProjectAccess.
        }

        public static bool CanViewProjectList() {
          if (UserInfo == null) return false;
          return IsUserRoleAdmin;
        }

        public static bool CanEditProjectComponents() {
          if (UserInfo == null) return false;
          return !IsUserRoleClient;
        }

        public static bool CanUpdateComponent(DbHelper.ProgramComponents.ComponentInfo componentInfo) {
          if (UserInfo == null || componentInfo == null) return false;
          return !IsUserRoleClient
            && componentInfo.PLPeriodDate == null;
        }

        public static bool CanViewProject(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          return IsUserAdminOrProgramManager(projectInfo)
            || IsUserQuoteOwnerInProject(projectInfo)
            || IsUserDeliveryInProject(projectInfo)
            || IsUserInProjectAccess(projectInfo)
            || IsUserCreator(projectInfo);
        }

        public static bool CanViewProject(DbHelper.AblePrograms.AbleProgramInfo program) {
          if (UserInfo == null || program == null) return false;
          return IsUserAdminOrProgramManager(program)
            || IsUserQuoteOwnerInProject(program)
            || IsUserDeliveryInProject(program.ProgramJobNumber)
            || IsUserInProjectAccess(program);
        }

        public static bool CanViewProjectInvoicing(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          return IsUserAdminOrProgramManager(projectInfo);
        }

        public static bool CanCreateNewProject() {
          if (UserInfo == null) return false;
          return IsUserRoleAdmin
            || IsUserRoleTenantAdmin
            || IsUserRoleCoach
            || IsUserRoleClient;
        }

        public static bool CanEditProject(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          if (IsUserRoleLeader) return false;
          return
            IsUserAdmin(projectInfo)
            || IsUserQuoteOwnerInProject(projectInfo)
            || IsUserCreator(projectInfo);
        }

        public static bool CanEditProjectAccess(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          if (IsUserRoleLeader) return false;
          return IsUserAdmin(projectInfo)
            || IsUserCreator(projectInfo)
            || IsUserQuoteOwnerInProject(projectInfo)
            || (IsUserRoleClient && IsUserInProjectAccess(projectInfo));
        }

        public static AppHelper.ParamEnum.UserSearchFilter GetProjectAccessUserSearchFilter() {
          if (UserInfo == null) return AppHelper.ParamEnum.UserSearchFilter.None;
          if (IsUserRoleClient) return AppHelper.ParamEnum.UserSearchFilter.None; // Clients not allowed to search in Project Access
          if (IsUserRoleAdmin) return AppHelper.ParamEnum.UserSearchFilter.All;
          return AppHelper.ParamEnum.UserSearchFilter.TenantOrg;
        }

        public static bool CanChangeTenantOrg() {
          if (UserInfo == null) return false;
          return IsUserRoleAdmin;
        }

        public static bool CanChangeProjectCompany(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          return IsUserAdmin(projectInfo)
            || (IsUserRoleCoach || IsUserCreator(projectInfo));
        }

        public static bool CanEditXeroAccountCode(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          return IsUserRoleAdmin; // TOOD: Change to IsAdmin(projectInfo) when Xero accounts are tenantified.
        }

        public static bool CanEditInvoiceTypeId(DbHelper.Projects.ProjectInfo projectInfo, bool isNewProject) {
          if (UserInfo == null || projectInfo == null) return false;
          if (isNewProject) return false;
          return IsUserAdmin(projectInfo)
            || (IsUserRoleCoach && IsUserCreator(projectInfo));
        }

        public static bool CanViewProjectCustomise(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          return
            IsUserAdminOrProgramManager(projectInfo)
            || IsUserQuoteOwnerInProject(projectInfo);
        }

        public static bool CanViewProjectsLinkToPrograms() => IsUserRoleAdmin; // Q: Tenant admin too?

        public static bool CanUpdateDefaultCostItemMarkupPercent(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          return ConfigHelper.CanUpdateDefaultCostItemMarkupPercent.Exists(x => x == UserInfo.UserId); // Q: This is really only super-admin?
        }

        public static bool CanAllowCostItemPriceOverwrite(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          return ConfigHelper.CanAllowCostItemPriceOverwrite.Exists(x => x == UserInfo.UserId); // Q: This is really only super-admin?
        }

        public static bool CanDisablePaxRegistrationReminders(DbHelper.Projects.ProjectInfo projectInfo)
          => IsUserRoleAdmin;
      }

      public class Quotes {

        public static bool CanCreateQuote() {
          if (UserInfo == null) return false;
          if (IsUserRoleClient || IsUserRoleLeader) return false;
          return IsUserRoleAdmin
            || UserInfo.IsOrgOwner
            || IsUserRoleTenantAdmin
            || UserInfo.IsPLCForAnyProject
            || UserInfo.IsPCForAnyProject;
        }

        public static bool CanCreateQuoteInProject(DbHelper.Projects.ProjectInfo projectInfo) {
          if (UserInfo == null || projectInfo == null) return false;
          return IsUserAdminOrProgramManager(projectInfo);
        }

        public static bool CanCurrentRoleViewQuoteInfo() => IsUserRoleAdmin || IsUserRoleTenantAdmin || IsUserRoleCoach || IsUserRoleClient;

        // TODO: Special handling needed for Tenant Admin viewing the top-level Quotes list (single specfic Project not available).
        public static bool CanViewQuoteInfo(DbHelper.AbleQuotes.QuoteInfo quote) {
          if (UserInfo == null || quote == null) return false;

          // Admin and Owner can always view.
          if (IsUserRoleAdmin) return true; // TODO: need something for tenant admin when viewing top-level quotes list.
          if (UserInfo.UserId == quote.OwnerUserId) return true;

          // Client can see accepted quotes.
          if (IsUserRoleClient) {
            return IsUserInProjectAccess(quote) && quote.IsAccepted;
          }

          // Other users can only view non-Lost quotes.
          if (quote.IsLost) return false;

          if (UserInfo.IsPCorPLCInProject(quote.JobNumber)
            || UserInfo.UserId == quote.ContactUserId) return true;

          // Team members can only view accepted quotes.
          if (quote.IsAccepted) {
            return quote.IsUserTeamMember(UserInfo.UserId)
              || quote.ForUser_IsCoachInProgram;
          }

          return false;
        }

        public static bool CanSignQuote(DbHelper.AbleQuotes.QuoteInfo quoteInfo, Guid? userGuid = null) {
          userGuid = userGuid ?? GetUserInfoOrNull().UserGuid;
          if (userGuid == null || quoteInfo == null) return false;
          return quoteInfo.QuoteTeamUsers.Exists(x => x.UserGuid == userGuid);
        }

        public static bool CanCopyQuote(DbHelper.AbleQuotes.QuoteInfo quoteInfo) {
          if (UserInfo == null || quoteInfo == null) return false;
          if (IsUserRoleClient) return false;
          return IsUserRoleAdmin
            || UserInfo.UserId == quoteInfo.OwnerUserId
            || UserInfo.IsLeadConsultantInProject(quoteInfo.JobNumber);
        }

        public static bool CanEditQuote(DbHelper.AbleQuotes.QuoteInfo quoteInfo) {
          if (UserInfo == null || quoteInfo == null) return false;
          if (IsUserRoleClient) return false;
          if (quoteInfo.IsAccepted) return false;
          return IsUserRoleAdmin
            || UserInfo.UserId == quoteInfo.OwnerUserId
            || UserInfo.IsLeadConsultantInProject(quoteInfo.JobNumber);
        }

        public static bool CanEditQuoteDealSource(DbHelper.AbleQuotes.QuoteInfo quoteInfo) {
          if (UserInfo == null || quoteInfo == null) return false;
          if (IsUserRoleClient) return false;
          if (quoteInfo.IsAccepted) return false;
          return IsUserRoleAdmin
            || UserInfo.UserId == quoteInfo.OwnerUserId
            || UserInfo.IsLeadConsultantInProject(quoteInfo.JobNumber);
        }

        public static bool CanEditQuoteProject(DbHelper.AbleQuotes.QuoteInfo quoteInfo, bool isQuoteFromProjectArea) {
          if (UserInfo == null || quoteInfo == null || IsUserRoleClient) return false;
          return IsUserRoleAdmin || (!quoteInfo.IsAccepted && (UserInfo.UserId == quoteInfo.OwnerUserId || UserInfo.IsLeadConsultantInProject(quoteInfo.JobNumber))) || isQuoteFromProjectArea;
        }

        public static bool CanEditQuoteBranding(DbHelper.AbleQuotes.QuoteInfo quoteInfo, DbHelper.AbleUser.AbleUserInfo userOrNullForCurrent = null) {
          var user = userOrNullForCurrent ?? GetUserInfoOrNull();
          if (user == null || quoteInfo == null) return false;
          return IsUserRoleAdmin && (!quoteInfo.IsAccepted || quoteInfo == null);
        }

        public static bool CanViewQuoteBranding(DbHelper.AbleUser.AbleUserInfo userOrNullForCurrent = null) {
          var user = userOrNullForCurrent ?? GetUserInfoOrNull();
          if (user == null) return false;
          return IsUserRoleAdmin;
        }

        public static bool CanEditStartDate(DbHelper.Projects.ProjectInfo projectInfo)
          => IsUserRoleAdmin
          || (projectInfo == null && IsUserRoleTenantAdmin)
          || (projectInfo != null && IsUserAdmin(projectInfo));

        public static bool CanDeleteQuote(DbHelper.AbleQuotes.QuoteInfo quoteInfo) {
          if (quoteInfo == null) return false;
          return IsUserRoleAdmin && !quoteInfo.HasLockedComponents;
        }

        public static bool CanEditFreshSalesOption() => IsUserRoleAdmin;
        public static bool CanUpdateExcludeFromSalesIncentive() => IsUserRoleAdmin;

        public static bool CanChangeSplitRoles(DbHelper.AbleQuotes.QuoteInfo quoteInfo, DbHelper.AbleUser.AbleUserInfo userOrNullForCurrent = null) {
          var user = userOrNullForCurrent ?? GetUserInfoOrNull();
          if (user == null || quoteInfo == null) return false;
          return IsUserRoleAdmin || (!quoteInfo.IsAccepted && (user.UserId == quoteInfo.OwnerUserId || user.IsLeadConsultantInProject(quoteInfo.JobNumber)));
        }

        public static bool CanSelectContactsFromAllOrgs(DbHelper.AbleUser.AbleUserInfo userOrNullForCurrent = null) {
          var user = userOrNullForCurrent ?? GetUserInfoOrNull();
          if (user == null) return false;
          return user.OrgId == ConfigHelper.IntegralTenantOrgId; // If false, user can only choose from contacts in their own OrgId.
        }

        public static bool CanSelectProjectsFromAllOrgs(DbHelper.AbleUser.AbleUserInfo userOrNullForCurrent = null) {
          var user = userOrNullForCurrent ?? GetUserInfoOrNull();
          if (user == null) return false;
          return user.OrgId == ConfigHelper.IntegralTenantOrgId; // If false, user can only choose from contacts in their own OrgId.
        }

        public static bool CanRequestQuote() => IsUserRoleClient;

        public static bool CanViewQuoteListActiveToggle() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanViewQuoteComponentPrice(DbHelper.AbleQuotes.QuoteInfo quoteInfo) {
          var user = GetUserInfoOrNull();
          if (user == null) return false;
          return IsUserRoleAdmin || user.IsPCorPLCInProject(quoteInfo.JobNumber) || quoteInfo.OwnerUserId == user.UserId || (IsUserRoleClient && user.ClientCompanyId == quoteInfo.CompanyInfo.CompanyId);
        }

        public static bool CanViewQuoteSplits(DbHelper.AbleQuotes.QuoteInfo quoteInfo) {
          var user = GetUserInfoOrNull();
          if (user == null) return false;
          return IsUserRoleAdmin || user.IsPCorPLCInProject(quoteInfo.JobNumber) || (quoteInfo.ContactUserId == user.UserId) || quoteInfo.OwnerUserId == user.UserId;
        }

        public static bool CanEditQuoteSplits(DbHelper.AbleQuotes.QuoteInfo quoteInfo) {
          var user = GetUserInfoOrNull();
          if (user == null || quoteInfo == null || IsUserRoleClient) return false;

          if (quoteInfo.IsAccepted) {
            return IsUserRoleAdmin;
          }

          return IsUserRoleAdmin || user.UserId == quoteInfo.OwnerUserId || user.IsLeadConsultantInProject(quoteInfo.JobNumber);
        }
      }

      public class Insights {

        public static bool CanViewQuality() => IsUserRoleAdmin || (IsUserRoleCoach && UserInfo.OrgId == ConfigHelper.IntegralTenantOrgId);

        public static bool CanCurrentRoleViewIOSReports() => IsUserRoleClient;

        public static bool CanViewSurveyViewer() => IsUserRoleAdmin;

        public static bool CanViewSkillsViewer(DbHelper.Projects.ProjectInfoBrief projectInfoBrief = null)
          => IsUserRoleAdmin || IsUserRoleClient || IsUserPCorPLCInProject(projectInfoBrief?.JobNumber);

        public static bool CanViewEvalViewer() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanViewIOSReports(DbHelper.AlbertSurveys.SurveyInfo survey) {
          if (survey == null) return false;
          if (IsUserRoleAdmin) return true;
          var user = UserInfo;
          if (user == null) return false;
          if (user.ClientCompanyId == survey.CompanyId) return true;
          if (user.ProjectAccessForCompanyIds.Contains(survey.CompanyId ?? 0)) return true;
          return false;
        }
      }

      public class Settings {

        public static bool CanUpdateSettings() {
          if (!TryGetUserInfo(out var userInfo)) return false;
          return IsUserRoleCoach && (userInfo.IsOrgOwner || userInfo.IsTenantOrgAdmin); // Only partners who are org owner or admin.
        }

        public static bool CanUpdateBilling(DbHelper.TenantOrg.TenantOrgInfo orgInfo) {
          if (!TryGetUserInfo(out var userInfo) || orgInfo == null) return false;
          return orgInfo.OrgOwnerUserId == userInfo.UserId;
        }


      }

      public class PageAccess {

        // Base Clases / Top Menus

        public static bool CanAccessProjectLevel() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanAccessProgramLevel() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanAccessQuoteLevel() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanAccessParticipantsList() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanAccessTopLevelParticipantsPageMenu() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanAccessPartnersLevel() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanAccessOrganisationLevel() => Companies.CanViewOrganisationListView();

        public static bool CanAccessContactLevel() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanAccessHelpLevel() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanAccessSurveysLevel() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanAccessParticipantSurveys() => IsUserRoleLeader;

        public static bool CanAccessParticipantCoaching() {

          var user = GetUserInfoOrNull();
          // User must be in "leader" role and have an active coaching participant with a coach assigned.
          if (IsUserRoleLeader && user?.LatestCoachingInfo != null) {

            // Can self assign coach
            if (user.LatestCoachingInfo.CanSelfSelectCoach
              && !user.LatestCoachingInfo.IsCoachAssigned
              && user.LatestCoachingInfo.SessionsAllocated > 0) {
              return true;
            }

            // Can book session
            if (user.LatestCoachingInfo.CoachUserId != ConfigHelper.UserId.Unassigned
              && user.LatestCoachingInfo.CoacheeProgramStatusId > DbHelper.CoacheeProgramStatus.Ids.WaitingLaunch
              && user.LatestCoachingInfo.CoacheeProgramStatusId < DbHelper.CoacheeProgramStatus.Ids.EndProgram) {
              return true;
            }
          }
          return false;
        }

        public static bool CanAccessParticipantAICoach() => Coaches.CanViewAIChat();

        public static bool CanAccessOverviewUpcoming() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanAccessParticipantUpcoming() => IsUserRoleLeader;

        public static bool CanAccessParticipantDevelopment()
          => IsUserRoleLeader
          && UserInfo.LatestCoachingInfo?.CompanyId != null;

        public static bool CanAccessParticipantMenu() => IsUserRoleLeader;

        // Pages

        public static bool CanAccessContentPage() {
          var userInfo = GetUserInfoOrNull();
          if (userInfo == null) return false;
          return IsUserRoleAdmin || IsUserRoleCoach || (userInfo.HasSubscription && IsUserRoleLeader && userInfo.UserSubscription.HasMicrolearnings);
        }

        public static bool CanAccessModuleEdit() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanAccessModule() {
          var userInfo = GetUserInfoOrNull();
          if (userInfo == null) return false;
          return IsUserRoleAdmin || IsUserRoleCoach || (userInfo.HasSubscription && IsUserRoleLeader && userInfo.UserSubscription.HasMicrolearnings);
        }

        // Coach

        public static bool CanAccessPartnerProfile() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient || IsUserRoleLeader;

        public static bool CanAccessPayruns() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanAccessInvitePartner() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanAccessPartnerReferrals() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanAccessPartnerUpcoming() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanAccessCoacheeSendSurvey() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanAccessCoacheeEdit() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        // Participants

        public static bool CanAccessDevelopmentPlan() {
          var userInfo = GetUserInfoOrNull();
          if (userInfo == null) return false;
          return IsUserRoleAdmin || (userInfo.HasSubscription && IsUserRoleLeader && userInfo.UserSubscription.HasDevelopmentPlan);
        }

        // Program

        public static bool CanAccessProgramInformation() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanAccessProgramParticipants() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanAccessProgramConsultingItems() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanAccessProgramCostItems() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanAccessProgramSurveyStatus(DbHelper.AblePrograms.AbleProgramInfo programInfo) {
          if (UserInfo == null || programInfo == null) return false;
          if (IsUserRoleClient) {
            return IsUserInProjectAccess(programInfo)
              || UserInfo.ClientCompanyId == programInfo.CompanyId;
          }
          return IsUserAdminOrProgramManager(programInfo);
        }

        public static bool CanAccessProgramSendSurvey() => IsUserRoleAdmin || IsUserRoleCoach;

        public static bool CanAccessProgramSendEmail() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanAccessProgramContent() => IsUserRoleAdmin || IsUserRoleCoach;

        // Project

        public static bool CanAccessProjectAccess(DbHelper.Projects.ProjectInfo projectInfo) {
          if (projectInfo == null) return false;
          if (IsUserRoleAdmin) return true;
          if (IsUserRoleClient && IsUserInProjectAccess(projectInfo)) return true;
          if (IsUserPCorPLCInProject(projectInfo.JobNumber)) return true;
          return false;
        }

        public static bool CanAccessProjectComponents() => IsUserRoleAdmin || IsUserRoleClient;

        public static bool CanAccessProjectPrograms() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanAccessProjectQuotes() => IsUserRoleAdmin || IsUserRoleCoach || IsUserRoleClient;

        public static bool CanAccessProjectSettings(DbHelper.Projects.ProjectInfo projectInfo) {
          return IsUserAdmin(projectInfo)
            || IsUserRoleCoach
            || IsUserRoleClient
            || IsUserCreator(projectInfo);
        }

        // Admin tools
        public static bool CanAccessAdminTools() {

          string userEmail = GetUserEmailOrNull();

          if (userEmail.IsNullOrEmpty() || ConfigHelper.AdminTools_AllowedUsers.IsNullOrEmpty()) return false;

          if (LoggedInWithAdminTools) {
            // User originally logged in with access to admin tools.
            // This allows user to retain the ability to change identity multiple times
            // without having to log out and back in as admin each time.
            return true;
          }
          // Must be admin and only specific user email addresses.
          return IsUserRoleAdmin && ConfigHelper.AdminTools_AllowedUsers.FindIndex(x => x.Equals(userEmail, StringComparison.OrdinalIgnoreCase)) >= 0;
        }

        public static bool CanViewClientLeadContact(DbHelper.AbleUser.AbleUserInfo userOrNullForCurrent = null) {
          var user = userOrNullForCurrent ?? GetUserInfoOrNull();
          if (user == null) return false;
          return IsUserRoleClient && user.ClientLeadUserId != null;
        }
      }
    }
  }
}
