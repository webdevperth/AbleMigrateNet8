# ViewComponent conventions

This document captures the conventions used when migrating legacy Web Forms
`.ascx` user controls in `/UserControls/`, `/Partials/`, and `/MasterPages/` to
ASP.NET Core 8 ViewComponents.

The goal is to keep the C# class, the Razor view, and any small model class
for a single component all in one folder, named after the original `.ascx`.

## Folder layout

For each component:

```
Views/
  Shared/
    Components/
      {OriginalAscxName}/
        {OriginalAscxName}.cs        ← ViewComponent C# class
        {OriginalAscxName}Model.cs   ← optional POCO (see below)
        Default.cshtml               ← Razor view
```

This is the framework's default discovery location for ViewComponent views
(`/Views/Shared/Components/{ComponentName}/{ViewName}.cshtml`), so no extra
view-location configuration is required.

The pilot example is
[ChartAlbert360TopNav](/Views/Shared/Components/ChartAlbert360TopNav/).

## Naming convention

- The class name is the **original `.ascx` file name**, unchanged. Do **not**
  add a `ViewComponent` suffix to the class name or the file name.
  e.g. `ChartAlbert360TopNav.ascx` → class `ChartAlbert360TopNav`.
- The view file is always `Default.cshtml` unless a component returns
  multiple named views (in which case use the view name in `View("Foo")`).
- The namespace for all ViewComponent classes is
  `Integral.Web.PortalSite.ViewComponents`.
- Classes derive from `Microsoft.AspNetCore.Mvc.ViewComponent` and expose
  an `Invoke()` (or `InvokeAsync()`) method that returns
  `IViewComponentResult` via `View(...)` or `View(model)`.

## Model classes

Each component model is a plain POCO (no behaviour, just public
get/set properties).

- **Inline on the ViewComponent class** when the model has only two or three
  public members — declare it as a nested `public class` inside the
  ViewComponent, or just pass the values as method args to `View(...)`.
- **Separate `{OriginalAscxName}Model.cs`** in the component's folder when
  the model has more than two or three public members, or when the model is
  shared between the C# class and the Razor view in a way that benefits
  from a dedicated file.

In both cases the model lives in `Integral.Web.PortalSite.ViewComponents`
so the Razor view can reference it via the `@using` already declared in
`/Views/_ViewImports.cshtml`.

**Important Note:** Although the namespace for all ViewComponent classes is
  `Integral.Web.PortalSite.ViewComponents`, the files must reside in
   `/Views/Shared/Components/{ComponentName}/` **not** `/ViewComponents/{ComponentName}/`.

## Translating `.ascx` markup → Razor

The mechanical translations from Web Forms inline-code syntax to Razor:

| Web Forms                           | Razor                                                                                                                                    |
| ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `<%= expr %>`                       | `@(expr)`                                                                                                                                |
| `<%: expr %>` (HTML-encoded)        | `@expr` (Razor encodes by default)                                                                                                       |
| `<% if (...) { %> ... <% } %>`      | `@if (...) { ... }`                                                                                                                      |
| `<% foreach (...) { %> ... <% } %>` | `@foreach (...) { ... }`                                                                                                                 |
| `<% var x = ...; %>`                | `@{ var x = ...; }`                                                                                                                      |
| `<% void Foo() { %> ... <% } %>`    | `@{ void Foo() { ... } }` (local fn), or — preferably — extract a helper method onto the ViewComponent's model and call it from the view |

Drop `<%@ Control ... %>` and any `runat="server"` attributes — they have
no meaning in Razor.

For helper methods that build HTML, prefer to either:

