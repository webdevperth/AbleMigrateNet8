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

    private const string RequestItemKey = "Integral.Web.PortalSite.LayoutModel";

    public static LayoutModel GetCurrent() {
      if (AppHelper.GetRequestItemOrNull(RequestItemKey) is LayoutModel existing) return existing;
      var model = new LayoutModel();
      AppHelper.SetRequestItem(RequestItemKey, model);
      return model;
    }
  }
}
