namespace Integral.Web.PortalSite.AppCode {

  // Holds the per-request layout/chrome data that the Site.Master and AdminLTE.Master
  // markup reads at render time. A single instance per request is shared between both
  // Framework master pages via LayoutModel.GetCurrent(), so a future Razor _Layout can
  // bind to the same object without changing the data shape.
  public class LayoutModel {

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

    private const string RequestItemKey = "Integral.Web.PortalSite.LayoutModel";

    public static LayoutModel GetCurrent() {
      if (AppHelper.GetRequestItemOrNull(RequestItemKey) is LayoutModel existing) return existing;
      var model = new LayoutModel();
      AppHelper.SetRequestItem(RequestItemKey, model);
      return model;
    }
  }
}
