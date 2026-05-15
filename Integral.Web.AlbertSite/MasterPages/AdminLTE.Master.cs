using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.UI;

namespace Integral.Web.PortalSite.MasterPages {

  public partial class AdminLTE : System.Web.UI.MasterPage {

    public object __o;
    public string CoacheeName, CoacheeEmail;
    public string ProgramJobNumber, ProgramName, ProgramUrl;
    public string ProjectJobNumber, ProjectName, ProjectUrl;
    public string QuoteName, CompanyName;

    public AppCode.LayoutModel Layout => AppCode.LayoutModel.GetCurrent();

    protected void Page_Init(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      if (SessionHelper.RedirectIfNotLoggedIn(PathHelper.WebRoot)) {
        return;
      }
    }

    protected void Page_Load(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      WebHelper.AddBodyClass("AdminLTE");

      Layout.CanViewBreadcrumb = !SessionHelper.IsUserRoleLeader;

      if (Page is AppCode.PageBaseClasses.CoacheeInfoBase) {

        var coacheePage = Page as AppCode.PageBaseClasses.CoacheeInfoBase;
        Layout.PageTitle = !coacheePage.PageTitle.IsNullOrEmpty() ? coacheePage.PageTitle : Regex.Replace(Request.RawUrl, @"^.*/([A-Za-z]+).*$", "$1").Replace("Coachee", "");
        Layout.PageSubSubtitleHTML = coacheePage.PageSubSubtitleHTML;
        Layout.MasterContentHeaderVisible = true;

        // Get all the info we need about project & program, based on user "state".
        // User could be:
        // a) adding participant from the Program area participant list - hence we know the Program,
        // b) adding participant from Participant area (admin) - we don't now the Program (admin user chooses it),
        // c) editing an existing participant.
        string companyName, projectJobNumber, projectName, programName;
        bool canViewProject = false;
        int programJobId = 0, companyId;

        // Note if Coachee is added at top level, coacheePage.ProgramInfo will be null.
        companyName = coacheePage.ProgramInfo?.CompanyName;
        companyId = coacheePage.ProgramInfo?.CompanyId ?? 0;
        projectJobNumber = coacheePage.ProgramInfo?.ProgramJobNumber;
        projectName = coacheePage.ProgramInfo?.ProjectName;
        programJobId = coacheePage.ProgramInfo?.ProgramJobId ?? 0;
        programName = coacheePage.ProgramInfo?.ProgramJobName;

        canViewProject = SessionHelper.AppAccess.Projects.CanViewProject(coacheePage.ProgramInfo); // User can access project area.

        // Create crumbs.
        var crumbList = new List<BreadcrumbPart>();
        // Company crumb is simple - no link just text.
        AddCompanyCrumb(crumbList, companyName, companyId);
        // Project name crumb.
        AddProjectCrumb(crumbList, projectJobNumber, projectName, canViewProject);
        // Program crumb - or skip if we don't know the program.
        if (programJobId > 0) {
          string programPageUrl = "";
          // Link to ProgramOverview if allowed, else link to ProgramParticipants.
          if (SessionHelper.AppAccess.Programs.CanViewProgramOverview(coacheePage.ProgramInfo)) {
            programPageUrl = PathHelper.Pages.ProgramOverview(programJobId);
          } else if (SessionHelper.AppAccess.Programs.CanListProgramParticipants(coacheePage.ProgramInfo)) {
            programPageUrl = PathHelper.Pages.ProgramParticipants(programJobId);
          }
          crumbList.Add(new BreadcrumbPart(coacheePage.CoacheeInfo.ProgramName, programPageUrl));
        }
        if (PathHelper.IsCurrentPage(PathHelper.Reports.CoacheeSurvey())) {
          // Add link back to coachee page (surveys tab) then plain text for report page.
          PathHelper.Pages.GetCoacheeSurveyUIDs(out string surveyUId, out string partUId);
          crumbList.Add(new BreadcrumbPart(coacheePage.CoacheeInfo.GetFullName(),
            SessionHelper.AppAccess.PageAccess.CanAccessCoacheeEdit() ?
            PathHelper.Pages.CoacheeEdit(coacheePage.CoacheeInfo.CoacheeId, PathHelper.CoacheeTabEnum.surveys, surveyUId, partUId) : ""));
          crumbList.Add(new BreadcrumbPart("Analytics"));
        } else {
          // Final crumb is plain text Participant name or "New Participant".
          crumbList.Add(new BreadcrumbPart(coacheePage.IsNewCoachee ? "New Participant" : coacheePage.CoacheeInfo.GetFullName()));
        }
        // Get the crumbs Html.
        Layout.PageBreadcrumbHtml = GetPageBreadcrumbHtml(crumbList);

      } else if (Page is AppCode.PageBaseClasses.ProgramPageBase) {

        var programPage = Page as AppCode.PageBaseClasses.ProgramPageBase;
        Layout.PageTitle = programPage.PageTitle;
        Layout.PageSubSubtitleHTML = programPage.PageSubSubtitleHTML;
        Layout.MasterContentHeaderVisible = true;

        if (programPage.IsNewProgram && programPage.AddToProjectInfo != null) {
          Layout.PageBreadcrumbHtml = GetProgramBreadCrumbHtml(null, programPage.AddToProjectInfo, programPage.IsNewProgram);
        } else {
          Layout.PageBreadcrumbHtml = GetProgramBreadCrumbHtml(programPage.ProgramInfo, null, programPage.IsNewProgram);
        }

      } else if (Page is AppCode.PageBaseClasses.ProjectPageBase) {

        var projectPage = Page as AppCode.PageBaseClasses.ProjectPageBase;
        Layout.PageTitle = projectPage.PageTitle;
        Layout.PageSubSubtitleHTML = projectPage.PageSubSubtitleHTML;
        Layout.MasterContentHeaderVisible = true;

        if (!projectPage.IsNewProject) {
          // New breadcrumb list.
          var crumbList = new List<BreadcrumbPart>();
          // Company text name.
          AddCompanyCrumb(crumbList, projectPage.ProjectInfo.ClientCompanyName, projectPage.ProjectInfo.CompanyId ?? 0);
          // Project name crumb.
          crumbList.Add(new BreadcrumbPart(GetCrumbProjectName(projectPage.ProjectInfo.JobNumber, projectPage.ProjectInfo.ProjectName)));
          // Get crumbs html.
          Layout.PageBreadcrumbHtml = GetPageBreadcrumbHtml(crumbList);
        }

      } else if (Page is AppCode.PageBaseClasses.QuotePageBase) {

        var quotePage = Page as AppCode.PageBaseClasses.QuotePageBase;
        Layout.PageTitle = quotePage.PageTitle;
        Layout.PageSubSubtitleHTML = quotePage.PageSubSubtitleHTML;
        Layout.MasterContentHeaderVisible = true;

        // New breadcrumb list.
        var crumbList = new List<BreadcrumbPart>();

        if (quotePage.IsNewQuote) {

          crumbList.Add(new BreadcrumbPart("New Quote"));

        } else {

          AddCompanyCrumb(crumbList, quotePage.QuoteInfo.CompanyInfo.CompanyName, quotePage.QuoteInfo.CompanyInfo.CompanyId);

          // Project name crumb.
          bool canViewProject = SessionHelper.AppAccess.Projects.CanViewProject(quotePage.ProjectInfo); // User can access project area.
          AddProjectCrumb(crumbList, quotePage.QuoteInfo.JobNumber, quotePage.QuoteInfo.ProjectName, canViewProject);

          // Add quote title if present.
          if (quotePage.QuoteInfo.QuoteTitle.IsNullOrEmpty()) {
            crumbList.Add(new BreadcrumbPart("[No Title]"));
          } else {
            crumbList.Add(new BreadcrumbPart(quotePage.QuoteInfo.QuoteTitle));
          }
        }

        // Get crumbs html.
        Layout.PageBreadcrumbHtml = GetPageBreadcrumbHtml(crumbList);

      } else if (Page is AppCode.PageBaseClasses.CoachInfoBase) {

        var coachPage = Page as AppCode.PageBaseClasses.CoachInfoBase;
        Layout.PageTitle = coachPage.PageTitle;
        Layout.PageSubSubtitleHTML = coachPage.PageSubSubtitleHTML;
        Layout.MasterContentHeaderVisible = true;

        // New breadcrumb list.
        var crumbList = new List<BreadcrumbPart>();

        if (coachPage.IsNewCoach) {
          crumbList.Add(new BreadcrumbPart("New Coach"));
        } else {
          crumbList.Add(new BreadcrumbPart(coachPage.CoachInfo.GetFullName()));
        }
        // Get crumbs html.
        Layout.PageBreadcrumbHtml = GetPageBreadcrumbHtml(crumbList);

      } else if (Page is AppCode.PageBaseClasses.CompanyInfoBase) {

        var companyPage = Page as AppCode.PageBaseClasses.CompanyInfoBase;
        Layout.PageTitle = companyPage.PageTitle;
        Layout.PageSubSubtitleHTML = companyPage.PageSubSubtitleHTML;
        Layout.MasterContentHeaderVisible = true;

        if (!companyPage.IsNewCompany) {
          var crumbList = new List<BreadcrumbPart>();
          crumbList.Add(new BreadcrumbPart(companyPage.CompanyInfo.CompanyName));
          // Get crumbs html.
          Layout.PageBreadcrumbHtml = GetPageBreadcrumbHtml(crumbList);
        }

      } else if (Page is AppCode.PageBaseClasses.ModulePageBase) {

        var modulePage = Page as AppCode.PageBaseClasses.ModulePageBase;
        Layout.PageTitle = modulePage.PageTitle;

        if (modulePage.ModuleInfo != null && modulePage.ContentInfo != null && modulePage.IsContentFromModule) {
          Layout.PageBreadcrumbHtml = GetModuleBreadCrumbHtml(modulePage.ModuleInfo, modulePage.ContentInfo);
          Layout.CanViewBreadcrumb = true; // All can see this breadcrumb
        }

        Layout.MasterContentHeaderVisible = !Layout.PageTitle.IsNullOrEmpty() || !Layout.PageBreadcrumbHtml.IsNullOrEmpty() ? true : false;

      } else if (Page is AppCode.PageBaseClasses.LoggedInPageBase) {

        var loggedInPage = Page as AppCode.PageBaseClasses.LoggedInPageBase;
        Layout.PageTitle = loggedInPage.PageTitle;
        Layout.PageSubSubtitleHTML = loggedInPage.PageSubSubtitleHTML;
        Layout.MasterContentHeaderVisible = !Layout.PageTitle.IsNullOrEmpty() ? true : false;

        if (loggedInPage.ProgramInfo != null) {
          if (PathHelper.IsCurrentPage(PathHelper.Pages.Coachees())) {
            Layout.PageBreadcrumbHtml = GetProgramBreadCrumbHtml(loggedInPage.ProgramInfo);
          } else if (PathHelper.IsCurrentPage(PathHelper.Reports.EvalViewer())) {
            Layout.PageBreadcrumbHtml = GetProgramBreadCrumbHtml(loggedInPage.ProgramInfo, Layout.PageTitle);
          }
        }
      }

      Layout.PageTitle_Mobile = (Page as AppCode.PageBaseClasses.LoggedInPageBase)?.PageTitle_Mobile;
    }

