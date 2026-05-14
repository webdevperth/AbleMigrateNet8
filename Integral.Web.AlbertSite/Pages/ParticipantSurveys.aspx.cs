using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class ParticipantSurveys : AppCode.PageBaseClasses.LoggedInPageBase {

    public List<DbHelper.AlbertSurveys.SurveyInfo> SurveyList = null;
    public List<DbHelper.SurveyShare.SharedSurveysInfo> SharedSurveysWithUser, SharedSurveysByUser;
    public PathHelper.ParticipantSurveysTabEnum SelectedSurveyTab;
    public class FormFields {
      public const string ShareSurveyId = "ShareSurveyId";
    }
    public class AjaxAction {
      public const string UnshareSurvey = "UnShareSurvey";
    }
    public class AjaxReturnData {
      public const string UnsharedResult = "UnsharedResult";
    }
    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Surveys";

      SurveyList = DbHelper.AlbertSurveys.GetSurveyInfoListForUser(userInfo.UserId, DbHelper.AlbertSurveys.OnlyViewerCompatible.No);

      SelectedSurveyTab = PathHelper.ParticipantSurveysTabEnum.UsersSurveys; // Default to profile tab.
      // Reads if URL contains a specific tab to redirect to, this variable is read in jQuery
      string pageTabQuery = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PageTab, "");
      if (!Enum.TryParse(pageTabQuery, true, out SelectedSurveyTab)) SelectedSurveyTab = PathHelper.ParticipantSurveysTabEnum.UsersSurveys;

      // Remove DevPlans and closed Evals.
      SurveyList.RemoveAll(
        s => s.SurveyTypeCode == ConfigHelper.SurveyTypeCodes.DevPlan
          || (s.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Eval && s.IsClosed));

      SharedSurveysByUser = new List<DbHelper.SurveyShare.SharedSurveysInfo>();
      SharedSurveysWithUser = new List<DbHelper.SurveyShare.SharedSurveysInfo>();
      var sharedSurveys = DbHelper.SurveyShare.GetSharedSurveysForUser(userInfo.UserId, DbHelper.SurveyShare.SharedSurveyTypeEnum.All);

      if (!sharedSurveys.IsNullOrEmpty()) {
        foreach (var ss in sharedSurveys) {
          if (ss.SurveyInfo.SurveyTypeCode == ConfigHelper.SurveyTypeCodes.DevPlan) continue; // Not include shared Dev Plans

          if (ss.UserIdSharedBy == userInfo.UserId) {
            SharedSurveysByUser.Add(ss);
          } else {
            SharedSurveysWithUser.Add(ss);
          }
        }
      }

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.UnshareSurvey:
              UnshareSurvey(ajax);
              break;

          }
        });
        return;
      }
    }

    public void UnshareSurvey(AjaxSubmitHelper ajax) {
      int? surveyShareId = WebHelper.GetFormValue(FormFields.ShareSurveyId).ToIntOrNull();

      if (surveyShareId == null) {
        ajax.AddDialogMessage("Invalid survey to unshare");
        return;
      }

      bool unshared = DbHelper.SurveyShare.UnshareSurvey(surveyShareId.Value);

      if (unshared) {
        ajax.AddSuccessToast("Survey unshared");
      } else {
        ajax.AddErrorToast("Couldn't unshare survey.");
      }

      ajax.AddReturnValue(AjaxReturnData.UnsharedResult, unshared);
    }

    public string GetSurveyInfoHtml(List<DbHelper.SurveyShare.SharedSurveysInfo> sharedSurveyInfo, PathHelper.ParticipantSurveysTabEnum surveyTab) {

      if (sharedSurveyInfo.IsNullOrEmpty()) {

        return WebHelper.GetNoRecordsBadge("No surveys found.");

      } else {

        string tableBody = "";

        foreach (var thisSurvey in sharedSurveyInfo) {
          string nameColumn = "";
          if (surveyTab == PathHelper.ParticipantSurveysTabEnum.SharedByUser) {
            nameColumn = $"<b>{thisSurvey.GetSharedWithFullName()}</b><br />{thisSurvey.EmailSharedWith}";
          } else {
            nameColumn = $"<b>{thisSurvey.GetSharedByFullName()}</b><br />{thisSurvey.EmailSharedBy}";
          }

          tableBody += $@"
            <tr tabindex=""0"" data-{WebHelper.DataAttrName.SurveyShareId}=""{thisSurvey.SurveyShareId}"" {(surveyTab == PathHelper.ParticipantSurveysTabEnum.SharedByUser ? "" : WebHelper.GetSharedSurveyDataAttrs(thisSurvey))}>
              <td>{nameColumn}</td>
              <td class=""type-description""><b>{thisSurvey.SurveyInfo.SurveyName.HTMLEncode()} </b>
                {(surveyTab == PathHelper.ParticipantSurveysTabEnum.SharedByUser ? $" - Self Close: {WebHelper.DisplayDate(thisSurvey.SurveyInfo.IntakeCloseDateSelf)}" : "")}
                <br />{thisSurvey.SurveyInfo.FriendlyProjectTitle.HTMLEncode()}
              </td>
              <td class=""type-delivery"">{WebHelper.GetSurveyDeliveryBadge(thisSurvey.SurveyInfo)}</td>
              {(surveyTab == PathHelper.ParticipantSurveysTabEnum.SharedByUser ? $"<td>{WebHelper.GetUnshareSurveyButtonHtml(thisSurvey, WebHelper.ShareSurveyButtonTypeEnum.ActionButton, WebHelper.CSSClasses.UnshareSurveyClass)}</td>" : "")}
            </tr>";
        }

        return $@"
          <div class=""table-responsive"">
            <table id =""tbl{surveyTab}"" class=""table table-bordered table-hover table-rowlink"" data-rowlink-url="""">
              <thead>
                <tr>
                  <th class=""type-description"">Shared {(surveyTab == PathHelper.ParticipantSurveysTabEnum.SharedByUser ? "with" : "by")}</th>
                  <th class=""type-description"">Survey</th>
                  <th class=""type-delivery"">Survey Type</th>
                  {(surveyTab == PathHelper.ParticipantSurveysTabEnum.SharedByUser ? "<th class=\"w100\"></th>" : "")}
                </tr>
              </thead>
              <tbody>
                {tableBody}
              </tbody>
            </table>
          </div>";
      }
    }

  }
}
