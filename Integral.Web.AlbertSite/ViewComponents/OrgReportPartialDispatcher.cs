namespace Integral.Web.PortalSite.ViewComponents {

  // Codifies the ?ctrlname= URL contract used by Pages/OrganisationIOSReports.aspx,
  // Pages/OrgReport.aspx, and Pages/InsightsIOSReports.aspx (where the legacy code
  // calls `LoadControl("~/UserControls/" + RequestedCtrlName + ".ascx")`). When the
  // host page is migrated to a Razor Page or controller, this dispatcher will map
  // the requested control name to a ViewComponent name without leaking string-typed
  // .ascx paths into the new code.
  //
  // Only the five ViewComponents migrated in this step are listed. The remaining
  // OrgRpt_HeatMap / OrgRpt_Ovw_TopChart / OrgRpt_Ovw_IOIDirs / OrgRpt_Ovw_Quadrants
  // entries are added in the next migration step.
  public static class OrgReportPartialDispatcher {

    public static string GetViewComponentName(string ctrlName) =>
      ctrlName?.ToLowerInvariant() switch {
        "orgrpt_topfilters" => nameof(OrgRpt_TopFiltersViewComponent),
        "orgrpt_detailed"   => nameof(OrgRpt_DetailedViewComponent),
        "orgrpt_focus"      => nameof(OrgRpt_FocusViewComponent),
        "orgrpt_categories" => nameof(OrgRpt_CategoriesViewComponent),
        "orgrpt_comments"   => nameof(OrgRpt_CommentsViewComponent),
        _ => null
      };

  }
}
