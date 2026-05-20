using System;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class ProjectPageBase : LoggedInPageBase {

    public bool IsNewProject { get; protected set; }

    protected override void Page_Init(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      base.Page_Init(sender, e);
      ProjectMenuIsActive = true;
      IsNewProject = false;
      ProjectInfo = null;

      // Mirror onto LayoutModel for future ViewComponent consumers. See LayoutModel.cs.
      var layout = LayoutModel.GetCurrent();
      layout.ProjectMenuIsActive = ProjectMenuIsActive;
      layout.ProjectInfo = ProjectInfo;

      // Is there a job id or "new" in the querystring?
      string urlProjectJobNumber = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProjectJobNumber);

      IsNewProject = urlProjectJobNumber == PathHelper.AbleUrlValues.IdNew;
      layout.IsNewProject = IsNewProject;

      // Get project data if an id is given, or new Project object if adding a new one.
      if (IsNewProject) {

        if (!SessionHelper.AppAccess.Projects.CanCreateNewProject()) {
          SetFallbackRedirect();
          return;
        }

        ProjectInfo = DbHelper.Projects.GetNewProjectInfo();
        layout.ProjectInfo = ProjectInfo;

      } else {

        ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(urlProjectJobNumber, SessionHelper.UserInfo);
        layout.ProjectInfo = ProjectInfo;

        if (ProjectInfo == null) {
          SetFallbackRedirect();
          return;
        }
      }

      // Check user access to Project pages.
      if (!CheckUserPageAccess()) {
        SetFallbackRedirect();
        return;
      }
    }

    internal bool CheckUserPageAccess() {

      if (!SessionHelper.AppAccess.PageAccess.CanAccessProjectLevel()) return false;

      if (IsNewProject) {
        // New project can only see settings page.
        return PathHelper.IsCurrentPage(PathHelper.Pages.ProjectSettings());
      }

      if (!SessionHelper.AppAccess.Projects.CanViewProject(ProjectInfo)) return false;

      if (PathHelper.IsCurrentPage(PathHelper.Pages.ProjectSettings())) {
        // Check access to project settings page.
        if (!SessionHelper.AppAccess.PageAccess.CanAccessProjectSettings(ProjectInfo)) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProjectAccess_List())) {
        // Check access to project access page.
        if (!SessionHelper.AppAccess.PageAccess.CanAccessProjectAccess(ProjectInfo)) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProjectComponents())) {
        // Check access to project components page.
        if (!SessionHelper.AppAccess.PageAccess.CanAccessProjectComponents()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProjectCustomise())) {
        // Check access to project customise page.
        if (!SessionHelper.AppAccess.Projects.CanViewProjectCustomise(ProjectInfo)) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProjectInvoicing())) {
        // Check access to project invoicing page.
        if (!SessionHelper.AppAccess.Projects.CanViewProjectInvoicing(ProjectInfo)) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProjectPrograms())) {
        // Check access to project programs page.
        if (!SessionHelper.AppAccess.PageAccess.CanAccessProjectPrograms()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ProjectQuotes())) {
        // Check access to project quotes page.
        if (!SessionHelper.AppAccess.PageAccess.CanAccessProjectQuotes()) return false;

      }

      return true;
    }
  }
}
