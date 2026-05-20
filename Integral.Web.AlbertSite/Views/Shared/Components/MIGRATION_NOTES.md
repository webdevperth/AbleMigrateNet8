# ASCX → ViewComponent migration notes

## Summary

The ViewComponent migration of `/UserControls/` is complete. Every `.ascx`
user control that was in scope has been ported to an ASP.NET Core
ViewComponent under `/Views/Shared/Components/`, and the original `.ascx`
and `.ascx.cs` files have been removed from disk.

For per-control details (class name, `?ctrlname=` value, dispatcher
mapping), see the **Status** section of [README.md](README.md).

## Counts

- **17 controls migrated** to ViewComponents:
  - 9 OrgRpt_* controls (the Organisation Report family)
  - 5 ChartAlbert360_* controls (the Coachee 360 chart family)
  - `SurveyForm`
  - `AdminLTEHeaderNav`, `AdminLTESidebarNav` (layout chrome)
- **1 control removed without migration**:
  - `AlbertRptPublic360Comments.ascx` — verified dead (no references in
    any `.aspx`, `.cs`, `.cshtml`, JS dispatcher map, or partial-loader
    URL).
- **1 helper class removed**:
  - `AppCode/OrgReportControlBase.cs` — superseded by the request-scoped
    [`AppCode/OrgReportContext.cs`](../../../AppCode/OrgReportContext.cs).

## Outstanding work for the next phase

The ViewComponents exist and compile, but the legacy `.aspx` host pages
still drive the actual HTTP traffic. The next migration phase needs to
move the hosting layer to Razor:

1. **Convert the three host pages to Razor Pages or controller actions:**
   - `Pages/OrganisationIOSReports.aspx`
   - `Pages/OrgReport.aspx`
   - `Pages/InsightsIOSReports.aspx`

   Each of these currently calls
   `LoadControl("~/UserControls/" + RequestedCtrlName + ".ascx").RenderToString()`
   to render a partial in response to a `?ctrlname=...` AJAX request.
   The Razor replacement should:
   - Read the `ctrlname` query parameter.
   - Resolve it to a ViewComponent name via
     `OrgReportPartialDispatcher.GetViewComponentName(ctrlName)` (or
     `ChartAlbert360PartialDispatcher` for the chart-family pages).
   - Render with `await Component.InvokeAsync(viewComponentName)` and
     return the resulting HTML.

   Both dispatcher classes already exist next to the ViewComponents and
   return `null` for unknown control names — callers should 404 on a
   `null` result.

2. **Build the Razor `_Layout.cshtml`** that invokes the two layout
   ViewComponents (`AdminLTEHeaderNav`, `AdminLTESidebarNav`) where the
   old AdminLTE Master Page rendered them. These two ViewComponents take
   their data from a `LayoutModel` (see
   [`AppCode/LayoutModel.cs`](../../../AppCode/LayoutModel.cs)).

3. **Wire `SurveyForm` POST handling.** `SurveyForm/SurveyFormPostHandler.cs`
   is plumbed (it carries the form-submit logic that used to live in
   the `SurveyForm.ascx.cs` Page_Load when `IsPostBack`), but **no
   endpoint currently invokes it**. The next phase needs a Razor Page
   handler (or controller action) that calls
   `SurveyFormPostHandler.Process(...)` when the survey form is POSTed.
   Until that wiring exists, the `SurveyForm` ViewComponent can render
   the form but the submission round-trip is non-functional.

## Known gaps

- **`SurveyFormPostHandler` is plumbed but not yet called.** See item
  3 above.
- **The `Compile Remove="**\*.ascx.cs"` rule remains in the csproj.**
  Although `/UserControls/` is now empty of `.ascx.cs` files, the
  `/Partials/` folder still contains `.aspx` host pages with `.aspx.cs`
  codebehinds that are out of scope for this migration; the rule is
  still required for those, alongside the sibling
  `<Compile Remove="**\*.aspx.cs" />` and `<Compile Remove="**\*.Master.cs" />`
  rules.
- **The empty `/UserControls/` directory remains on disk.** It is
  harmless and will disappear naturally when the folder is removed by
  any cleanup tool or commit; no project file references it.
