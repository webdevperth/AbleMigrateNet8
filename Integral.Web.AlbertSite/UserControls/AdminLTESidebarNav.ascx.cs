using Integral.Web.PortalSite.AppCode.PageBaseClasses;
using System;

namespace Integral.Web.PortalSite.UserControls {

  public partial class AdminLTESidebarNav : System.Web.UI.UserControl {

    public class MenuStyles {
      public const string SubmenuClass = "submenu";
      public const string SubmenuItemClass = "submenu-item";
      public const string SubmenuHasThirdLevel = " has-third-level";
      public const string SubmenuIsThirdLevel = " third-level";
      public const string AddActiveClass = " activePage";
      public const string RemoveHighlight = " removeHighlight";
    }

    public class MenuItems {
      public const string Dashboard = "Dashboard";
      public const string Projects = "Projects";
      public const string Participants = "Participants";
      public const string Quotes = "Quotes";
      public const string DevelopmentPlan = "Plan";
      public const string Insights = "Insights";
      public const string Surveys = "Surveys";
      public const string MyProfile = "My Profile";
      public const string OrganisationOverview = "Overview";
      public const string Microlearning = "Microlearning";
    }

    public object __o; // removes VS errors

    public string CurrentItemLine1 = string.Empty, CurrentItemLine2 = string.Empty;
    public int ViewingEntityId = 0; // The ID of whatever is being viewed/edited at the moment (coachee id, coach id, job id, etc)
    public bool IsCurrentPageCoacheeList, IsCurrentPageEvalViewer, IsCurrentPageProgramContentDetails;

    public ProjectPageBase ProjectPage = null;
    public ProgramPageBase ProgramPage = null;
    public QuotePageBase QuotePage = null;
    public CoacheeInfoBase CoacheePage = null;
    public CoachInfoBase CoachPage = null;
    public CompanyInfoBase CompanyPage = null;

    public DbHelper.AbleUser.AbleUserInfo UserInfo = null;
    public DbHelper.Projects.ProjectInfo ProjectInfo = null;
    public DbHelper.AblePrograms.AbleProgramInfo ProgramInfo = null;
    public DbHelper.AlbertCoaches.AlbertCoachInfo CoachInfo = null;
    public DbHelper.ClientCompanies.AlbertCompanyInfo CompanyInfo = null;
    public DbHelper.AbleQuotes.QuoteInfo QuoteInfo = null;
    public LoggedInPageBase LoggedInPage;
    public bool MenuThirdLayerActive_Program, DashboardMenuIsActive, ProjectMenuIsActive;

