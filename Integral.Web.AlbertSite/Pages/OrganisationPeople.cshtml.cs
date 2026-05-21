using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class OrganisationPeople : AppCode.PageBaseClasses.CompanyInfoBase {

    public const int Default_Rows_Per_Page = 10;
    public const int Min_Search_Length = 3;
    public const string UrlParam_GetRows = "getrows";
    public bool CanViewParticipants, CanListAllParticipantsInProgram, CanAddCompanyParticipants, CanViewPeopleDetails;

    public List<DbHelper.ProjectUserAccess.ProjectAccessInfo> ProgramAccessUsers;
    public string AccessUsersToReceiveForm = "";

    public class AjaxAction {
      public const string Search = "Search";
      public const string SendInvite = "SendInvite";
    }

    public SearchInfo searchInfo = new SearchInfo();

    public class SearchInfo {
      public string SearchTerm { get; set; }
      public int RowsPerPage { get; set; }
      public int GetPage { get; set; }
      public bool StatusInactive { get; set; }
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Organisation People";

      if (!SessionHelper.AppAccess.Companies.CanViewOrganisationPeople(CompanyInfo)) {
        SetFallbackRedirectNoAccess();
        return new EmptyResult();
      }

      CanAddCompanyParticipants = SessionHelper.AppAccess.Companies.CanAddCompanyParticipants(CompanyInfo);
      CanViewPeopleDetails = SessionHelper.AppAccess.Reports.CanViewPeopleDetails();

      searchInfo.SearchTerm = (WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.OrgPeopleSearchTerm) ?? SessionHelper.AppState.OrganisationPeople.Search_SearchTerm ?? "").URLDecode().TrimWhitespace();

      if (!WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.OrgPeopleStatusToggle).IsNullOrEmpty()) {
        searchInfo.StatusInactive = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.OrgPeopleStatusToggle) == PathHelper.AbleUrlValues.OrganisationPeople_Inactive;
      } else {
        searchInfo.StatusInactive = SessionHelper.AppState.OrganisationPeople.Search_StatusInactive;
      }

      searchInfo.GetPage = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.GetPage).ToIntOrDefault(SessionHelper.AppState.OrganisationPeople.Search_CurrentPage.GetValueOrDefault(1));
      searchInfo.RowsPerPage = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PerPage).ToIntOrDefault(Default_Rows_Per_Page);

      if (!WebHelper.GetQueryStringValue(UrlParam_GetRows).IsNullOrEmpty()) {
        DoPeopleSearch();
        return new EmptyResult();
      }

      return Page();
    }

    public string GetRowLinkUrl() {
      return PathHelper.Pages.PeopleDetails(CompanyInfo.CompanyId, null);
    }

    private void DoPeopleSearch() {

      var results = new PeopleSearchResults();
      results.RowsPerPage = searchInfo.RowsPerPage;
      results.CurrentPage = searchInfo.GetPage;
      results.SearchTerm = searchInfo.SearchTerm;
      results.StatusInactive = searchInfo.StatusInactive;

      SessionHelper.AppState.OrganisationPeople.Search_SearchTerm = searchInfo.SearchTerm;
      SessionHelper.AppState.OrganisationPeople.Search_CurrentPage = searchInfo.GetPage;
      SessionHelper.AppState.OrganisationPeople.Search_StatusInactive = searchInfo.StatusInactive;

      int fetchRows = searchInfo.RowsPerPage;
      int offsetRows = (searchInfo.GetPage - 1) * fetchRows;

      var searchResults = DbHelper.OrganisationUsers.GetOrganisationUserList_BySearchTerm(CompanyInfo.CompanyId, searchInfo.SearchTerm, searchInfo.StatusInactive, offsetRows, fetchRows);

      if (searchResults != null) results.SetResults(searchResults);

      WebHelper.WriteAndEnd(JsonConvert.SerializeObject(results), WebHelper.HttpContentType.json);
    }

    class PeopleSearchResults {

      public int? RowsPerPage { get; set; }
      public int? CurrentPage { get; set; }
      public string SearchTerm { get; set; }
      public bool StatusInactive { get; set; }
      public SearchResults results { get; private set; }

      public PeopleSearchResults() {
        results = new SearchResults();
      }

      public void SetResults(DbHelper.OrganisationUsers.OrganisationUserInfo newResults) {

        results.OffsetRows = newResults.OffsetRows;
        results.FetchRows = newResults.FetchRows;
        results.TotalRows = newResults.TotalRows;
        results.ClearOrgUsersInfo();

        foreach (var u in newResults.OrganisationUserInfoList) {
          results.AddOrgUserInfo(u);
        }
      }

      public class SearchResults {

        public int? OffsetRows { get; set; }
        public int? FetchRows { get; set; }
        public int TotalRows { get; set; }

        public List<OrganisationUserInfo> OrgUsersInfoList { get; private set; }

        public SearchResults() {
          OrgUsersInfoList = new List<OrganisationUserInfo>();
        }

        public void AddOrgUserInfo(DbHelper.OrganisationUsers.OrgUserInfo orgUser) {

          OrgUsersInfoList.Add(new OrganisationUserInfo(
            orgUser.UserGuid,
            GetNameDisplay(orgUser),
            GetSubscriptionType(orgUser),
            GetCoachingSessionsBar(orgUser),
            GetWorkshopSessionsBar(orgUser),
            WebHelper.DisplayDate(orgUser?.UserActivity?.Latest360CompletedUtc, "-"),
            WebHelper.DisplayDate(orgUser?.UserActivity?.LatestEvalCompletedUtc, "-"),
            WebHelper.ParticipantActivities.GetBadgeParticipantUserActivityInfo(orgUser.UserActivity, orgUser.UserSubscription),
            GetRowAttribute(orgUser)
          ));
        }

        public string GetRowAttribute(DbHelper.OrganisationUsers.OrgUserInfo orgUser) {
          if (SessionHelper.AppAccess.Reports.CanViewPeopleDetails(orgUser)) {
            return $"data-rowlink-id=\"{orgUser.UserGuid}\"";
          } else {
            return WebHelper.GetSlideoutTriggerDataAttributes("Participant Details", PathHelper.Partials.ParticipantSlideoutPanel(orgUser.UserActivity.LatestCoacheeId));
          }
        }

        private string GetNameDisplay(DbHelper.OrganisationUsers.OrgUserInfo orgUser) {
          return WebHelper.GetAvatarForTable_Participant(PathHelper.Images.UserPhoto(orgUser.FirstName, orgUser.LastName, PathHelper.Images.UserPhotoSize.Thumbnail, true), orgUser.FullName, orgUser.Email, orgUser.UserActivity.LatestCoacheeId);
        }

        private string GetSubscriptionType(DbHelper.OrganisationUsers.OrgUserInfo orgUser) {

          if (orgUser.UserSubscription == null) return "No Subscription";

          return $@"
            <div class=""flex-inline"">
              {orgUser.UserSubscription.SubscriptionName}
              {WebHelper.GetPartnerStatusIcon(orgUser.UserSubscription != null, "ml5")}
            </div>";
        }

        public string GetCoachingSessionsBar(DbHelper.OrganisationUsers.OrgUserInfo orgUser) {
          return WebHelper.GetProgressBarHtml(orgUser.CoachingSessionsCompleted, orgUser.CoachingSessionsAllocated);
        }

        public string GetWorkshopSessionsBar(DbHelper.OrganisationUsers.OrgUserInfo orgUser) {
          return WebHelper.GetProgressBarHtml(orgUser.UserActivity.WorkshopsAttended, orgUser.UserActivity.WorkshopsAllocated);
        }

        public void ClearOrgUsersInfo() {
          OrgUsersInfoList.Clear();
        }

        public class OrganisationUserInfo {

          public Guid UserGuid { get; private set; }
          public string NameDisplay { get; private set; }
          public string SubscriptionType { get; private set; }
          public string CoachingSessionsProgressBar { get; private set; }
          public string WorkshopsProgressBar { get; private set; }
          public string Latest360Completed { get; private set; }
          public string LatestEvalCompleted { get; private set; }
          public string UserActivityInfo { get; private set; }
          public string CoacheeRowAttribute { get; set; }

          public OrganisationUserInfo(
            Guid userGuid,
            string nameDisplay,
            string subscriptionType,
            string coachingSessionsProgressBar,
            string workshopsProgressBar,
            string latest360Completed,
            string latestEvalCompleted,
            string userActivityInfo,
            string coacheeRowAttribute
          ) {
            this.UserGuid = userGuid;
            this.NameDisplay = nameDisplay;
            this.SubscriptionType = subscriptionType;
            this.CoachingSessionsProgressBar = coachingSessionsProgressBar;
            this.WorkshopsProgressBar = workshopsProgressBar;
            this.Latest360Completed = latest360Completed;
            this.LatestEvalCompleted = latestEvalCompleted;
            this.UserActivityInfo = userActivityInfo;
            this.CoacheeRowAttribute = coacheeRowAttribute;
          }

        }
      }
    }
  }
}
