
// General helpers for HTML Org reporting, either database or UI.

namespace Integral.Web {

  public partial class ReportHelper {

    public class Coachee {
      public const string tooltipData = "tooltipData";
      private const string tooltipText = "tooltipText";

      public static string GetTabMessageForAverages(bool hasPreSurvey, PathHelper.SurveyViewerTabEnum surveyViewerTab) {

        if (!hasPreSurvey) return "";

        string html = $"<div class=\"infoIcon\"><span data-{tooltipData}=\"{tooltipText}\"><ion-icon name=\"information-circle-outline\" style=\"margin-left: 5px;\"></ion-icon></span></div>";

        if (surveyViewerTab == PathHelper.SurveyViewerTabEnum.detailed) {
          return html.Replace(tooltipText, "All participants who have answered these questions in all surveys.");
        } else if (surveyViewerTab == PathHelper.SurveyViewerTabEnum.prepost) {
          return html.Replace(tooltipText, "Pre average is for all participants who have completed the pre-survey questions. "
                       + "<br/>Post average is for all participants who have completed the post-survey questions.");
        }
        return html;
      }
    }

  }
}