    protected void Page_Load(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      // Get user info from the host page.
      if (!(Page is LoggedInPageBase loggedInPage)) return;

      LoggedInPage = loggedInPage;
      ProjectMenuIsActive = loggedInPage.ProjectMenuIsActive;
      MenuThirdLayerActive_Program = loggedInPage.MenuThirdLayerActive_Programs;
      DashboardMenuIsActive = loggedInPage.DashboardMenuIsActive;
      UserInfo = loggedInPage.userInfo;
      ProgramInfo = loggedInPage.ProgramInfo;
      ProjectInfo = loggedInPage.ProjectInfo;

      IsCurrentPageCoacheeList = PathHelper.IsCurrentPage(PathHelper.Pages.Coachees());
      IsCurrentPageEvalViewer = PathHelper.IsCurrentPage(PathHelper.Reports.EvalViewer());
      IsCurrentPageProgramContentDetails = PathHelper.IsCurrentPage(PathHelper.Pages.ProgramContentDetails(null, null)) && ProgramInfo != null;

      if (IsCurrentPageCoacheeList || IsCurrentPageEvalViewer) {

        ProjectInfo = loggedInPage.ProjectInfo;
        ProgramInfo = loggedInPage.ProgramInfo;

      } else if (Page is CoacheeInfoBase) {

        CoacheePage = Page as CoacheeInfoBase;
        ProjectInfo = CoacheePage.ProjectInfo;
        ProgramInfo = CoacheePage.ProgramInfo;
        CurrentItemLine1 = CoacheePage.CoacheeInfo.GetFullName();
        CurrentItemLine2 = CoacheePage.CoacheeInfo.EmailAddress;
        ViewingEntityId = CoacheePage.CoacheeInfo.CoacheeId;
        if (ProjectInfo != null && ProgramInfo != null) ProjectMenuIsActive = MenuThirdLayerActive_Program = true;

      } else if (Page is ProjectPageBase) {

        ProjectPage = Page as ProjectPageBase;
        ProjectInfo = ProjectPage.ProjectInfo;
        CurrentItemLine1 = ProjectInfo.JobNumber;
        CurrentItemLine2 = ProjectInfo.ProjectName;
        ViewingEntityId = ProjectInfo.ProjectId;

      } else if (Page is ProgramPageBase) {

        ProgramPage = Page as ProgramPageBase;
        ProjectInfo = ProgramPage.ProjectInfo;
        ProgramInfo = ProgramPage.ProgramInfo;
        CurrentItemLine1 = ProgramInfo.ProgramJobNumber;
        CurrentItemLine2 = ProgramInfo.ProgramJobName + "\n" + ProgramInfo.CompanyName;
        ViewingEntityId = ProgramInfo.ProgramJobId;

      } else if (Page is QuotePageBase) {

        QuotePage = Page as QuotePageBase;
        ProjectInfo = QuotePage.ProjectInfo;
        QuoteInfo = QuotePage.QuoteInfo;
        CurrentItemLine1 = QuoteInfo.JobNumber;
        CurrentItemLine2 = QuoteInfo.QuoteTitle.ValueIfNullOrEmpty(QuoteInfo.ProjectName);
        ViewingEntityId = QuoteInfo.QuoteId;

      } else if (Page is CoachInfoBase) {

        CoachPage = Page as CoachInfoBase;
        CoachInfo = CoachPage.CoachInfo;
        CurrentItemLine1 = CoachInfo.GetFullName();
        CurrentItemLine2 = CoachInfo.EmailAddress;
        ViewingEntityId = CoachInfo.UserId;

      } else if (Page is CompanyInfoBase) {

        CompanyPage = Page as CompanyInfoBase;
        CompanyInfo = CompanyPage.CompanyInfo;
        CurrentItemLine1 = CompanyInfo.CompanyName;
        CurrentItemLine2 = string.Empty;
        ViewingEntityId = CompanyInfo.CompanyId;
      }
    }

    public void GetOrganisationMenu() {

      if (!SessionHelper.AppAccess.Companies.CanViewOrganisationListView()) return;

      bool canViewOverview = SessionHelper.AppAccess.Companies.CanViewOrganisationOverview(CompanyInfo);
      bool canViewSettings = SessionHelper.AppAccess.Companies.CanViewOrganisationSettings(CompanyInfo, isNewCompany: (Page as CompanyInfoBase)?.IsNewCompany ?? false);
      bool canViewDepartments = SessionHelper.AppAccess.Companies.CanViewOrganisationDepartments(CompanyInfo);
      bool canViewPeople = SessionHelper.AppAccess.Companies.CanViewOrganisationPeople(CompanyInfo);
      bool canViewProjects = SessionHelper.AppAccess.Companies.CanViewOrganisationProjects(CompanyInfo);
      bool canViewCapabilities = SessionHelper.AppAccess.Companies.CanViewOrganisationCapabilities(CompanyInfo);
      bool canViewIOSReports = SessionHelper.AppAccess.Companies.CanViewOrganisationIOSReports(CompanyInfo);

      MenuItemWithSubMenu(GetOrganisationUrl(), "Organisation" + (SessionHelper.IsUserRoleClient ? "" : "s"), "business-outline", () => {

        if (CompanyPage != null && CompanyPage.IsNewCompany) {
          SubMenu(() => {
            SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.OrganisationSettings(true), "New Organisation", "flag-outline"));
          });
          return;
        }

        if (CompanyInfo == null) return;

        if (canViewOverview) {
          SubMenu(() => {
            SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.OrganisationOverview(true), MenuItems.OrganisationOverview, "bookmark-outline"));
          });
        }

