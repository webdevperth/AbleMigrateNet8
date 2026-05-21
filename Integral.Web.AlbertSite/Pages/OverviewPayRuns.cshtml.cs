using System;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class OverviewPayRuns : AppCode.PageBaseClasses.OverviewPageBase {

    public int UrlPayRunId;

    public class FormFields {
      public const string PayRunId = "PayRunId";
    }
    public class AjaxAction {
      public const string GetPayRun = "GetPayRun";
    }
    public class AjaxReturnData {
      public const string PayRunInfoHtml = "PayrunInfoHtml";
      public const string HasItems = "HasItems";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Pay Runs";

      if (!WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoachId).IsNullOrEmpty()) {
        WebHelper.Redirect(PathHelper.Pages.OverviewPayruns(UrlPayRunId));
        return new EmptyResult();
      }

      UrlPayRunId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.PayRunId) ?? 0;

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          if (PageAjaxAction == AjaxAction.GetPayRun) {

            var payRunIdSelected = ajax.CheckFieldIntOrNull(FormFields.PayRunId);

            string payRunInfoHtml = WebHelper.Payruns.GetPartnerPayRunInfo(OverviewCoachInfo, payRunIdSelected, out bool hasItems);

            ajax.AddReturnValue(AjaxReturnData.PayRunInfoHtml, payRunInfoHtml);
            ajax.AddReturnValue(AjaxReturnData.HasItems, hasItems);
          }
        });
        return new EmptyResult();
      }

      return Page();
    }

  }
}
