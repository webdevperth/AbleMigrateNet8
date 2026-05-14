using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class OrganisationProjects : AppCode.PageBaseClasses.CompanyInfoBase {

    public const int Min_Search_Length = 3;
    public const int Default_Rows_Per_Page = 10;
    public const string UrlParam_GetRows = "getrows";
    public SearchInfo searchInfo = new SearchInfo();

    public class SearchInfo {
      public string SearchTerm { get; set; }
      public int RowsPerPage { get; set; }
      public int GetPage { get; set; }
      public bool StatusClosed { get; set; }
    }

    public DbHelper.AblePrograms.AbleProgramList organisationPrograms { get; internal set; }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Organisation Projects";

      searchInfo.SearchTerm = (WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.JobSearchTerm) ?? SessionHelper.AppState.Programs.Search_SearchTerm ?? "").URLDecode().TrimWhitespace();

      if (!WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProjectStatusToggle).IsNullOrEmpty()) {
        searchInfo.StatusClosed = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProjectStatusToggle) == PathHelper.AbleUrlValues.ProjectStatusToggleValue_Closed;
      } else {
        searchInfo.StatusClosed = SessionHelper.AppState.Programs.Search_StatusClosed;
      }
      searchInfo.GetPage = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.GetPage).ToIntOrDefault(SessionHelper.AppState.Programs.Search_CurrentPage.GetValueOrDefault(1));
      searchInfo.RowsPerPage = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PerPage).ToIntOrDefault(Default_Rows_Per_Page);

      if (!WebHelper.GetQueryStringValue(UrlParam_GetRows).IsNullOrEmpty()) {
        DoProgramSearch();
      }

    }

    public string GetRowLinkUrl() {
      return PathHelper.Pages.ProgramOverview(null);
    }

    void DoProgramSearch() {
      // If user is NOT admin, a blank search means show all programs with coachees assigned to that user.
      // So an admin user always requires a search term. Non-admin search term can be blank.
      // For non-admins, only programs found and those containing coachees assigned to the coach(user).

      int fetchRows = searchInfo.RowsPerPage;
      int offsetRows = (searchInfo.GetPage - 1) * fetchRows;

      DbHelper.AblePrograms.AbleProgramList programList = null;

      var results = new ProgramSearchResults();
      results.RowsPerPage = searchInfo.RowsPerPage;
      results.CurrentPage = searchInfo.GetPage;
      results.SearchTerm = searchInfo.SearchTerm;
      results.StatusClosed = searchInfo.StatusClosed;

      SessionHelper.AppState.Programs.Search_SearchTerm = searchInfo.SearchTerm;
      SessionHelper.AppState.Programs.Search_CurrentPage = searchInfo.GetPage;
      SessionHelper.AppState.Programs.Search_StatusClosed = searchInfo.StatusClosed;

      var statusSearchOpts = new DbHelper.AblePrograms.StatusSearchOpts();
      if (searchInfo.StatusClosed) {
        statusSearchOpts.AddIncludeStatuses(DbHelper.AlbertProgramStatus.Statuses.Closed); // Search only closed status.
      } else {
        statusSearchOpts.AddExcludeStatuses(DbHelper.AlbertProgramStatus.Statuses.Closed); // Search all except closed status.
      }

      programList = DbHelper.AblePrograms.GetMainProgramList(
        programAccessLevel: SessionHelper.AppAccess.Projects.GetProjectAccessLevel(),
        forUser: userInfo,
        forCompanyId: CompanyInfo.CompanyId,
        searchTerm: searchInfo.SearchTerm,
        searchOpts: statusSearchOpts,
        offsetRows: offsetRows,
        fetchRows: fetchRows);

      if (programList != null) results.SetResults(programList);

      WebHelper.WriteAndEnd(JsonConvert.SerializeObject(results), WebHelper.HttpContentType.json);
    }

    class ProgramSearchResults {

      public int? RowsPerPage { get; set; }
      public int? CurrentPage { get; set; }
      public string SearchTerm { get; set; }
      public bool StatusClosed { get; set; }
      public SearchResults results { get; private set; }

      public ProgramSearchResults() {
        results = new SearchResults();
      }
      public void SetResults(DbHelper.AblePrograms.AbleProgramList newResults) {
        results.OffsetRows = newResults.OffsetRows;
        results.FetchRows = newResults.FetchRows;
        results.TotalRows = newResults.TotalRows;
        results.ClearProgramInfo();
        foreach (var pi in newResults.ProgramInfoList) results.AddProgramInfo(pi);
      }
      public class SearchResults {
        public int? OffsetRows { get; internal set; }
        public int? FetchRows { get; internal set; }
        public int TotalRows { get; internal set; }
        public List<ProgramInfo> ProgramInfoList { get; private set; }
        public SearchResults() {
          ProgramInfoList = new List<ProgramInfo>();
        }
        public void ClearProgramInfo() {
          ProgramInfoList.Clear();
        }
        public void AddProgramInfo(DbHelper.AblePrograms.AbleProgramInfo pi) {
          var program = new ProgramInfo() {
            ProgramJobId = pi.ProgramJobId,
            ProjectJobName = WebHelper.GetListViewLocatorColumnHtml(pi.ProjectName, GetJobNumberHtml(pi.ProgramJobNumber), pi.CompanyName),
            ProgramJobName = pi.ProgramJobName,
            ProgramStartEndDateUtc = WebHelper.DisplayDate(pi.ProgramStartDateUtc, "-") + " - " + WebHelper.DisplayDate(pi.ProgramEndDateUtc, "-"),
            ProgramStatus = WebHelper.GetProgramStatusBadge(pi.ProgramStatusId),
            WorkshopCount = pi.WorkshopCount,
            ParticipantCount = pi.HasCoachingCount.ToString() + " / " + pi.ParticipantCount.ToString(),
            RevenueProgress = SessionHelper.AppAccess.Programs.Revenue.CanViewTotalRevenue(pi)
              ? WebHelper.GetProgressBarHtml(pi.CompletedRevenueAmt, pi.TotalRevenueAmt, string.Empty, WebHelper.ProgressBarType.Currency)
              : "-"
          };
          ProgramInfoList.Add(program);
        }

        public string GetJobNumberHtml(string jobNumber) {
          if (!SessionHelper.AppAccess.Projects.CanViewProjectsLinkToPrograms()) return jobNumber;
          return "<a href=\"" + PathHelper.Pages.ProjectPrograms(jobNumber) + "\">" + jobNumber + "</a>";
        }

        public class ProgramInfo {
          public int ProgramJobId { get; set; }
          public string ProjectJobName { get; set; }
          public string ProgramJobName { get; set; }
          public string ProgramStartEndDateUtc { get; set; }
          public string ProgramStatus { get; set; }
          public int WorkshopCount { get; internal set; }
          public string ParticipantCount { get; internal set; }
          public string RevenueProgress { get; internal set; }
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