    protected override void Render(HtmlTextWriter writer) {

      if (WebHelper.IsRequestExiting()) return;

      base.Render(writer);
    }

    private string GetModuleBreadCrumbHtml(DbHelper.Modules.ModuleInfo moduleInfo, DbHelper.Content.ContentInfo contentInfo) {

      if (PathHelper.IsCurrentPage(PathHelper.Pages.ContentDetails(null)) && moduleInfo != null && contentInfo != null) {

        var breadcrumbParts = new List<BreadcrumbPart>();
        breadcrumbParts.Add(new BreadcrumbPart("Microlearning", PathHelper.Pages.Content()));

        if (moduleInfo != null) {
          breadcrumbParts.Add(new BreadcrumbPart(moduleInfo.ModuleTitle, PathHelper.Pages.Module(moduleInfo.ModuleGuid)));
        }

        if (contentInfo != null) {
          breadcrumbParts.Add(new BreadcrumbPart(contentInfo.ContentTitle));
        }

        return GetPageBreadcrumbHtml(breadcrumbParts);
      }
      return "";
    }

    private string GetProgramBreadCrumbHtml(DbHelper.AblePrograms.AbleProgramInfo thisProgramInfo, string pageTitle) {

      if (thisProgramInfo == null) return "";

      bool canViewProject = SessionHelper.AppAccess.Projects.CanViewProject(thisProgramInfo); // User can access project area.

      var crumbList = new List<BreadcrumbPart>();
      AddCompanyCrumb(crumbList, thisProgramInfo.CompanyName, thisProgramInfo.CompanyId ?? 0);
      AddProjectCrumb(crumbList, thisProgramInfo.ProgramJobNumber, thisProgramInfo.ProjectName, canViewProject);
      AddProgramCrumb(crumbList, thisProgramInfo, canViewProject);
      crumbList.Add(new BreadcrumbPart(pageTitle));

      return GetPageBreadcrumbHtml(crumbList);
    }

