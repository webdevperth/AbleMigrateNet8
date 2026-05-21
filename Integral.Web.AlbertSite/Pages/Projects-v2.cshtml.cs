using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class Projects_v2 : AppCode.PageBaseClasses.LoggedInPageModel {

    public const string UrlKey_TabName = "t";
    public const string UrlKey_SearchTerm = "s";
    public const char TabNameDelimiter = '_';
    public const int SearchTerm_MinLength = 3;

    public List<string> ProgramStatusList;
    public bool CanCreateNewProject;

    public enum ListEntity { Projects, Programs }
    public enum ListStatus { Active, Closed }

    public class ListFilters {
      // Add column filters to this later.
      public ListEntity ListEntity = ListEntity.Projects; // defaults
      public ListStatus ListStatus = ListStatus.Active;
      public string SearchTerm = string.Empty;
    }

    public string SelectedTabName = string.Empty;
    public ListFilters ActiveFilters = new ListFilters();

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Projects and Programs";

      CanCreateNewProject = SessionHelper.AppAccess.Projects.CanCreateNewProject();

      SetActiveFilters();

      if (SystemWeb.IsHttpPost) {
        if (WebHelper.Tabulator.GetAjaxAction() == WebHelper.Tabulator.AjaxAction.GetTableData) {
          try {
            WebHelper.WriteJsonAndEnd(GetListJson());
            return new EmptyResult();
          } catch (Exception ex) {
            // Return a JSON error object; Tabulator will log errors in console.
            WebHelper.WriteJsonAndEnd(new {
              error = true,
              message = "Failed to load projects.",
              detail = ex.ToString(),
              responseLog = LogHelper.GetResponseLog(),
              lastSql = LogHelper.GetLatestSqlQueryText()
            }, WebHelper.HttpStatusEnum.ServerError);
            return new EmptyResult();
          }
        }

      } else {

        SelectedTabName = GetTabName(ActiveFilters.ListEntity, ActiveFilters.ListStatus);
        ProgramStatusList = DbHelper.AlbertProgramStatus.GetProgramStatusList().Select(s => s.DisplayTitle).ToList();

      }

      return Page();
    }

    private void SetActiveFilters() {

      string tabName = WebHelper.GetQueryStringValue(UrlKey_TabName);
      if (!tabName.IsNullOrEmpty() && tabName.IndexOf(TabNameDelimiter) > 0) {
        var strArr = tabName.Split(TabNameDelimiter);
        if (Enum.TryParse(strArr[0], out ListEntity le)) ActiveFilters.ListEntity = le;
        if (Enum.TryParse(strArr[1], out ListStatus ls)) ActiveFilters.ListStatus = ls;
      }
      ActiveFilters.SearchTerm = (WebHelper.GetQueryStringValue(UrlKey_SearchTerm) ?? SessionHelper.AppState.Programs.Search_SearchTerm ?? string.Empty).TrimWhitespace();
    }

    private string GetTabName(ListEntity listEntity, ListStatus listStatus) {
      return listEntity.ToString() + "_" + listStatus.ToString();
    }

    public WebHelper.PageTabItem GetPageTabItem(ListEntity tabListEntity, ListStatus tabListStatus, string tabTitle) {

      string tabName = GetTabName(tabListEntity, tabListStatus);
      bool tabSelected =
        tabListEntity == ActiveFilters.ListEntity
        && tabListStatus == ActiveFilters.ListStatus;

      return new WebHelper.PageTabItem(tabName, tabTitle, tabSelected);
    }

    private WebHelper.Tabulator.ResponseData<TableRow> GetListJson() {

      var statusSearchOpts = new DbHelper.AblePrograms.StatusSearchOpts();
      if (ActiveFilters.ListStatus == ListStatus.Active) {
        statusSearchOpts.AddIncludeStatuses(DbHelper.AlbertProgramStatus.Statuses.Setup);
        statusSearchOpts.AddIncludeStatuses(DbHelper.AlbertProgramStatus.Statuses.Active);
      } else {
        statusSearchOpts.AddIncludeStatuses(DbHelper.AlbertProgramStatus.Statuses.Complete);
        statusSearchOpts.AddIncludeStatuses(DbHelper.AlbertProgramStatus.Statuses.ToBeClosed);
        statusSearchOpts.AddIncludeStatuses(DbHelper.AlbertProgramStatus.Statuses.Closed);
      }

      var projectAccessLevel = SessionHelper.AppAccess.Projects.GetProjectAccessLevel();

      // If user is admin and no search term is passed (default list), adjust project access level
      // if user is global admin or other, default to showing the user-activity list.
      if (ActiveFilters.SearchTerm.IsNullOrEmpty()) {
        if (SessionHelper.IsUserRoleAdmin || !SessionHelper.UserInfo.IsTenantOrgAdmin) {
          projectAccessLevel = AppHelper.ParamEnum.ProjectAccessLevel.RelatedOrInvited;
        }
      }

      var programs = DbHelper.AblePrograms.GetMainProgramList(
        programAccessLevel: projectAccessLevel,
        forUser: userInfo,
        forCompanyId: null,
        searchTerm: ActiveFilters.SearchTerm,
        searchOpts: statusSearchOpts);

      var tableRows = new List<TableRow>();
      TableRow projectRow = null;

      // In "projects" mode, a project row is added to the table and programs are added as children.
      // In "programs" mode, programs are added directly to the table, no projects.

      foreach (var program in programs.ProgramInfoList) {

        if (ActiveFilters.ListEntity == ListEntity.Projects) {
          if (projectRow == null || projectRow.JobNumber != program.ProgramJobNumber) {
            // Add a row for the project.
            // Values here are specific for projects.
            projectRow = new TableRow() {
              IsProject = true,
              JobNumber = program.ProgramJobNumber,
              RowItemName = program.ProjectName,
              ClientName = program.CompanyName,
              PartnerUserId = program.ClientLead_UserId,
              PartnerFirstName = program.ClientLead_FirstName,
              PartnerLastName = program.ClientLead_LastName,
              PartnerPhotoFilename = PathHelper.Images.UserPhotoFileName(program.ClientLead_FirstName, program.ClientLead_LastName, PathHelper.Images.UserPhotoSize.Thumbnail),
              Summary = new DbHelper.ProgramSummary.SummaryData(),
              ChildRows = new List<TableRow>(),
              CanViewRevenue = true // Any program-level CanViewRevenue=false will make the project-level one false.
            };
            tableRows.Add(projectRow);
          }
        }

        var programRow = new TableRow() {
          IsProject = false,
          JobNumber = program.ProgramJobNumber,
          ProgramJobId = program.ProgramJobId,
          RowItemName = program.ProgramJobName,
          ClientName = program.CompanyName,
          ProgramStatusId = program.ProgramStatusId,
          ProgramStatusDisplayOrder = program.ProgramStatusDisplayOrder,
          ProgramStatusDisplayTitle = DbHelper.AlbertProgramStatus.GetDisplayTitleOrNull(program.ProgramStatusId),
          StartDateUtc = program.ProgramStartDateUtc.SpecifyKindOrNull(DateTimeKind.Utc),
          EndDateUtc = program.ProgramEndDateUtc.SpecifyKindOrNull(DateTimeKind.Utc),
          PaxNow = program.HasCoachingCount,
          PaxMax = program.ParticipantCount,
          ComponentsNow = program.Summary.ComponentsCompleted,
          ComponentsMax = program.Summary.ComponentsCount,
          DeliverySorter = program.Summary.ComponentsCount,
          PartnerUserId = program.LeadConsultantUserId,
          PartnerFirstName = program.LeadConsultantFirstName,
          PartnerLastName = program.LeadConsultantLastName,
          PartnerPhotoFilename = PathHelper.Images.UserPhotoFileName(program.LeadConsultantFirstName, program.LeadConsultantLastName, PathHelper.Images.UserPhotoSize.Thumbnail),
          Summary = program.Summary
        };

        if (SessionHelper.AppAccess.Programs.Revenue.CanViewTotalRevenue(program)) {
          programRow.CanViewRevenue = true;
          programRow.RevenueNow = program.Summary.RevenueCompleted;
          programRow.RevenueMax = program.Summary.RevenueTotal;
          programRow.CostTotal = program.Summary.CostTotal;
          programRow.ProfitTotal = program.Summary.RevenueTotal - program.Summary.CostTotal;
        } else {
          programRow.CanViewRevenue = false;
          projectRow.CanViewRevenue = false;
        }

        if (SessionHelper.IsUserRoleAdmin) {
          programRow.WorkshopsNow = program.WorkshopsCompleted;
          programRow.WorkshopsMax = program.WorkshopCount;
        } else {
          programRow.WorkshopsNow = program.OwnWorkshopsCompleted;
          programRow.WorkshopsMax = program.OwnWorkshopCount;
        }

        if (SessionHelper.IsUserRoleAdmin) {
          programRow.SessionsCompleted = program.Summary.PaxSessionsCompleted;
          programRow.SessionsAllocated = program.Summary.PaxSessionsAllocated;
        } else {
          programRow.SessionsCompleted = program.Summary.PaxSessionsCompleted;
          programRow.SessionsAllocated = program.Summary.PaxSessionsAllocated;
        }

        // Delivery percent for program = number of components completed / count.
        if (programRow.Summary.ComponentsCompleted > 0 && programRow.Summary.ComponentsCount > 0) {
          programRow.DeliveryPercent = Math.Round((decimal)programRow.Summary.ComponentsCompleted * 100 / programRow.Summary.ComponentsCount);
        }

        if (ActiveFilters.ListEntity == ListEntity.Projects) {
          // Update project level stats.

          if (projectRow.ProgramStatusDisplayOrder == 0 || projectRow.ProgramStatusDisplayOrder > programRow.ProgramStatusDisplayOrder) {
            projectRow.ProgramStatusId = programRow.ProgramStatusId;
            projectRow.ProgramStatusDisplayOrder = programRow.ProgramStatusDisplayOrder;
            projectRow.ProgramStatusDisplayTitle = programRow.ProgramStatusDisplayTitle;
          }

          if (programRow.StartDateUtc != null && (projectRow.StartDateUtc == null || projectRow.StartDateUtc > programRow.StartDateUtc)) {
            projectRow.StartDateUtc = programRow.StartDateUtc; // Earliest start date.
          }
          if (programRow.EndDateUtc != null && (projectRow.EndDateUtc == null || projectRow.EndDateUtc < programRow.EndDateUtc)) {
            projectRow.EndDateUtc = programRow.EndDateUtc; // Latest end date.
          }

          // Accumulate top project totals.
          projectRow.PaxNow += programRow.PaxNow;
          projectRow.PaxMax += programRow.PaxMax;
          projectRow.WorkshopsNow += programRow.WorkshopsNow;
          projectRow.WorkshopsMax += programRow.WorkshopsMax;
          projectRow.SessionsCompleted += programRow.SessionsCompleted;
          projectRow.SessionsAllocated += programRow.SessionsAllocated;
          if (SessionHelper.AppAccess.Programs.Revenue.CanViewTotalRevenue(program)) {
            projectRow.RevenueNow += programRow.RevenueNow;
            projectRow.RevenueMax += programRow.RevenueMax;
            projectRow.CostTotal += programRow.CostTotal;
            projectRow.ProfitTotal += programRow.ProfitTotal;
          }

          projectRow.Summary.Add(programRow.Summary);

          // Delivery percent for project = number of programs completed / count.
          projectRow.ProgramCount += 1;
          projectRow.DeliverySorter = projectRow.ProgramCount;
          if (program.ProgramStatusId >= DbHelper.AlbertProgramStatus.Ids.Complete) projectRow.ProgramsComplete += 1;
          projectRow.DeliveryPercent = Math.Round((decimal)projectRow.ProgramsComplete * 100 / projectRow.ProgramCount);

          projectRow.ChildRows.Add(programRow);

        } else {

          tableRows.Add(programRow);

        }

      }

      return new WebHelper.Tabulator.ResponseData<TableRow>(tableRows);
    }

    public sealed class TableRow {

      public bool IsProject { get; set; }
      public int ProjectId { get; set; }
      public string JobNumber { get; set; }
      public int? ProgramJobId { get; set; }
      public string RowItemName { get; set; }
      public string ClientName { get; set; }
      public string ClientLead_FirstName { get; set; }
      public string ClientLead_LastName { get; set; }
      public int ProgramStatusId { get; set; }
      public int ProgramStatusDisplayOrder { get; set; }
      public string ProgramStatusDisplayTitle { get; set; }
      public DateTime? StartDateUtc { get; set; } // keep as ISO string for simplicity
      public DateTime? EndDateUtc { get; set; }
      public int PaxNow { get; set; }
      public int PaxMax { get; set; }
      public int ProgramsComplete { get; set; }
      public int ProgramCount { get; set; }
      public decimal DeliveryPercent { get; set; }
      public int WorkshopsNow { get; set; }
      public int WorkshopsMax { get; set; }
      public int SessionsCompleted { get; set; }
      public int SessionsAllocated { get; set; }
      public int ComponentsNow { get; set; }
      public int ComponentsMax { get; set; }
      public int DeliverySorter { get; set; }
      public bool CanViewRevenue { get; set; }
      public decimal RevenueNow { get; set; }
      public decimal RevenueMax { get; set; }
      public decimal CostTotal { get; set; }
      public decimal ProfitTotal { get; set; }
      public decimal OverallScore { get; set; }
      public decimal PrePostDelta { get; set; }
      public decimal ProgramScore { get; set; }
      public decimal WorkshopScore { get; set; }
      public decimal CoachingScore { get; set; }

      public int? PartnerUserId { get; set; }
      public string PartnerFirstName { get; set; }
      public string PartnerLastName { get; set; }
      public string PartnerPhotoFilename { get; set; }

      public DbHelper.ProgramSummary.SummaryData Summary { get; set; }

      public List<TableRow> ChildRows = null;

    }

  }
}
