using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class Module : AppCode.PageBaseClasses.ModulePageBase {

    public List<DbHelper.Modules.UserModuleContentInfo> UserModuleContentInfo = null;
    public DbHelper.Content.UserContentInfo UserContentInfo = null;
    public bool CanEnrolInModule;

    public class AjaxAction {
      public const string Enrol = "Enrol";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

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
        return new EmptyResult();
      }

      return Page();
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
