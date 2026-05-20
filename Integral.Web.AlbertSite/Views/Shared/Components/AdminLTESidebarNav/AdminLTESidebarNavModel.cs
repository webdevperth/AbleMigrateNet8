using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.ViewComponents {

  // Model for the AdminLTESidebarNav ViewComponent. Mirrors the public API of
  // the legacy UserControls/AdminLTESidebarNav.ascx.cs codebehind. The legacy
  // helper methods (MenuItem, MenuItemWithSubMenu, SubMenu) used to write
  // directly to the response stream via SystemWeb.ResponseWriteLine — they
  // have been refactored here to return strings so the Razor view can compose
  // the navigation tree via @Html.Raw(Model.X).
  public class AdminLTESidebarNavModel {

    public static class MenuStyles {
      public const string SubmenuClass = "submenu";
      public const string SubmenuItemClass = "submenu-item";
      public const string SubmenuHasThirdLevel = " has-third-level";
      public const string SubmenuIsThirdLevel = " third-level";
      public const string AddActiveClass = " activePage";
      public const string RemoveHighlight = " removeHighlight";
    }

    public static class MenuItems {
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

    public string CurrentItemLine1 = string.Empty, CurrentItemLine2 = string.Empty;
    public int ViewingEntityId = 0;
    public bool IsCurrentPageCoacheeList, IsCurrentPageEvalViewer, IsCurrentPageProgramContentDetails;

    // State previously sourced from the host Page via casts. Now read from
    // LayoutModel, which the page base classes mirror onto during Page_Init.
    public DbHelper.AbleUser.AbleUserInfo UserInfo = null;
    public DbHelper.Projects.ProjectInfo ProjectInfo = null;
    public DbHelper.AblePrograms.AbleProgramInfo ProgramInfo = null;
    public DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo = null;
    public DbHelper.AlbertCoaches.AlbertCoachInfo CoachInfo = null;
    public DbHelper.ClientCompanies.AlbertCompanyInfo CompanyInfo = null;
    public DbHelper.AbleQuotes.QuoteInfo QuoteInfo = null;
    public DbHelper.TenantOrg.TenantOrgInfo TenantOrgInfo = null;

    public bool MenuThirdLayerActive_Program, DashboardMenuIsActive, ProjectMenuIsActive;

    // Replaces the legacy `Page is XPageBase` checks. Computed in Build() from
    // the mirrored LayoutModel state.
    public bool IsCoacheePage, IsProjectPage, IsProgramPage, IsQuotePage;
    public bool IsCoachPage, IsCompanyPage;
    public bool IsNewCoachee, IsNewProject, IsNewProgram, IsNewQuote, IsNewCompany;
    public bool CanViewQuoteInfo, CanUpdateCompany;

    // Mirrors the legacy Page_Load. Reads layout state and computes the
    // page-type / URL-derived flags the menu rendering depends on.
    public static AdminLTESidebarNavModel Build(LayoutModel layout) {

      var model = new AdminLTESidebarNavModel {
        UserInfo = layout.UserInfo,
        ProgramInfo = layout.ProgramInfo,
        ProjectInfo = layout.ProjectInfo,
        CoacheeInfo = layout.CoacheeInfo,
        CoachInfo = layout.CoachInfo,
        CompanyInfo = layout.CompanyInfo,
        QuoteInfo = layout.QuoteInfo,
        TenantOrgInfo = layout.TenantOrgInfo,
        ProjectMenuIsActive = layout.ProjectMenuIsActive,
        MenuThirdLayerActive_Program = layout.MenuThirdLayerActive_Programs,
        DashboardMenuIsActive = layout.DashboardMenuIsActive,
        IsNewCoachee = layout.IsNewCoachee,
        IsNewProject = layout.IsNewProject,
        IsNewProgram = layout.IsNewProgram,
        IsNewQuote = layout.IsNewQuote,
        IsNewCompany = layout.IsNewCompany,
        CanViewQuoteInfo = layout.CanViewQuoteInfo,
        CanUpdateCompany = layout.CanUpdateCompany
      };

      if (model.UserInfo == null) return model;

      model.IsCurrentPageCoacheeList = PathHelper.IsCurrentPage(PathHelper.Pages.Coachees());
      model.IsCurrentPageEvalViewer = PathHelper.IsCurrentPage(PathHelper.Reports.EvalViewer());
      model.IsCurrentPageProgramContentDetails = PathHelper.IsCurrentPage(PathHelper.Pages.ProgramContentDetails(null, null)) && model.ProgramInfo != null;

      // Determine which page-type branch the legacy code would have entered.
      // Each legacy `Page is X` check maps to a layout-state condition.
      model.IsCoacheePage = model.CoacheeInfo != null || model.IsNewCoachee;
      model.IsQuotePage = model.QuoteInfo != null || model.IsNewQuote;
      // ProgramPageBase sets ProgramInfo; this is null on Project/Quote pages.
      model.IsProgramPage = !model.IsCoacheePage && (model.IsNewProgram || (model.ProgramInfo != null && !model.IsQuotePage));
      // ProjectPageBase sets ProjectInfo without setting ProgramInfo or QuoteInfo.
      model.IsProjectPage = !model.IsCoacheePage && !model.IsProgramPage && !model.IsQuotePage
        && (model.IsNewProject || (model.ProjectInfo != null && model.ProgramInfo == null && model.QuoteInfo == null));
      model.IsCoachPage = model.CoachInfo != null;
      model.IsCompanyPage = model.CompanyInfo != null || model.IsNewCompany;

      // Mirrors the if/else if chain in the legacy Page_Load that picked the
      // CurrentItemLine1/2 + ViewingEntityId from whichever page type was active.
      if (model.IsCurrentPageCoacheeList || model.IsCurrentPageEvalViewer) {
        // ProjectInfo/ProgramInfo already taken from layout, nothing else to do.
      } else if (model.IsCoacheePage) {
        if (model.CoacheeInfo != null) {
          model.CurrentItemLine1 = model.CoacheeInfo.GetFullName();
          model.CurrentItemLine2 = model.CoacheeInfo.EmailAddress;
          model.ViewingEntityId = model.CoacheeInfo.CoacheeId;
        }
        if (model.ProjectInfo != null && model.ProgramInfo != null) {
          model.ProjectMenuIsActive = model.MenuThirdLayerActive_Program = true;
        }
      } else if (model.IsProjectPage) {
        if (model.ProjectInfo != null) {
          model.CurrentItemLine1 = model.ProjectInfo.JobNumber;
          model.CurrentItemLine2 = model.ProjectInfo.ProjectName;
          model.ViewingEntityId = model.ProjectInfo.ProjectId;
        }
      } else if (model.IsProgramPage) {
        if (model.ProgramInfo != null) {
          model.CurrentItemLine1 = model.ProgramInfo.ProgramJobNumber;
          model.CurrentItemLine2 = model.ProgramInfo.ProgramJobName + "\n" + model.ProgramInfo.CompanyName;
          model.ViewingEntityId = model.ProgramInfo.ProgramJobId;
        }
      } else if (model.IsQuotePage) {
        if (model.QuoteInfo != null) {
          model.CurrentItemLine1 = model.QuoteInfo.JobNumber;
          model.CurrentItemLine2 = model.QuoteInfo.QuoteTitle.ValueIfNullOrEmpty(model.QuoteInfo.ProjectName);
          model.ViewingEntityId = model.QuoteInfo.QuoteId;
        }
      } else if (model.IsCoachPage) {
        model.CurrentItemLine1 = model.CoachInfo.GetFullName();
        model.CurrentItemLine2 = model.CoachInfo.EmailAddress;
        model.ViewingEntityId = model.CoachInfo.UserId;
      } else if (model.IsCompanyPage) {
        if (model.CompanyInfo != null) {
          model.CurrentItemLine1 = model.CompanyInfo.CompanyName;
          model.CurrentItemLine2 = string.Empty;
          model.ViewingEntityId = model.CompanyInfo.CompanyId;
        }
      }

      return model;
    }

    public string GetOrganisationMenu() {

      if (!SessionHelper.AppAccess.Companies.CanViewOrganisationListView()) return "";

      bool canViewOverview = SessionHelper.AppAccess.Companies.CanViewOrganisationOverview(CompanyInfo);
      bool canViewSettings = SessionHelper.AppAccess.Companies.CanViewOrganisationSettings(CompanyInfo, isNewCompany: IsNewCompany);
      bool canViewDepartments = SessionHelper.AppAccess.Companies.CanViewOrganisationDepartments(CompanyInfo);
      bool canViewPeople = SessionHelper.AppAccess.Companies.CanViewOrganisationPeople(CompanyInfo);
      bool canViewProjects = SessionHelper.AppAccess.Companies.CanViewOrganisationProjects(CompanyInfo);
      bool canViewCapabilities = SessionHelper.AppAccess.Companies.CanViewOrganisationCapabilities(CompanyInfo);
      bool canViewIOSReports = SessionHelper.AppAccess.Companies.CanViewOrganisationIOSReports(CompanyInfo);

      string inner = "";

      if (IsCompanyPage && IsNewCompany) {
        inner = SubMenu(SubmenuItem(PathHelper.Pages.OrganisationSettings(true), "New Organisation", "flag-outline"));
      } else if (CompanyInfo != null) {

        if (canViewOverview) {
          inner += SubMenu(SubmenuItem(PathHelper.Pages.OrganisationOverview(true), MenuItems.OrganisationOverview, "bookmark-outline"));
        }

        if (canViewSettings) {
          inner += SubMenu(SubmenuItem(PathHelper.Pages.OrganisationSettings(true), "Settings", "flag-outline"));
        }
        if (canViewPeople) {
          inner += SubMenu(SubmenuItem(PathHelper.Pages.OrganisationPeople(true), "People", "people-outline"));
        }
        if (canViewDepartments) {
          inner += SubMenu(SubmenuItem(PathHelper.Pages.OrganisationDepartments(true), "Departments", "grid-outline"));
        }
        if (canViewProjects) {
          inner += SubMenu(SubmenuItem(PathHelper.Pages.OrganisationProjects(true), "Projects", "folder-open-outline",
            PathHelper.Pages.PeopleDetails(CompanyInfo.CompanyId, null)));
        }
        if (canViewCapabilities) {
          inner += SubMenu(SubmenuItem(PathHelper.Pages.OrganisationCapabilities(CompanyInfo.CompanyId), "Capabilities", "bar-chart-outline",
            PathHelper.Pages.OrganisationCapabilities(CompanyInfo.CompanyId)));
        }
        if (canViewIOSReports) {
          inner += SubMenu(SubmenuItem(PathHelper.Reports.OrganisationIOSReports(CompanyInfo.CompanyId), "IOS Reports", "bar-chart-outline"));
        }
      }

      return MenuItemWithSubMenu(GetOrganisationUrl(), "Organisation" + (SessionHelper.IsUserRoleClient ? "" : "s"), "business-outline", inner);
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
      } else if (IsCoacheePage && !isTopLevel) {
        return MenuStyles.AddActiveClass;
      } else if (IsCoacheePage && IsNewCoachee) {
        if (isTopLevel && ProgramInfo != null) return "";
        return MenuStyles.AddActiveClass;
      } else if (IsCurrentPageCoacheeList && ProgramInfo != null && !isTopLevel) {
        return MenuStyles.AddActiveClass;
      }

      return "";
    }

    public string QuotesHighlight(bool isTopLevel) {
      if (IsQuotePage) {
        if (IsNewQuote) {
          if (isTopLevel && ProjectInfo != null) return "";
          else if ((isTopLevel && ProjectInfo == null) || (!isTopLevel && ProjectInfo != null)) return MenuStyles.AddActiveClass;
        } else if (CanViewQuoteInfo && !isTopLevel) {
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
        (IsProjectPage || IsProgramPage || IsQuotePage || IsCoacheePage
        || IsCurrentPageCoacheeList || IsCurrentPageEvalViewer || IsCurrentPageProgramContentDetails);
    }

    public bool CanExpandProgramMenu() {
      return IsProgramPage || IsCoacheePage
        || (ProgramInfo != null && (IsCurrentPageCoacheeList || IsCurrentPageEvalViewer || IsCurrentPageProgramContentDetails));
    }

    public bool CanExpandDashboardMenu() {
      // The legacy code checked `Page is OverviewPageBase`. OverviewPageBase is the
      // only page type that sets DashboardMenuIsActive=true on Page_Init, so the
      // mirrored layout flag is a faithful proxy.
      return DashboardMenuIsActive || PathHelper.IsCurrentPage(PathHelper.Pages.CoachReferrals())
        || (IsCoacheePage && ProjectInfo == null && ProgramInfo == null && !SessionHelper.IsUserRoleAdmin)
        || IsDisplayingProfileInDashboard()
        || IsDisplayingCompanyFromDashboard();
    }

    public bool IsViewingContentPageFromProgram() {
      return ProgramInfo != null && (PathHelper.IsCurrentPage(PathHelper.Pages.Content()) || PathHelper.IsCurrentPage(PathHelper.Pages.ContentDetails(null)));
    }

    public bool IsDisplayingProfileInDashboard() {
      return (PathHelper.IsCurrentPage(PathHelper.Pages.CoachEdit()) && CoachInfo != null && CoachInfo.UserId == UserInfo.UserId && SessionHelper.IsUserRoleClient && !SessionHelper.IsUserRoleLeader)
        || IsPagePeopleDetails_ForParticipant();
    }

    public bool IsDisplayingCompanyFromDashboard() {
      return PathHelper.IsCurrentPage(PathHelper.Pages.OrganisationSettings()) && CanUpdateCompany && SessionHelper.IsUserRoleClient;
    }

    public string MenuItem(string innerHtml) {
      return $"<li>{innerHtml}</li>";
    }

    public string MenuItemWithSubMenu(string path, string text, string iconName, string innerHtml) {
      if (path.IsNullOrEmpty()) return "";
      return $"<li class=\"{MenuStyles.SubmenuClass}\">" + TopMenuLink(path, text, iconName) + innerHtml + "</li>";
    }

    public string SubMenu(string innerHtml) {
      return $"<ul class=\"{MenuStyles.SubmenuClass}\">{innerHtml}</ul>";
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