1. Move the helper onto the ViewComponent's model as a method that returns
   `string` (or `HtmlString` if it returns pre-built HTML), and call it
   from Razor as `@Html.Raw(Model.GetThing())` (or `@Model.GetThing()` if
   it's plain text), or
2. Convert it to a Razor block in the `.cshtml` file directly.

Avoid sprinkling local functions across the markup — keep the view as
close to plain HTML + bindings as possible.

## Legacy `.ascx` files

The original `.ascx` and `.ascx.cs` files under `/UserControls/` have been
**deleted** at the end of the ViewComponent migration. The `.ascx` codebehind
exclusion remains in the csproj:

```xml
<Compile Remove="**\*.ascx.cs" />
```

It is still required because `/Partials/` contains `.aspx` host pages with
`.aspx.cs` codebehinds that are out of scope for this migration phase.

## Status

Every `.ascx` user control formerly under `/UserControls/` has been migrated
to a ViewComponent. The original `.ascx` and `.ascx.cs` files have been
removed from disk.

The `?ctrlname=...` AJAX URL contract used by the legacy
`LoadControl("~/UserControls/" + RequestedCtrlName + ".ascx")` callers is
preserved by two dispatcher classes. The future page-migration phase
should use these dispatchers (rather than re-coding the string-to-class
mapping) when wiring host pages to `Component.InvokeAsync`.

### OrgRpt_* family — dispatcher: `OrgReportPartialDispatcher`

Request-scoped data is loaded once per request via
[`OrgReportContext`](../../../AppCode/OrgReportContext.cs) (`GetOrLoad`).

| Original `.ascx`              | ViewComponent class      | `?ctrlname=` value      |
| ----------------------------- | ------------------------ | ----------------------- |
| `OrgRpt_TopFilters.ascx`      | `OrgRpt_TopFilters`      | `orgrpt_topfilters`     |
| `OrgRpt_Detailed.ascx`        | `OrgRpt_Detailed`        | `orgrpt_detailed`       |
| `OrgRpt_Focus.ascx`           | `OrgRpt_Focus`           | `orgrpt_focus`          |
| `OrgRpt_Categories.ascx`      | `OrgRpt_Categories`      | `orgrpt_categories`     |
| `OrgRpt_Comments.ascx`        | `OrgRpt_Comments`        | `orgrpt_comments`       |
| `OrgRpt_HeatMap.ascx`         | `OrgRpt_HeatMap`         | `orgrpt_heatmap`        |
| `OrgRpt_Ovw_TopChart.ascx`    | `OrgRpt_Ovw_TopChart`    | `orgrpt_ovw_topchart`   |
| `OrgRpt_Ovw_IOIDirs.ascx`     | `OrgRpt_Ovw_IOIDirs`     | `orgrpt_ovw_ioidirs`    |
| `OrgRpt_Ovw_Quadrants.ascx`   | `OrgRpt_Ovw_Quadrants`   | `orgrpt_ovw_quadrants`  |

### ChartAlbert360_* family — dispatcher: `ChartAlbert360PartialDispatcher`

Request-scoped data is loaded once per request via
[`Coachee360Context`](../../../AppCode/Coachee360Context.cs) (`GetOrLoad`).

| Original `.ascx`                      | ViewComponent class            | `?ctrlname=` value             |
| ------------------------------------- | ------------------------------ | ------------------------------ |
| `ChartAlbert360TopNav.ascx`           | `ChartAlbert360TopNav`         | `chartalbert360topnav`         |
| `ChartAlbert360Detailed.ascx`         | `ChartAlbert360Detailed`       | `chartalbert360detailed`       |
| `ChartAlbert360Focus.ascx`            | `ChartAlbert360Focus`          | `chartalbert360focus`          |
| `ChartAlbert360FunctionTable.ascx`    | `ChartAlbert360FunctionTable`  | `chartalbert360functiontable`  |
| `ChartAlbert360ScoreOverTime.ascx`    | `ChartAlbert360ScoreOverTime`  | `chartalbert360scoreovertime`  |

### Standalone components — no dispatcher

These controls are invoked directly (not through the `?ctrlname=` AJAX
contract), so callers should use `await Component.InvokeAsync(nameof(...))`
against the class name directly.

| Original `.ascx`             | ViewComponent class    | Notes                                                                                                  |
| ---------------------------- | ---------------------- | ------------------------------------------------------------------------------------------------------ |
| `SurveyForm.ascx`            | `SurveyForm`           | POST handling is plumbed in `SurveyFormPostHandler` but not yet wired to any endpoint — see notes.     |
| `AdminLTEHeaderNav.ascx`     | `AdminLTEHeaderNav`    | Invoked from the future Razor `_Layout.cshtml`.                                                        |
| `AdminLTESidebarNav.ascx`    | `AdminLTESidebarNav`   | Invoked from the future Razor `_Layout.cshtml`.                                                        |

### Removed (no migration)

| Original `.ascx`                       | Reason                                                                                                  |
| -------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| `AlbertRptPublic360Comments.ascx`      | Dead code — no references in any `.aspx`, `.cs`, `.cshtml`, JS dispatcher map, or anywhere else.        |
