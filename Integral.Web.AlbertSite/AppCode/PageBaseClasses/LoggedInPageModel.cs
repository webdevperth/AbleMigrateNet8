using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Threading.Tasks;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class LoggedInPageModel : PageModel {

    public string PageTitle {
      get {
        return SystemWeb.GetRequestItemValue(ConfigHelper.RequestItems.PageTitle).ToString();
      }
      set {
        SystemWeb.SetRequestItemValue(ConfigHelper.RequestItems.PageTitle, value);
      }
    }

    public string PageTitle_Mobile { get; set; } = "";
    public string PageSubtitle { get; protected set; } = "";
    public string PageSubSubtitleHTML { get; protected set; } = "";
    public bool PageSubtitleIsHtml { get; protected set; } = false;
    public string PageAjaxAction { get; protected set; } = "";
    public string FallbackUrl { get; protected set; } = "";
    public bool MenuThirdLayerActive_Programs { get; protected set; } = false;
    public bool DashboardMenuIsActive { get; protected set; } = false;
    public bool ProjectMenuIsActive { get; protected set; } = false;
    public DbHelper.AblePrograms.AbleProgramInfo ProgramInfo { get; protected set; } = null;
    public DbHelper.Projects.ProjectInfo ProjectInfo { get; protected set; } = null;
    public DbHelper.AbleUser.AbleUserInfo userInfo { get; protected set; }
    public bool IsViewingSharedSurvey { get; protected set; } = false;
    public DbHelper.SurveyShare.SharedSurveysInfo SharedSurveyInfo { get; protected set; } = null;

    public override async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context, PageHandlerExecutionDelegate next) {

      InitializePage();
      if (WebHelper.IsRequestExiting()) {
        // Caller (SetRedirect/SetFallbackRedirect) has set Response.Headers.Location and ended the request.
        context.Result = new EmptyResult();
        return;
      }
      await next();
    }

    protected virtual void InitializePage() {

      SystemWeb.SetRequestItemValue(ConfigHelper.RequestItems.IsLoggedInPage, true);

      // Init.
      MenuThirdLayerActive_Programs = DashboardMenuIsActive = ProjectMenuIsActive = false;
      ProgramInfo = null;
      ProjectInfo = null;

      // Get user from session if it exists.
      userInfo = SessionHelper.GetUserInfoOrNull();

      // Mirror onto the request-scoped LayoutModel so future ViewComponents (which have
      // no host Page to cast) can read the same state. See LayoutModel.cs.
      var layout = LayoutModel.GetCurrent();
      layout.MenuThirdLayerActive_Programs = MenuThirdLayerActive_Programs;
      layout.DashboardMenuIsActive = DashboardMenuIsActive;
      layout.ProjectMenuIsActive = ProjectMenuIsActive;
      layout.ProgramInfo = ProgramInfo;
      layout.ProjectInfo = ProjectInfo;
      layout.UserInfo = userInfo;

      // For convenience at the page level, set PageAjaxAction string from the commonly-used "AjaxAction" form field.
      if (SystemWeb.IsHttpPost) PageAjaxAction = WebHelper.GetAjaxaction();

      // If not logged in, perform second check for a report-specific login.
      if (userInfo == null) {
        // Url needs to contain the coachee Guid that is requesting the report.

        if (Guid.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeGuid, ""), out Guid coacheeGuid)) {
          if (SessionHelper.PublicReport.GetIsLoggedIn(coacheeGuid)) {
            return; // Coachee has logged into their public report, nothing more to check.
          }
        }

      } else if (userInfo.IsSoftDeleted) {
        // If user is deleted, log them out.

        SessionHelper.LogOut();
        SetRedirect(PathHelper.WebRoot);
        return;
      }

      if (SessionHelper.RedirectIfNotLoggedIn(PathHelper.WebRoot)) {
        return;
      }

      // FallbackUrl first set here as the user's "landing page" after login.
      // In different app areas (project, program, etc) the user can visit,
      // FallbackUrl changes to the "home" of that area (see the area base classes).
      FallbackUrl = PathHelper.GetDefaultPostLoginURL(userInfo);

      // Check user access to Top Level pages.
      if (!CheckPageAccess()) {
        // User doesn't have access to the intended page in their current role.
        // Howver, for convenience, if trying to view a Participant page but currently not in that role,
        // change the role to Leader automatically so they can proceed (i.e. so they don't have to manually change it).
        // TODO: Lose this role-changing crap for coaches & participants, simply decide by access rules instead.
        if (PathHelper.IsParticipantPage(PathHelper.CurrentUrl)
          && userInfo.IsParticipant
          && !SessionHelper.IsUserRoleLeader) {
          // Change user role to Participant and redirect to the page.
          SessionHelper.SetUserRole(ConfigHelper.UserRole.Leader);
        } else {
          // Redirect to fallback page.
          SetFallbackRedirect();
          return;
        }
      }


    }

    protected void GetSharedSurveyInfo() {

      SharedSurveyInfo = null;
      IsViewingSharedSurvey = false;

      var surveyShareIdUrl = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.SurveyShareId, null);
      if (surveyShareIdUrl != null) {
        SharedSurveyInfo = DbHelper.SurveyShare.GetSharedSurveyInfo(surveyShareIdUrl.Value);
        if (SharedSurveyInfo != null && SharedSurveyInfo.UserIdSharedWith == SessionHelper.UserInfo?.UserId) {
          IsViewingSharedSurvey = true;
        }
      }

      // Mirror onto LayoutModel for future ViewComponent consumers.
      var layout = LayoutModel.GetCurrent();
      layout.SharedSurveyInfo = SharedSurveyInfo;
      layout.IsViewingSharedSurvey = IsViewingSharedSurvey;
    }

    protected bool CheckPageAccess() {

      // Check for self registered users, as some will not be able to navigate able until they complete an action.
      if ((SessionHelper.IsUserRoleClient && !PathHelper.IsCurrentPage(PathHelper.Pages.OverviewUpcoming()))
        || (SessionHelper.IsUserRoleLeader && (!PathHelper.IsCurrentPage(PathHelper.Pages.ParticipantUpcoming()) && !PathHelper.IsCurrentPage(PathHelper.Partials.PurchaseServices())))) {
        if (!SessionHelper.AppAccess.Users.CanNavigatePlatform()) return false;
      }

      // Restricted pages...

      if (PathHelper.IsCurrentPage(PathHelper.Pages.Coachees())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessParticipantsList()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.Projects())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessProjectLevel()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.QuoteList())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessQuoteLevel()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.Coaches())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessPartnersLevel()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.Organisations())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessOrganisationLevel()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Reports.Quality())) {
        if (!SessionHelper.AppAccess.Insights.CanViewQuality()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Reports.SurveyViewer())) {
        if (!SessionHelper.AppAccess.Insights.CanViewSurveyViewer()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Reports.SkillsViewer())) {
        if (!SessionHelper.AppAccess.Insights.CanViewSkillsViewer()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.Surveys())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessSurveysLevel()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ParticipantSurveys())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessParticipantSurveys()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ParticipantAICoach())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessParticipantAICoach()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ParticipantUpcoming())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessParticipantUpcoming()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.AdminTools())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessAdminTools()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.Content()) || PathHelper.IsCurrentPage(PathHelper.Pages.ContentDetails(null))) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessContentPage()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ModuleEdit_AddContent())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessModuleEdit()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.Module(null))) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessModule()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.DevelopmentPlan()) || PathHelper.IsCurrentPage(PathHelper.Pages.DevelopmentPlanForm())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessDevelopmentPlan()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ParticipantCoaching())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessParticipantCoaching()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Reports.OldIOSReport(null))) {
        if (!SessionHelper.AppAccess.Reports.CanViewOldIOSReport()) return false;
      }

      // Rest are assumed to be accessible to all users.
      return true;
    }

    protected void SetFallbackRedirectNoAccess(string message = "You do not have access to this resource.") {
      SetFallbackRedirect(message);
    }

    protected void SetFallbackRedirect(string message = null) {
      SetRedirect(FallbackUrl, message);
    }

    protected void SetRedirectToReferrer() {
      string referrerUrl = WebHelper.GetReferrerUri()?.AbsoluteUri;
      if (!referrerUrl.IsNullOrEmpty()) {
        SetRedirect(referrerUrl);
      } else {
        SetRedirect(PathHelper.Pages.Home());
      }
    }

    protected void SetRedirect(string destinationUrl, string message = null) {
      WebHelper.Redirect(destinationUrl, message);
    }

    protected void RespondMessageAndEnd(string messageHtml) {

      if (SystemWeb.IsAjaxPost) {
        AjaxSubmitHelper.Process(ajax => {
          ajax.AddDialogMessage(messageHtml);
        });
      } else {
        WebHelper.WriteAndEnd(messageHtml);
      }
    }

  }
}
