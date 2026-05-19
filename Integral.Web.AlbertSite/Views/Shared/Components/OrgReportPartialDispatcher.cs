namespace Integral.Web.PortalSite.ViewComponents {

  // Codifies the ?ctrlname= URL contract used by Pages/OrganisationIOSReports.aspx,
  // Pages/OrgReport.aspx, and Pages/InsightsIOSReports.aspx (where the legacy code
  // calls `LoadControl("~/UserControls/" + RequestedCtrlName + ".ascx")`). When the
  // host page is migrated to a Razor Page or controller, this dispatcher will map
  // the requested control name to a ViewComponent name without leaking string-typed
  // .ascx paths into the new code.
  public static class OrgReportPartialDispatcher {

    public static string GetViewComponentName(string ctrlName) =>
      ctrlName?.ToLowerInvariant() switch {
        "orgrpt_topfilters"      => nameof(OrgRpt_TopFilters),
        "orgrpt_detailed"        => nameof(OrgRpt_Detailed),
        "orgrpt_focus"           => nameof(OrgRpt_Focus),
        "orgrpt_categories"      => nameof(OrgRpt_Categories),
        "orgrpt_comments"        => nameof(OrgRpt_Comments),
        "orgrpt_heatmap"         => nameof(OrgRpt_HeatMap),
        "orgrpt_ovw_topchart"    => nameof(OrgRpt_Ovw_TopChart),
        "orgrpt_ovw_ioidirs"     => nameof(OrgRpt_Ovw_IOIDirs),
        "orgrpt_ovw_quadrants"   => nameof(OrgRpt_Ovw_Quadrants),
        _ => null
      };

  }
}
