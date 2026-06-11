namespace Integral.Web.PortalSite.AppCode {

  // Holds per-request common values set on a page level, that need to be accessed by 
  // the page's layout template, the site menu and possibly other components.
  public class LayoutModel {

    // Per-request "Singleton" using the request items collection.
    public static LayoutModel GetCurrent() {
      if (AppHelper.GetRequestItemOrNull(RequestItemKey) is LayoutModel existing) return existing;
      var model = new LayoutModel();
      AppHelper.SetRequestItem(RequestItemKey, model);
      return model;
    }

    public string BrowserPageTitle { get; set; } = "";
    public bool MasterContentHeaderVisible { get; set; } = false;
    public string PageBreadcrumbHtml { get; set; } = "";
    public bool CanViewBreadcrumb { get; set; } = false;
    public bool NoMinHeaderHeight { get; set; } = false;
    public string PageTitle { get; set; } = "";
    public string PageTitle_Mobile { get; set; } = "";
    public string PageSubSubtitleHTML { get; set; } = "";
    public string PageMessageText { get; set; } = "";
    public AjaxSubmitHelper.PageMessageType PageMessageType { get; set; } = AjaxSubmitHelper.PageMessageType.None;

    // BodyClass is built up additively across the request via WebHelper.AddBodyClass,
    // which writes to HttpContext.Items. Expose it here so the layout markup reads the
    // model uniformly; the underlying storage stays put so existing callers don't change.
    public string BodyClass {
      get { return WebHelper.GetBodyClass(); }
    }

    // The properties below replace the `Page is LoggedInPageBase` cast performed by
    // AdminLTESidebarNav.ascx.cs (and the other host-page reads done by the sidebar/
    // header nav). Legacy page base classes mirror their own properties onto these
    // during Page_Init so a future ViewComponent — which has no host Page to cast —
    // can read the same per-request state via LayoutModel.GetCurrent().

    /// <summary>Current logged-in user. Mirrors LoggedInPageBase.userInfo.</summary>
    public DbHelper.AbleUser.AbleUserInfo UserInfo { get; set; } = null;

    /// <summary>Program in scope for the current request. Mirrors LoggedInPageBase.ProgramInfo.</summary>
    public DbHelper.AblePrograms.AbleProgramInfo ProgramInfo { get; set; } = null;

    /// <summary>Project in scope for the current request. Mirrors LoggedInPageBase.ProjectInfo.</summary>
    public DbHelper.Projects.ProjectInfo ProjectInfo { get; set; } = null;

    /// <summary>Coach (Partner) in scope for the current request. Mirrors CoachInfoBase.CoachInfo.</summary>
    public DbHelper.AlbertCoaches.AlbertCoachInfo CoachInfo { get; set; } = null;

    /// <summary>Coachee (Participant) in scope for the current request. Mirrors CoacheeInfoBase.CoacheeInfo.</summary>
    public DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo { get; set; } = null;

    /// <summary>Company (Organisation) in scope for the current request. Mirrors CompanyInfoBase.CompanyInfo.</summary>
    public DbHelper.ClientCompanies.AlbertCompanyInfo CompanyInfo { get; set; } = null;

    /// <summary>Quote in scope for the current request. Mirrors QuotePageBase.QuoteInfo.</summary>
    public DbHelper.AbleQuotes.QuoteInfo QuoteInfo { get; set; } = null;

    /// <summary>True when the current request is a dashboard area page. Mirrors LoggedInPageBase.DashboardMenuIsActive.</summary>
    public bool DashboardMenuIsActive { get; set; } = false;

    /// <summary>True when the current request is a project area page. Mirrors LoggedInPageBase.ProjectMenuIsActive.</summary>
    public bool ProjectMenuIsActive { get; set; } = false;

    /// <summary>True when the current request is inside the Programs third-layer menu. Mirrors LoggedInPageBase.MenuThirdLayerActive_Programs.</summary>
    public bool MenuThirdLayerActive_Programs { get; set; } = false;

    /// <summary>True when the current request is a Leader viewing a survey that was shared with them. Mirrors LoggedInPageBase.IsViewingSharedSurvey.</summary>
    public bool IsViewingSharedSurvey { get; set; } = false;

    /// <summary>Shared-survey record backing IsViewingSharedSurvey. Mirrors LoggedInPageBase.SharedSurveyInfo.</summary>
    public DbHelper.SurveyShare.SharedSurveysInfo SharedSurveyInfo { get; set; } = null;

    /// <summary>True when the current request is adding a new Participant. Mirrors CoacheeInfoBase.IsNewCoachee.</summary>
    public bool IsNewCoachee { get; set; } = false;

    /// <summary>True when the current request is adding a new Project. Mirrors ProjectPageBase.IsNewProject.</summary>
    public bool IsNewProject { get; set; } = false;

    /// <summary>True when the current request is adding a new Program. Mirrors ProgramPageBase.IsNewProgram.</summary>
    public bool IsNewProgram { get; set; } = false;

    /// <summary>True when the current request is adding a new Quote. Mirrors QuotePageBase.IsNewQuote.</summary>
    public bool IsNewQuote { get; set; } = false;

    /// <summary>True when the current user can view the Quote in scope. Mirrors QuotePageBase.CanViewQuoteInfo.</summary>
    public bool CanViewQuoteInfo { get; set; } = false;

    /// <summary>True when the current request is adding a new Company. Mirrors CompanyInfoBase.IsNewCompany.</summary>
    public bool IsNewCompany { get; set; } = false;

    /// <summary>True when the current user can update the Company in scope. Mirrors CompanyInfoBase.CanUpdateCompany.</summary>
    public bool CanUpdateCompany { get; set; } = false;

    /// <summary>Tenant Org for the current request. Mirrors SettingsPageBase.TenantOrgInfo. Non-null implies the page is a SettingsPageBase.</summary>
    public DbHelper.TenantOrg.TenantOrgInfo TenantOrgInfo { get; set; } = null;

    private const string RequestItemKey = "Integral.Web.PortalSite.LayoutModel";
  }
}