        if (canViewSettings) {
          SubMenu(() => {
            SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.OrganisationSettings(true), "Settings", "flag-outline"));
          });
        }
        if (canViewPeople) {
          SubMenu(() => {
            SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.OrganisationPeople(true), "People", "people-outline"));
          });
        }
        if (canViewDepartments) {
          SubMenu(() => {
            SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.OrganisationDepartments(true), "Departments", "grid-outline"));
          });
        }

        if (canViewProjects) {
          SubMenu(() => {
            SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.OrganisationProjects(true), "Projects", "folder-open-outline",
              PathHelper.Pages.PeopleDetails(CompanyInfo.CompanyId, null)));
          });
        }

        if (canViewCapabilities) {
          SubMenu(() => {
            SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.OrganisationCapabilities(CompanyInfo.CompanyId), "Capabilities", "bar-chart-outline",
              PathHelper.Pages.OrganisationCapabilities(CompanyInfo.CompanyId)));
          });
        }

        if (canViewIOSReports) {
          SubMenu(() => {
            SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Reports.OrganisationIOSReports(CompanyInfo.CompanyId), "IOS Reports", "bar-chart-outline"));
          });
        }
      });
    }

    // Force Highlight Subitem
    public string SideMenuDetailHighlight(string menuItem, bool isTopLevel) {

      if (menuItem == MenuItems.Participants) {
        return ParticipantsHighlight(isTopLevel);
      } else if (menuItem == MenuItems.Quotes) {
        return QuotesHighlight(isTopLevel);
      } else if (menuItem == MenuItems.Surveys) {
        return SurveyHighlight();
      } else if (menuItem == MenuItems.MyProfile) {
        if (IsDisplayingProfileInDashboard() && !IsPagePeopleDetails_ForParticipant()) return MenuStyles.AddActiveClass;
      } else if (menuItem == MenuItems.OrganisationOverview) {
        if (!IsDisplayingProfileInDashboard() && PathHelper.IsCurrentPage(PathHelper.Pages.PeopleDetails(null, null))) {
          return MenuStyles.AddActiveClass;
        }

      } else if (menuItem == MenuItems.Microlearning) {

        if (IsCurrentPageProgramContentDetails) {
          if (!isTopLevel || (isTopLevel && ProgramInfo != null && SessionHelper.IsUserRoleLeader)) {
            return MenuStyles.AddActiveClass;
          }
        }

        if (ProgramInfo == null) {
          if (PathHelper.IsCurrentPage(PathHelper.Pages.ContentDetails(null))
            || PathHelper.IsCurrentPage(PathHelper.Pages.ModuleEdit(null))
            || PathHelper.IsCurrentPage(PathHelper.Pages.Module(null))) {
            return MenuStyles.AddActiveClass;
          }
        }
      }

      return "";
    }

    // Highlight Participant in Project/Program/Participant if in CoacheeEdit
    public string ParticipantsHighlight(bool isTopLevel) {
      if (IsCurrentPageCoacheeList && ProgramInfo != null && isTopLevel) {
        return MenuStyles.RemoveHighlight;
      } else if (CoacheePage != null && !isTopLevel) {
        return MenuStyles.AddActiveClass;
      } else if (Page is CoacheeInfoBase && CoacheePage.IsNewCoachee) {
        if (isTopLevel && ProgramInfo != null) return "";
        return MenuStyles.AddActiveClass;
      } else if (IsCurrentPageCoacheeList && ProgramInfo != null && !isTopLevel) {
        return MenuStyles.AddActiveClass;
      }

      return "";
    }

    public string QuotesHighlight(bool isTopLevel) {
      if (QuotePage != null) {
        if (QuotePage.IsNewQuote) {
          if (isTopLevel && ProjectInfo != null) return "";
          else if ((isTopLevel && ProjectInfo == null) || (!isTopLevel && ProjectInfo != null)) return MenuStyles.AddActiveClass;
        } else if (QuotePage.CanViewQuoteInfo && !isTopLevel) {
          return MenuStyles.AddActiveClass;
        }
      }
      return "";
    }

    public string SurveyHighlight() {
      if ((PathHelper.IsCurrentPage(PathHelper.Reports.CoacheeSurvey()) && SessionHelper.IsUserRoleLeader)
        || PathHelper.IsCurrentPage(PathHelper.Pages.ProgramSendSurvey()) || PathHelper.IsCurrentPage(PathHelper.Pages.ProgramSurveyStatus())) {
        return MenuStyles.AddActiveClass;
      }
      return "";
    }

    public string TopMenuLink(string path, string displayText, string iconName, params string[] extraHighlightPages) {
      return TopMenuLink(path, displayText, iconName, false, extraHighlightPages);
    }

    public string TopMenuLink(string path, string displayText, string iconName, bool isMobileOnly, params string[] extraHighlightPages) {
      return $@"<li class=""{MenuStyles.SubmenuClass}{SideMenuDetailHighlight(displayText, true)} {(isMobileOnly ? "visible-xs" : "")}"">"
        + MenuLink(path, displayText, iconName, false, extraHighlightPages) + "</li>";
    }

    public string MenuLink(string path, string displayText, string iconName, bool isThirdLayerHolder, params string[] extraHighlightPages) {
      return @"<a href=""" + path.HTMLEncode() + @""" class="""
        + extraHighlightPages.Join(" ", s => { return PathHelper.MenuHighlightPathPrefix + (s + "?").Split('?')[0].Replace("/", ""); }) + @""">"
        + "<span>"
        + "<ion-icon name=\"" + iconName + "\"></ion-icon>"
        + "<span class=\"pl5\">" + displayText + "</span></span>"
        + ChevronDefiner(displayText, isThirdLayerHolder, path)
        + "</a>";
    }

    public string SubmenuItem(string path, string text, string iconName, params string[] extraHighlightPages) {
      return @"<li class=""" + MenuStyles.SubmenuItemClass + SideMenuDetailHighlight(text, false) + @""">" + MenuLink(path, text, iconName, false, extraHighlightPages) + "</li>";
    }

    public string ChevronDefiner(string text, bool isThirdLayerHolder, string path) {
      string chevronDown = "<ion-icon name=\"chevron-down-outline\" class=\"nav-chevron\"></ion-icon>";
      string chevronForward = "<ion-icon name=\"chevron-forward-outline\" class=\"nav-chevron\"></ion-icon>";

      if (text == MenuItems.Dashboard) {
        if (!DashboardMenuIsActive && !isThirdLayerHolder) {
          return chevronForward;
        } else {
          return chevronDown;
        }
      } else if (text == MenuItems.Projects && path == PathHelper.Pages.Projects_List()) {
        if (ProjectMenuIsActive) {
          return chevronDown;
        } else {
          return chevronForward;
        }
      } else if (isThirdLayerHolder) {
        return chevronDown;
      }

      return "";
    }

    public bool CanExpandProjectMenu() {
      return ProjectInfo != null &&
        (ProjectPage != null || ProgramPage != null || QuotePage != null || CoacheePage != null
        || IsCurrentPageCoacheeList || IsCurrentPageEvalViewer || IsCurrentPageProgramContentDetails);
    }

    public bool CanExpandProgramMenu() {
      return ProgramPage != null || CoacheePage != null
        || (ProgramInfo != null && (IsCurrentPageCoacheeList || IsCurrentPageEvalViewer || IsCurrentPageProgramContentDetails));
    }

    public bool CanExpandDashboardMenu() {
      return Page is OverviewPageBase || PathHelper.IsCurrentPage(PathHelper.Pages.CoachReferrals())
        || (Page is CoacheeInfoBase && ProjectInfo == null && ProgramInfo == null && !SessionHelper.IsUserRoleAdmin)
        || IsDisplayingProfileInDashboard()
        || IsDisplayingCompanyFromDashboard();
    }

    public bool IsViewingContentPageFromProgram() {
      return ProgramInfo != null && (PathHelper.IsCurrentPage(PathHelper.Pages.Content()) || PathHelper.IsCurrentPage(PathHelper.Pages.ContentDetails(null)));
    }

    public bool IsDisplayingProfileInDashboard() {
      return (PathHelper.IsCurrentPage(PathHelper.Pages.CoachEdit()) && CoachPage.CoachInfo.UserId == UserInfo.UserId && SessionHelper.IsUserRoleClient && !SessionHelper.IsUserRoleLeader)
        || IsPagePeopleDetails_ForParticipant();
    }

    public bool IsDisplayingCompanyFromDashboard() {
      return PathHelper.IsCurrentPage(PathHelper.Pages.OrganisationSettings()) && CompanyPage.CanUpdateCompany && SessionHelper.IsUserRoleClient;
    }

    public void MenuItem(Action writeHtml) {
      SystemWeb.ResponseWriteLine($"<li>");
      writeHtml();
      SystemWeb.ResponseWriteLine($"</li>");
    }

    public void MenuItemWithSubMenu(string path, string text, string iconName, Action writeHtml) {
      if (path.IsNullOrEmpty()) return;

      SystemWeb.ResponseWriteLine($"<li class=\"{MenuStyles.SubmenuClass}\">");
      SystemWeb.ResponseWriteLine(TopMenuLink(path, text, iconName));
      writeHtml();
      SystemWeb.ResponseWriteLine($"</li>");
    }

    public void SubMenu(Action writeHtml) {
      SystemWeb.ResponseWriteLine($"<ul class=\"{MenuStyles.SubmenuClass}\">");
      writeHtml();
      SystemWeb.ResponseWriteLine($"</ul>");
    }

    public string GetOrganisationUrl() {

      if (SessionHelper.AppAccess.Companies.CanViewOrganisationListView()) {
        return PathHelper.Pages.Organisations();
      } else if (SessionHelper.AppAccess.Companies.CanViewOrganisationOverview(CompanyInfo)) {
        return PathHelper.Pages.OrganisationOverview(UserInfo.ClientCompanyId.Value);
      }

      return null;
    }

    private bool IsPagePeopleDetails_ForParticipant() {
      return (PathHelper.IsCurrentPage(PathHelper.Pages.PeopleDetails(null, null)) && SessionHelper.IsUserRoleLeader);
    }

    public string GetUpcomingUrl() {
      if (SessionHelper.AppAccess.PageAccess.CanAccessParticipantUpcoming()) {
        return PathHelper.Pages.ParticipantUpcoming();
      }
      return PathHelper.Pages.OverviewUpcoming();
    }

    public string GetUserRoleSubmenuHtml() {

      var userRoleOptions = SessionHelper.GetAvailableUserRoles();
      var currentUserRole = SessionHelper.GetUserRole();

      // If user doesn't have more than one role, don't show the dropdown.
      if (userRoleOptions.Count < 2) return "";

      string optionHtml = "", html = "";

      foreach (var userRole in userRoleOptions) {
        // Don't show the current role in the dropdown.
        if (userRole == currentUserRole) continue;
        string userRoleName = SessionHelper.GetUserRoleDisplayName(userRole);
        optionHtml += $@"<li class=""submenu-item""><a href=""#"" tabindex=""0"" class=""switch-user-role"" data-role=""{userRole.ToString().HTMLEncode()}"">{userRoleName.HTMLEncode()}</a></li>";
      }
      html = $@"
        <li class=""submenu treeview visible-xs"">
          <a href=""#"">
            <span><ion-icon name=""{WebHelper.MenuIconName[WebHelper.MenuIconTypeEnum.ChangeRole]}""></ion-icon><span class=""pl5"">Change Role</span></span>
          </a>
          <ul class=""treeview-menu submenu"">{optionHtml}</ul>
        </li>";

      return html;
    }
  }
}
