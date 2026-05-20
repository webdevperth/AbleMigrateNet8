namespace Integral.Web.PortalSite.ViewComponents {

  // Codifies the ?ctrlname= URL contract used by the legacy code that calls
  // `LoadControl("~/UserControls/" + RequestedCtrlName + ".ascx")` for the
  // ChartAlbert360_* family. When the host page is migrated to a Razor Page or
  // controller, this dispatcher will map the requested control name to a
  // ViewComponent name without leaking string-typed .ascx paths into the new
  // code.
  public static class ChartAlbert360PartialDispatcher {

    public static string GetViewComponentName(string ctrlName) =>
      ctrlName?.ToLowerInvariant() switch {
        "chartalbert360detailed"       => nameof(ChartAlbert360Detailed),
        "chartalbert360focus"          => nameof(ChartAlbert360Focus),
        "chartalbert360functiontable"  => nameof(ChartAlbert360FunctionTable),
        "chartalbert360scoreovertime"  => nameof(ChartAlbert360ScoreOverTime),
        "chartalbert360topnav"         => nameof(ChartAlbert360TopNav),
        _ => null
      };

  }
}
