using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class Module : AppCode.PageBaseClasses.ModulePageBase {

    public List<DbHelper.Modules.UserModuleContentInfo> UserModuleContentInfo = null;
    public DbHelper.Content.UserContentInfo UserContentInfo = null;
    public bool CanEnrolInModule;

    public class AjaxAction {
      public const string Enrol = "Enrol";
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Module";

      UserModuleContentInfo = DbHelper.Modules.GetUserModuleContentInfo(null, ModuleInfo.ModuleId, userInfo.UserId, SessionHelper.GetUserRole());
      CanEnrolInModule = SessionHelper.AppAccess.Modules.CanEnrolInModule(ModuleInfo, ParticipantModuleInfo);

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.Enrol:
              if (!CanEnrolInModule) {
                ajax.AddDialogMessage("Not allowed to.");
                return;
              }
              EnrolInModule(ajax);
              break;

          }
        });
        return;
      }
    }

    private void EnrolInModule(AjaxSubmitHelper ajax) {
      bool enrolled = DbHelper.Modules.EnrolUserInModule(ModuleInfo.ModuleId, userInfo.UserId);
      if (enrolled) {
        ajax.SetReloadPage("Successfully enrolled in Module", AjaxSubmitHelper.PageMessageType.SuccessToast);
      } else {
        ajax.AddErrorToast("Couldn't be enrolled in Module");
      }
    }
  }
}
