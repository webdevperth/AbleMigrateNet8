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

The original `.ascx` and `.ascx.cs` files **remain on disk** during the
migration so that legacy `.aspx` pages can keep referencing them if the
old Framework build is restored for diff/reference purposes. The
codebehinds are already excluded from compilation via
`<Compile Remove="**\*.ascx.cs" />` in the csproj.

The `.ascx` files will be deleted in the final cleanup migration step,
once every page that referenced them has been moved to Razor.
