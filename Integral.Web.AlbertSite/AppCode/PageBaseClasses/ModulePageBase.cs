using System;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class ModulePageBase : LoggedInPageBase {

    public bool IsNewModule, IsProgramView, IsNewContent, IsParticipantView;

    public DbHelper.Modules.ModuleInfo ModuleInfo { get; set; }
    public DbHelper.Content.ContentInfo ContentInfo { get; set; }
    public DbHelper.Modules.ParticipantModuleInfo ParticipantModuleInfo = null;

    protected override void Page_Init(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      base.Page_Init(sender, e);

      FallbackUrl = PathHelper.Pages.Content();

      // Check if it's attempting to create new
      IsNewModule = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ModuleGuid) == PathHelper.AbleUrlValues.IdNew;
      IsNewContent = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ContentGuid) == PathHelper.AbleUrlValues.IdNew;
      IsParticipantView = SessionHelper.IsUserRoleLeader;

      // Seen from Program
      var programJobId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.ProgramJobId);
      IsProgramView = programJobId != null;

      if (IsProgramView) {
        ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(programJobId.Value, DbHelper.AblePrograms.WhereRelatedUserIs.Tenant_AnyRelated, SessionHelper.UserInfo);
        if (ProgramInfo == null) {
          SetFallbackRedirect(); // Given program id was not found.
          return;
        }
      }

      if (IsNewModule) {

        ModuleInfo = new DbHelper.Modules.ModuleInfo();

      } else if (IsNewContent) {

        if (IsParticipantView) {
          SetFallbackRedirect();
          return;
        }

        ContentInfo = new DbHelper.Content.ContentInfo();

      } else {

        if (HasModuleParam) {

          if (!WebHelper.TryGetQueryStringGuid(PathHelper.AbleUrlKeys.ModuleGuid, out Guid moduleGuid)) {
            SetFallbackRedirect();
            return;
          }

          if (IsParticipantView) {
            ParticipantModuleInfo = DbHelper.Modules.GetParticipantModuleInfo(null, moduleGuid, SessionHelper.UserInfo, SessionHelper.GetUserRole());
            if (ParticipantModuleInfo != null) {
              ModuleInfo = ParticipantModuleInfo.ModuleInfo;
            }
          }

          if (ParticipantModuleInfo == null) {
            ModuleInfo = DbHelper.Modules.GetModuleInfo(null, moduleGuid, SessionHelper.UserInfo, SessionHelper.GetUserRole());
          }

          if (ModuleInfo == null) {
            SetFallbackRedirect("Module not found.");
            return;
          }

        }

        if (HasContentParam) {

          string contentUrlValue = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ContentGuid);
          Guid contentGuid;

          if (!Guid.TryParse("" + contentUrlValue, out contentGuid)) {
            SetFallbackRedirect();
            return;
          }

          ContentInfo = DbHelper.Content.GetContentInfo(contentGuid, SessionHelper.UserInfo, SessionHelper.UserRole);

          if (ContentInfo == null) {
            SetFallbackRedirect("Microleaning not found.");
            return;
          }

          if (IsParticipantView && ModuleInfo != null) {
            if (!SessionHelper.AppAccess.Modules.CanNavigateToContentFromModule(ModuleInfo, ParticipantModuleInfo)) {
              SetRedirect(PathHelper.Pages.Module(ModuleInfo.ModuleGuid), "You must enrol in Module first");
              return;
            }
          }
        }

        if (IsParticipantView && ParticipantModuleInfo != null && ParticipantModuleInfo.IsUserEnrolled) {
          if (ParticipantModuleInfo.LastViewedUtc == null || ParticipantModuleInfo.LastViewedUtc.Value.Date != DateTime.UtcNow.Date) {
            DbHelper.Modules.UpdateModuleLastViewed(null, ModuleInfo.ModuleId, userInfo.UserId);
          }
        }

      }

      // Check user access to this page.
      if (!CheckUserPageAccess()) {
        SetFallbackRedirect();
        return;
      }

    }

    internal bool CheckUserPageAccess() {

      if (IsContentPage) {

        if (!SessionHelper.AppAccess.PageAccess.CanAccessContentPage()) return false;

        if (IsNewContent) {

          if (ProgramInfo != null) {
            return SessionHelper.AppAccess.Content.CanAddContentToProgram(ProgramInfo);
          } else {
            return SessionHelper.AppAccess.Content.CanAddContent();
          }

        } else {

          if (ContentInfo == null) return false;

          if (ModuleInfo != null) {
            if (ModuleInfo?.ContentInModule != null && ModuleInfo.ContentInModule.Exists(x => x == ContentInfo.ContentGuid)) return true;
          } else {

            if (ParticipantModuleInfo != null) {

              if (SessionHelper.AppAccess.Content.CanViewContentItem(ParticipantModuleInfo)) return true;

            } else {

              if (SessionHelper.AppAccess.Content.CanViewContentItem(ContentInfo)) return true;
            }

          }
        }

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.ModuleEdit_AddContent())) {

        if (SessionHelper.AppAccess.PageAccess.CanAccessModuleEdit()) return true;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.Module(null))) {

        if (ParticipantModuleInfo != null) {
          if (SessionHelper.AppAccess.Modules.CanViewModule(ParticipantModuleInfo)) return true;
        } else {
          if (SessionHelper.AppAccess.Modules.CanViewModule(ModuleInfo)) return true;
        }
      }

      return false; // Checks failed, deny.
    }

    internal bool IsContentPage => PathHelper.IsCurrentPage(PathHelper.Pages.Content()) || PathHelper.IsCurrentPage(PathHelper.Pages.ContentDetails(null));
    internal bool HasModuleParam => !WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ModuleGuid).IsNullOrEmpty();
    internal bool HasContentParam => !WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ContentGuid).IsNullOrEmpty();
    public bool IsContentFromModule => IsContentPage && HasModuleParam;
  }
}
