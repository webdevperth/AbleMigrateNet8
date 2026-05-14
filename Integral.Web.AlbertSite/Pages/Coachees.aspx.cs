using System;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class Coachees : AppCode.PageBaseClasses.LoggedInPageBase {

    public const int Default_Rows_Per_Page = 10;
    public const int Min_Search_Length = 3;
    public const string UrlParam_GetRows = "getrows";
    public bool CanAddParticipant, CanSendInviteToAddParticipants, CanViewParticipants, CanListAllParticipantsInProgram;

    public List<DbHelper.ProjectUserAccess.ProjectAccessInfo> ProgramAccessUsers;
    public string AccessUsersToReceiveForm = "";

    // This page runs in two places - at the top level ("Global")
    // and within a selected Program ("Program").
    public enum PageModeEnum { Global, Program }
    public PageModeEnum PageMode;

    public class AjaxAction {
      public const string SendInvite = "SendInvite";
    }

    public SearchInfo searchInfo = new SearchInfo();

    public class SearchInfo {
      public string SearchTerm { get; set; }
      public int RowsPerPage { get; set; }
      public int GetPage { get; set; }
      public bool StatusEndProgram { get; set; }
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Participants";

      searchInfo.SearchTerm = (WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeSearchTerm) ?? SessionHelper.AppState.Coachees.Search_SearchTerm ?? "").URLDecode().TrimWhitespace();

      if (!WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeStatusToggle).IsNullOrEmpty()) {
        searchInfo.StatusEndProgram = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeStatusToggle) == PathHelper.AbleUrlValues.CoacheeStatusToggleValue_EndProgram;
      } else {
        searchInfo.StatusEndProgram = SessionHelper.AppState.Coachees.Search_StatusEndProgram;
      }

      searchInfo.GetPage = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.GetPage).ToIntOrDefault(SessionHelper.AppState.Coachees.Search_CurrentPage.GetValueOrDefault(1));
      searchInfo.RowsPerPage = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PerPage).ToIntOrDefault(Default_Rows_Per_Page);

      // Check if URL contains ProgramId
      int programJobId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.ProgramJobId) ?? 0;

      if (programJobId > 0) {

        ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(programJobId,
          SessionHelper.AppAccess.Programs.CanViewAllProjectPrograms()
            ? DbHelper.AblePrograms.WhereRelatedUserIs.NoCheck
            : DbHelper.AblePrograms.WhereRelatedUserIs.Tenant_AnyRelated,
          SessionHelper.UserInfo);
        if (ProgramInfo == null) {
          RedirectNoAccess(); // Given program id was not found.
          return;
        }

        bool CanListProgramParticipants = SessionHelper.AppAccess.Programs.CanListProgramParticipants(ProgramInfo);
        if (!CanListProgramParticipants) {
          RedirectNoAccess(); // User has no access to Program
          return;
        }

        PageMode = PageModeEnum.Program;
        GetProgramFilter();

      } else {

        PageMode = PageModeEnum.Global;
      }

      SetViewPermissions();

      // If querystring includes [UrlParam_GetRows] that indicates we just want the json participant list results, not the page html.
      if (!WebHelper.GetQueryStringValue(UrlParam_GetRows).IsNullOrEmpty()) {
        WebHelper.WriteAndEnd(GetParticipantListJson(), WebHelper.HttpContentType.json);
        return;
      }

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          if (ajax.Action == AjaxAction.SendInvite) {
            // Check if user is allowed to send the participant form.
            if (!CanSendInviteToAddParticipants) {
              ajax.RespondNoAccessToFunction();
              return;
            } else {
              SendInviteToAddParticipants(ajax);
            }
          }
        });

        return;

      }
    }

    void RedirectNoAccess() {
      if (SessionHelper.AppAccess.PageAccess.CanAccessTopLevelParticipantsPageMenu()) {
        SetRedirect(PathHelper.Pages.Coachees());
      } else {
        SetRedirect(PathHelper.Pages.Projects());
      }
    }

    void GetProgramFilter() {

      ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(ProgramInfo.ProgramJobNumber);
      ProgramAccessUsers = DbHelper.ProjectUserAccess.GetProjectAccessUsers(ProgramInfo.ProgramJobNumber);

      if (ProgramAccessUsers.Count > 0) AccessUsersToReceiveForm = " to ";
      for (int i = 0; i < ProgramAccessUsers.Count; i++) {
        string separator = "";
        if (ProgramAccessUsers.Count - 1 - i > 1) {
          separator = ", ";
        } else if (ProgramAccessUsers.Count - 1 - i == 1) {
          separator = " and ";
        }
        AccessUsersToReceiveForm += "<b>" + ProgramAccessUsers[i].FirstName + " " + ProgramAccessUsers[i].LastName + "</b>" + separator;
      }
    }

    void SetViewPermissions() {

      if (PageMode == PageModeEnum.Program) {

        CanAddParticipant = SessionHelper.AppAccess.Programs.CanAddProgramParticipant(ProgramInfo);
        CanSendInviteToAddParticipants = SessionHelper.AppAccess.Programs.CanSendInviteToAddParticipants(ProgramInfo, ProgramAccessUsers);
        CanViewParticipants = SessionHelper.AppAccess.Programs.CanViewProgramParticipants(ProgramInfo) && !SessionHelper.IsUserRoleClient;
        CanListAllParticipantsInProgram = SessionHelper.AppAccess.Programs.CanListAllParticipants(ProgramInfo);
        MenuThirdLayerActive_Programs = ProjectMenuIsActive = true;

      } else {

        CanAddParticipant = SessionHelper.AppAccess.Participants.CanAdd();
        CanViewParticipants = true; // Top level pax list, can view all.
      }
    }

    void SendInviteToAddParticipants(AjaxSubmitHelper ajax) {

      // Essential checks.
      if (ProgramInfo.ProjectCoordinatorUserId == null) {
        ajax.AddDialogMessage("To send invite, a Project Coordinator must be assigned.");
        return;
      }
      if (ProgramAccessUsers.IsNullOrEmpty()) {
        ajax.AddDialogMessage("To send invite, a client user must have access to the program.");
        return;
      }

      int invitesSent = 0, failedToSend = 0, missingContactDetails = 0;

      foreach (var user in ProgramAccessUsers) {

        var programAccessUser = DbHelper.AbleUser.GetBasicInfoById(user.UserId, DbHelper.AbleUser.RegisteredFilter.Any); // Include unregistered users.

        if (programAccessUser.FirstName.IsNullOrEmpty()
        || programAccessUser.LastName.IsNullOrEmpty()
        || programAccessUser.EmailAddress.IsNullOrEmpty()) {
          missingContactDetails++;
          continue;
        }

        bool emailSent = false;
        int invitedByUserId;

        // If user was alreayd invited by someone, keep that inviter otheriwse use current user.
        if (programAccessUser.InvitedByUserId != null) {
          invitedByUserId = programAccessUser.InvitedByUserId.Value;
        } else {
          invitedByUserId = userInfo.UserId;
        }

        // Create invite code if necessary.
        if (programAccessUser.InviteCode.IsNullOrEmpty()) {
          // Generate Invite code.
          try {
            DbHelper.AbleUser.UpdateInviteDetails(programAccessUser, invitedByUserId);
          } catch (Exception) {
            failedToSend++;
            continue;
          }
        }

        try {
          emailSent = AlbertEmails.SendClientInviteToAddParticipants(programAccessUser, ProgramInfo);
        } catch (Exception) { }

        if (!emailSent) {
          failedToSend++;
          continue;
        }

        invitesSent++;
        // Record in db that it was sent.
        try {
          DbHelper.AblePrograms.UpdateParticipantFormEmailSent(ProgramInfo.ProgramJobId, DateTime.UtcNow);
        } catch (Exception) { } // ignore any error, not important
      }

      string msg = "";
      if (invitesSent > 0) {
        msg += $"Invite to add participant has been sent to {invitesSent} client{(invitesSent > 1 ? "s" : "")}.";
      }
      if (missingContactDetails > 0) {
        msg += $"<br/><br/>{missingContactDetails} client{(missingContactDetails > 1 ? "s are" : " is")} missing contact details.";
      }
      if (failedToSend > 0) {
        msg += $"<br/><br/>Failed to send invitation link to {failedToSend} client{(failedToSend > 1 ? "s" : "")}.";
      }

      // Reload page so the popup shows the correct info after sending.
      ajax.SetReloadPage(msg);
    }

    public string GetRowLinkUrl() {
      return PathHelper.Pages.CoacheeEdit(PathHelper.CoacheeTabEnum.summary, null);
    }

    string GetParticipantListJson() {

      DbHelper.AlbertCoachees.AbleCoacheeList coacheeList;
      var results = new CoacheeSearchResults();
      results.RowsPerPage = searchInfo.RowsPerPage;
      results.CurrentPage = searchInfo.GetPage;
      results.SearchTerm = searchInfo.SearchTerm;
      results.StatusEndProgram = searchInfo.StatusEndProgram;

      // Save UI state in session.
      // Note: These same filters will be appled when user views the list in the Programs area
      //       and at the top level. Might be a good idea to have separate filters eventually.
      SessionHelper.AppState.Coachees.Search_SearchTerm = searchInfo.SearchTerm;
      SessionHelper.AppState.Coachees.Search_CurrentPage = searchInfo.GetPage;
      SessionHelper.AppState.Coachees.Search_StatusEndProgram = searchInfo.StatusEndProgram;

      coacheeList = DbHelper.AlbertCoachees.GetCoacheeList(GetCoacheeListOptions());

      if (coacheeList != null) results.SetResults(coacheeList, PageMode);
      ShowEmptyStateHtml(results);

      return JsonConvert.SerializeObject(results);
    }

    private void ShowEmptyStateHtml(CoacheeSearchResults results) {

      bool foundCoachees = results.results.TotalRows > 0;
      bool isLookingForCoachee = !results.SearchTerm.IsNullOrEmpty();
      bool showEmptyStateHtml = false;

      // Do not show empty status if searching for something or there's results.
      if (isLookingForCoachee || foundCoachees) return;

      // If passes the sentence above, means there's no results.
      if (PageMode == PageModeEnum.Program) {
        // Show empty status and hide current html if there's no results in program.
        showEmptyStateHtml = !foundCoachees;
      } else {
        // If page is in General PageMode
        if (SessionHelper.IsUserRoleAdmin) {
          // To admins: Show empty state, but keep the html so they can browse.
          showEmptyStateHtml = true;
        } else {
          // Check if user has any coachees in any status.
          // If they don't have any status at all, they hide their current html.
          // If they have Coachees, just not in this status. Keep the current html but show the empty status too.
          bool userHasAnyCoachees = DbHelper.AlbertCoachees.UserHasAnyCoachees(SessionHelper.GetUserInfoOrNull(), SessionHelper.GetUserRole());
          if (userHasAnyCoachees && !foundCoachees) {
            showEmptyStateHtml = true;
          } else {
            showEmptyStateHtml = !userHasAnyCoachees;
          }
        }
      }

      results.ShowEmptyStateHtml = showEmptyStateHtml;
    }

    // Determine the options for the participant list, based on user role and UI state.
    DbHelper.AlbertCoachees.GetCoacheeListOptions GetCoacheeListOptions() {

      // By default, no filters are applied. Admins can list all participants.
      var coacheeListOptions = new DbHelper.AlbertCoachees.GetCoacheeListOptions();

      if (!SessionHelper.IsUserRoleAdmin) {
        coacheeListOptions.TenantOrgId = SessionHelper.UserInfo.OrgId;
      }

      if (PageMode == PageModeEnum.Program) { // Page is running in the Program area.

        coacheeListOptions.ProgramJobId = ProgramInfo.ProgramJobId; // Only list participants in the current program.
        coacheeListOptions.StatusEndProgram = null; // Don't use this filter. Show all statuses.
        coacheeListOptions.OffsetRows = 0; // Prevent paging and display all results for the current program in the same page.

        if (!CanListAllParticipantsInProgram) {
          coacheeListOptions.CoachUserId = SessionHelper.UserInfo.UserId; // Restrict to Coach being the current user.
        }

      } else { // Page is running at the top level (not within a program).

        // If no search term given, default view (whether Admin or not) is just own coachees.
        if (searchInfo.SearchTerm.IsNullOrEmpty()) {

          // Tenant admins default to seeing all pax, otherwise default is just your own coachees.
          // In future, tenants may have too many pax for this default to be practical, but is best for now.
          if (!SessionHelper.IsUserRoleTenantAdmin) {
            coacheeListOptions.CoachUserId = SessionHelper.UserInfo.UserId;
          }

        } else {

          // If Admin is doing a search, don't apply any restrictions at all - i.e. it is a "global search".
          // If not admin, search is restricted to coachees OR to the projects where user is PC/PLC.
          if (!SessionHelper.IsUserRoleAdmin && !SessionHelper.IsUserRoleTenantAdmin) {
            coacheeListOptions.CoachUserId = SessionHelper.GetUserIdOrNull(); // Restrict to Coach being the current user.
            coacheeListOptions.UserIsProgramManager = true;
            coacheeListOptions.PCForJobNumbers = userInfo.PCForJobNumbers;
            coacheeListOptions.PLCForJobNumbers = userInfo.PLCForJobNumbers;
          }

        }

        // Apply UI status filters & paging.
        coacheeListOptions.StatusEndProgram = searchInfo.StatusEndProgram;  // Filter by End-Program status, if selected.
        coacheeListOptions.OffsetRows = Math.Max(searchInfo.GetPage - 1, 0) * searchInfo.RowsPerPage;
        coacheeListOptions.FetchRows = searchInfo.RowsPerPage;
      }

      // Apply UI filters & paging.
      coacheeListOptions.SearchTerm = searchInfo.SearchTerm;              // Filter by search text, if any.

      return coacheeListOptions;
    }

    public string GetEmptyStatePageHtml() {

      string stateTitle = "Participants";

      if (PageMode == PageModeEnum.Program) {

        return WebHelper.GetEmptyStatePageHtml(
        title: stateTitle,
        description: $"There's no participants {(PageMode == PageModeEnum.Program ? "in the program" : "")} yet. {(CanAddParticipant ? "<br/>Add the first one!" : "")}",
        addActionHtml: CanAddParticipant,
        customActionHtml: CanAddParticipant ? WebHelper.GetAddParticipantDropdownButton(WebHelper.AddParticipantFrom.Program, ProgramInfo.ProgramJobId, CanSendInviteToAddParticipants) : "");

      } else {
        return WebHelper.GetEmptyStatePageHtml(
        title: stateTitle,
        description: $"You have no participants yet. {(CanAddParticipant ? "Add the first one!" : "")}",
        addActionHtml: CanAddParticipant,
        customActionHtml: CanAddParticipant ? "<button class=\"btn btn-primary floatright ml10 btnAddParticipant\">New Participant</button>" : "");
      }
    }

    class CoacheeSearchResults {

      public int? RowsPerPage { get; set; }
      public int? CurrentPage { get; set; }
      public string SearchTerm { get; set; }
      public bool StatusEndProgram { get; set; }
      public bool ShowEmptyStateHtml { get; set; }
      public SearchResults results { get; private set; }

      public CoacheeSearchResults() {
        results = new SearchResults();
      }
      public void SetResults(DbHelper.AlbertCoachees.AbleCoacheeList newResults, PageModeEnum pageMode) {
        results.OffsetRows = newResults.OffsetRows;
        results.FetchRows = newResults.FetchRows;
        results.TotalRows = newResults.TotalRows;
        results.ClearCoacheeInfo();
        foreach (var ci in newResults.CoacheeInfoList) results.AddCoacheeInfo(ci, pageMode);
      }
      public class SearchResults {
        public int? OffsetRows { get; internal set; }
        public int? FetchRows { get; internal set; }
        public int TotalRows { get; internal set; }
        public List<CoacheeInfo> CoacheeInfoList { get; private set; }
        public SearchResults() {
          CoacheeInfoList = new List<CoacheeInfo>();
        }
        public void ClearCoacheeInfo() {
          CoacheeInfoList.Clear();
        }
        public void AddCoacheeInfo(DbHelper.AlbertCoachees.CoacheeListItem ci, PageModeEnum pageMode) {
          CoacheeInfoList.Add(new CoacheeInfo() {
            CoacheeId = ci.CoacheeId,
            CoacheeName = GetCoacheeName(ci, pageMode),
            LocatorColumn = WebHelper.GetListViewLocatorColumnHtml(ci.ProjectName, ci.ProgramJobNumber, ci.CompanyName),
            CoachId = ci?.UserActivity?.CoachUserId,
            CoachName = GetCoachName(ci), // html
            CoachingType = GetDeliveryType(ci?.UserActivity?.CoachingTypeId), // html
            ProgramJobId = ci.ProgramJobId,
            ProgramName = ci.ProgramJobId == null ? "" : ci.ProgramJobNumber + ": " + ci.ProgramJobName,
            CoacheeSessions = GetCoacheeSessionsBar(ci), //html
            CoacheeUserActivity = WebHelper.ParticipantActivities.GetBadgeParticipantUserActivityInfo(ci.UserActivity, ci.UserSubscription), //html
            CoacheeRowAttribute = GetRowAttribute(ci)
          });
        }

        public string GetRowAttribute(DbHelper.AlbertCoachees.CoacheeListItem cli) {
          // TODO: AppAccess.Participants.CanViewParticipantProfile needs to take CoacheeListItem, which needs to have relevant tenant info.
          if (SessionHelper.AppAccess.Participants.CanViewParticipantProfile(cli)) {
            return $"data-rowlink-id=\"{cli.CoacheeId}\"";
          } else {
            return WebHelper.GetSlideoutTriggerDataAttributes("Participant Details", PathHelper.Partials.ParticipantSlideoutPanel(cli.CoacheeId));
          }
        }

        public string GetCoacheeName(DbHelper.AlbertCoachees.CoacheeListItem ci, PageModeEnum pageMode) {

          string programName = pageMode != PageModeEnum.Global ? "" : ci.ProgramJobName;

          return WebHelper.GetAvatarForTable_Participant(PathHelper.Images.UserPhoto(ci.FirstName, ci.LastName, PathHelper.Images.UserPhotoSize.Thumbnail, true), ci.FullName, programName, ci.CoacheeId);
        }

        public string GetCoachName(DbHelper.AlbertCoachees.CoacheeListItem ci) {

          return WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(ci, PathHelper.Images.UserPhotoSize.Thumbnail, true), ci?.UserActivity?.CoachFirstName + " " + ci?.UserActivity?.CoachLastName, ci?.UserActivity?.CoachUserId);
        }

        public string GetDeliveryType(int? CoachingTypeId) {

          if (CoachingTypeId == null) return "";

          bool inPerson = DbHelper.AlbertCoachingTypes.GetCoachingTypeById(CoachingTypeId.Value).SessionTypeInPerson;

          return WebHelper.GetDeliveryBadge(inPerson);
        }

        public string GetCoacheeSessionsBar(DbHelper.AlbertCoachees.CoacheeListItem ci) {
          if (ci == null || ci.UserActivity == null) return "";
          return WebHelper.GetProgressBarHtml(ci.UserActivity.SessionsCompleted, ci.UserActivity.SessionsAllocated);
        }

        public class CoacheeInfo {
          public int CoacheeId { get; set; }
          public string CoacheeName { get; set; }
          public string LocatorColumn { get; set; }
          public int? CoachId { get; set; }
          public string CoachName { get; set; }
          public string CoachingType { get; set; }
          public int? ProgramJobId { get; set; }
          public string ProgramName { get; set; }
          public string CoacheeSessions { get; set; }
          public string CoacheeUserActivity { get; set; }
          public string CoacheeRowAttribute { get; set; }
        }
      }

      public class Pagination {
        public bool more { get; set; }
        public Pagination() {
          more = false;
        }
      }
    }
  }
}
