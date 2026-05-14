using System;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class Coaches : AppCode.PageBaseClasses.LoggedInPageBase {

    public DbHelper.AlbertCoaches.UserListViewInfoPaged CoachInfoList;
    public DbHelper.PartnerTags.AllTagInfo AllTagInfo;
    public List<int> SelectedTagIds;
    public SearchInfo searchInfo = new SearchInfo();
    public const int Min_Search_Length = 4;
    public const int Default_Rows_Per_Page = 50;
    public bool CanViewStatusToggle = false;
    public bool CanViewHiddenPartners { get; protected set; } = false;
    public bool CanViewInactivePartners { get; protected set; } = false;
    public bool CanViewAllUsers { get; protected set; } = false;

    public class SearchInfo {
      public string SearchTerm { get; set; }
      public bool HasSearchTerm { get; set; }
      public bool StatusInactive { get; set; }
      public List<int> SelectedTagIds { get; set; }
      public int RowsPerPage { get; set; }
      public int GetPage { get; set; }
    }
    public DbHelper.AlbertCoaches.SearchModeEnum SearchMode;

    public class DataAttr {
      public const string SearchMode = "searchmode";
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Partners";

      AllTagInfo = DbHelper.PartnerTags.GetAllTags(new DbHelper.PartnerTags.GetAllTagsParams() { OnlyPartnerTags = true });
      CanViewStatusToggle = SessionHelper.AppAccess.Coaches.CanViewStatusToggle();
      CanViewHiddenPartners = SessionHelper.AppAccess.Coaches.CanViewHiddenPartners();
      CanViewInactivePartners = SessionHelper.AppAccess.Coaches.CanViewInactivePartners();
      CanViewAllUsers = SessionHelper.AppAccess.Coaches.CanViewAllUsers();

      searchInfo.SearchTerm = (WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartnerSearchTerm) ?? SessionHelper.AppState.CoachList.Search_SearchFor ?? "").URLDecode().TrimWhitespace();
      searchInfo.HasSearchTerm = !searchInfo.SearchTerm.IsNullOrEmpty();
      if (!WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartnerStatusToggle).IsNullOrEmpty()) {
        searchInfo.StatusInactive = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartnerStatusToggle) == PathHelper.AbleUrlValues.PartnerStatus_Inactive;
      } else {
        searchInfo.StatusInactive = SessionHelper.AppState.CoachList.Search_StatusInactive;
      }
      searchInfo.GetPage = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.GetPage).ToIntOrDefault(SessionHelper.AppState.CoachList.Search_CurrentPage.GetValueOrDefault(1));
      searchInfo.RowsPerPage = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PerPage).ToIntOrDefault(Default_Rows_Per_Page);
      if (!CanViewStatusToggle) searchInfo.StatusInactive = false; // Force to always display active
      searchInfo.SelectedTagIds = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartnerTagIds).ToIntList() ?? SessionHelper.AppState.CoachList.GetSelectedTagIds();

      if (!string.IsNullOrEmpty(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartnerSearchMode)) && CanViewAllUsers) {
        if (Enum.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartnerSearchMode), out DbHelper.AlbertCoaches.SearchModeEnum thisSearchMode)) {
          SearchMode = thisSearchMode;
        } else {
          SearchMode = DbHelper.AlbertCoaches.SearchModeEnum.SearchPartners;
        }
      } else {
        SearchMode = DbHelper.AlbertCoaches.SearchModeEnum.SearchPartners;
      }

      if (WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartnerSearchTerm) != null) {
        DoPartnerSearch();
        return;
      }
    }

    private void DoPartnerSearch() {

      int fetchRows = searchInfo.RowsPerPage;
      int offsetRows = (searchInfo.GetPage - 1) * fetchRows;

      CoachInfoList = new DbHelper.AlbertCoaches.UserListViewInfoPaged();
      var results = new UserSearchResults();
      results.RowsPerPage = searchInfo.RowsPerPage;
      results.CurrentPage = searchInfo.GetPage;
      results.SearchTerm = searchInfo.SearchTerm;
      results.StatusInactive = searchInfo.StatusInactive;

      SessionHelper.AppState.CoachList.Search_SearchFor = searchInfo.SearchTerm;
      SessionHelper.AppState.CoachList.Search_CurrentPage = searchInfo.GetPage;
      SessionHelper.AppState.CoachList.Search_StatusInactive = searchInfo.StatusInactive;
      SessionHelper.AppState.CoachList.SetSelectedTagIds(searchInfo.SelectedTagIds);
      SelectedTagIds = searchInfo.SelectedTagIds;

      // User might get cut off of selection because of the rows per page indication, this will be handled in the dto.
      // If Partner's Activity is the same as being looked for, then always include.
      bool includePartnerInSelection = !searchInfo.StatusInactive == userInfo.IsPartnerActive;

      CoachInfoList = DbHelper.AlbertCoaches.GetCoachInfoList_BySearch(
        searchTerm: searchInfo.SearchTerm,
        statusInactive: searchInfo.StatusInactive,
        CanViewHiddenPartners: SessionHelper.AppAccess.Coaches.CanViewHiddenPartners(),
        includePartnerInSelection: includePartnerInSelection,
        userId: userInfo.UserId,
        orgId: null,
        includeUnassigned: false,
        includeUnregistered: DbHelper.AbleUser.RegisteredFilter.OnlyRegistered,
        searchMode: SearchMode,
        offsetRows: offsetRows,
        fetchRows: fetchRows,
        partnerTagIds: SelectedTagIds);
      LogHelper.ResponseLog($"{LogHelper.GetLatestSqlQueryText()}");
      PutCurrentUsernameAtTheTop();

      if (CoachInfoList != null) results.SetResults(CoachInfoList);

      WebHelper.WriteAndEnd(JsonConvert.SerializeObject(results), WebHelper.HttpContentType.json);
      return;

    }

    public string GetTagSelected(DbHelper.PartnerTags.TagInfo tag) {
      if (!SelectedTagIds.IsNullOrEmpty() && SelectedTagIds.Contains(tag.TagId)) return "selected";
      return "";
    }

    private void PutCurrentUsernameAtTheTop() {
      if (CoachInfoList == null) return;
      // Put current user's name at the top of the list.
      for (int i = 0; i < CoachInfoList.UserListInfo.Count; i++) {
        var item = CoachInfoList.UserListInfo[i];
        if (item.UserId == userInfo.UserId) {
          CoachInfoList.UserListInfo.Insert(0, item);
          CoachInfoList.UserListInfo.RemoveAt(i + 1);
          break;
        }
      }
    }

    public string GetDefaultRowUrl() {
      // The list row for the logged-in user will link to the Upcoming page.
      return PathHelper.Pages.CoachEdit(null);
    }

    class UserSearchResults {

      public int? RowsPerPage { get; set; }
      public int? CurrentPage { get; set; }
      public string SearchTerm { get; set; }
      public bool StatusInactive { get; set; }
      public SearchResults results { get; private set; }

      public UserSearchResults() {
        results = new SearchResults();
      }
      public void SetResults(DbHelper.AlbertCoaches.UserListViewInfoPaged newResults) {
        results.OffsetRows = newResults.OffsetRows;
        results.FetchRows = newResults.FetchRows;
        results.TotalRows = newResults.TotalRows;
        results.ClearProgramInfo();
        foreach (var ci in newResults.UserListInfo) results.AddCoachInfo(ci);
      }
      public class SearchResults {
        public int? OffsetRows { get; internal set; }
        public int? FetchRows { get; internal set; }
        public int TotalRows { get; internal set; }
        public List<CoachInfo> CoachInfoList { get; private set; }
        public SearchResults() {
          CoachInfoList = new List<CoachInfo>();
        }
        public void ClearProgramInfo() {
          CoachInfoList.Clear();
        }

        public string GetJobNumberHtml(string jobNumber) {
          if (!SessionHelper.AppAccess.Projects.CanViewProjectsLinkToPrograms()) return jobNumber;
          return "<a href=\"" + PathHelper.Pages.ProjectPrograms(jobNumber) + "\">" + jobNumber + "</a>";
        }

        public void AddCoachInfo(DbHelper.AlbertCoaches.UserListInfo uli) {
          CoachInfoList.Add(new CoachInfo() {
            UserId = uli.UserId,
            PartnerName = WebHelper.GetAvatarForTable_User(
              profileImagePath: PathHelper.Images.UserPhoto(uli.FirstName,
              uli.LastName, PathHelper.Images.UserPhotoSize.Thumbnail, true),
              profileName: uli.GetFullName(),
              userId: uli.UserId,
              noSlideoutTrigger: !uli.IsAbleCoach),
            CompanyName = uli.OrgName,
            CoachTags = WebHelper.GetCoachTagsHtml(uli.TagIdList),
            PartnerHiddenIcon = WebHelper.GetPartnerHiddenIcon(uli.IsProfileHidden, SessionHelper.AppAccess.Coaches.CanViewHiddenPartners()),
            UserTooltipHtml = WebHelper.GetUserTooltipInfoHtml(uli)
          });
        }
        public class CoachInfo {
          public int UserId { get; set; }
          public string PartnerName { get; set; }
          public string CompanyName { get; set; }
          public string CoachTags { get; set; }
          public string PartnerHiddenIcon { get; set; }
          public string UserTooltipHtml { get; set; }
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