    private string GetProgramBreadCrumbHtml(
      DbHelper.AblePrograms.AbleProgramInfo thisProgramInfo = null,
      DbHelper.Projects.ProjectInfo thisProject = null,
      bool isNewProgram = false) {

      string companyName, projectJobNumber, projectName, programName;
      bool canViewProject = false;
      int programJobId, companyId;

      if (isNewProgram) {
        canViewProject = SessionHelper.AppAccess.Projects.CanViewProject(thisProject);
        companyName = thisProject.ClientCompanyName;
        companyId = thisProject.CompanyId ?? 0;
        projectJobNumber = thisProject.JobNumber;
        projectName = thisProject.ProjectName;
        programJobId = 0;
        programName = null;
      } else {
        canViewProject = SessionHelper.AppAccess.Projects.CanViewProject(thisProgramInfo);
        companyName = thisProgramInfo.CompanyName;
        companyId = thisProgramInfo.CompanyId ?? 0;
        projectJobNumber = thisProgramInfo.ProgramJobNumber;
        projectName = thisProgramInfo.ProjectName;
        programJobId = thisProgramInfo.ProgramJobId;
        programName = thisProgramInfo.ProgramJobName;
      }

      // New breadcrumb list.
      var crumbList = new List<BreadcrumbPart>();
      // Company text name.
      AddCompanyCrumb(crumbList, companyName, companyId);
      // Project name crumb.
      AddProjectCrumb(crumbList, projectJobNumber, projectName, canViewProject);
      // Add Program name as text.
      crumbList.Add(new BreadcrumbPart(isNewProgram ? "New Program" : programName));
      // Get crumbs html.
      return GetPageBreadcrumbHtml(crumbList);

    }

    private void AddCompanyCrumb(List<BreadcrumbPart> crumbList, string companyName, int companyId) {
      if (companyId > 0 && !companyName.IsNullOrEmpty()) {
        crumbList.Add(new BreadcrumbPart(companyName, PathHelper.Pages.OrganisationOverview(companyId)));
      }
    }

    private void AddProjectCrumb(List<BreadcrumbPart> crumbList, string projectJobNumber, string projectName, bool canViewProject) {
      // Only provide link if user has access to Project area.
      if (!projectJobNumber.IsNullOrEmpty()) {
        crumbList.Add(
          new BreadcrumbPart(GetCrumbProjectName(projectJobNumber, projectName), // Standard format string for Project name.
          canViewProject ? PathHelper.Pages.ProjectPrograms(projectJobNumber) : null)); // Link or plain text.
      }
    }

    private void AddProgramCrumb(List<BreadcrumbPart> crumbList, DbHelper.AblePrograms.AbleProgramInfo program, bool canViewProject) {
      crumbList.Add(
        new BreadcrumbPart(program.ProgramJobName,
        canViewProject ? PathHelper.Pages.ProgramOverview(program.ProgramJobId) : null));
    }

    private string GetCrumbProjectName(string projectJobNumber, string projectName) {
      return projectName + " (" + projectJobNumber + ")";
    }

    private class BreadcrumbPart {
      public string Label;
      public string Url;
      public BreadcrumbPart(string label, string url = null) { Label = label; Url = url; }
    }

    private string GetPageBreadcrumbHtml(List<BreadcrumbPart> crumbs) {
      var breadcrumbHtml = new StringBuilder("");
      int crumbCount = 0;
      string separator = "<i class=\"far fa-chevron-right \"></i>";
      foreach (var crumb in crumbs) {
        crumbCount++;
        string firstBread = crumbCount == 1 ? " class=\"firstBread\" " : "";
        if (crumbCount > 1) breadcrumbHtml.Append(separator);
        if (!crumb.Url.IsNullOrEmpty()) {
          breadcrumbHtml.Append("<a href=\"" + crumb.Url + "\">" + crumb.Label.HTMLEncode() + "</a>");
        } else {
          breadcrumbHtml.Append("<span" + firstBread + " >" + crumb.Label.HTMLEncode() + "</span>");
        }
      }
      return breadcrumbHtml.ToString();
    }
  }
}
