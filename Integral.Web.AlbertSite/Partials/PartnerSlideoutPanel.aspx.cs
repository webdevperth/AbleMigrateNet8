using Integral.Integrations;
using System;
using System.Text;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class PartnerSlideoutPanel : AppCode.PageBaseClasses.CoachInfoBase {
    private bool CanViewCoachProfile, CanViewCoachContactInfo, IsSelfSelectingCoach;

    public class AjaxAction {
      public const string SelectCoach = "SelectCoach";
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Partner Details";

      int coachId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.CoachId).GetValueOrDefault(0);
      CoachInfo = DbHelper.AlbertCoaches.GetCoachInfo(coachId, onlyPartners: true);
      if (CoachInfo == null) {
        SetFallbackRedirectNoAccess();
        return;
      }

      CanViewCoachContactInfo = SessionHelper.AppAccess.Coaches.CanViewCoachContactInfo(CoachInfo);
      CanViewCoachProfile = SessionHelper.AppAccess.Coaches.CanViewCoachProfile(CoachInfo);

      IsSelfSelectingCoach = SessionHelper.AppAccess.Participants.CanSelfSelectCoach(userInfo)
        && SessionHelper.AppAccess.Coaches.CanParticipantSelfSelect(CoachInfo);

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.SelectCoach:
              if (!IsSelfSelectingCoach) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              SelectCoachForParticipant(ajax);
              break;
          }
        });
        return;
      }
    }

    void SelectCoachForParticipant(AjaxSubmitHelper ajax) {

      bool updated = DbHelper.Common.UsingTransaction(trans => {

        try {

          bool updatedCoach = DbHelper.AlbertCoachees.UpdateCoachee_CoachUserId(trans, userInfo.LatestCoacheeInfo.CoacheeId, CoachInfo.UserId);
          bool updatedComponent = DbHelper.ProgramComponents.UpdateCoachingSessions_PartnerUserId(trans, userInfo.LatestCoacheeInfo.CoacheeId, CoachInfo.UserId);

          return updatedCoach && updatedComponent;

        } catch (Exception) {

          return false;
        }
      });

      if (updated) {
        ajax.SetRedirectUrl(PathHelper.Pages.ParticipantCoaching(userInfo.LatestCoacheeInfo.CoacheeGuid), "You can book your session now!");
        return;
      } else {
        ajax.AddErrorToast("Could not assign coach.");
      }
    }

    public string GetPartnerPanelHtml() {

      if (CoachInfo == null) return "";

      string html = "", statusHtml = "";

      if (CanViewInactivePartners) {
        statusHtml = "&nbsp " + (CoachInfo.IsPartnerActive ? WebHelper.HtmlEntitySymbol.PartnerActive : WebHelper.HtmlEntitySymbol.PartnerInactive);
      }
      if (CanViewHiddenPartners) {
        statusHtml += "&nbsp " + WebHelper.GetPartnerHiddenIcon(CoachInfo.IsProfileHidden, CanViewHiddenPartners);
      }

      string htmlDetailList = string.Empty;

      if (CanViewCoachContactInfo) {
        if (!CoachInfo.EmailAddress.IsNullOrEmptyOrWhitespace()) {
          htmlDetailList += $"<li><label>Email:</label><span>{CoachInfo.EmailAddress.HTMLEncode()}</span></li>";
        }
        if (!CoachInfo.MobileNumber.IsNullOrEmptyOrWhitespace()) {
          htmlDetailList += $"<li><label>Mobile:</label><span>{CoachInfo.MobileNumber.HTMLEncode()}</span></li>";
        }
      }
      if (!CoachInfo.BioShort.IsNullOrEmptyOrWhitespace()) {
        htmlDetailList += $"<li><label>Short Bio:</label><span>{CoachInfo.BioShort}</span></li>";
      }
      if (!CoachInfo.TagIdList.IsNullOrEmpty()) {
        htmlDetailList += $"<li><label>Tags:</label><span><div class=\"coachListTags\">{WebHelper.GetCoachTagsHtml(CoachInfo.TagIdList)}</div></span></li>";
      }
      if (!CoachInfo.WebProfileUrl.IsNullOrEmptyOrWhitespace()) {
        htmlDetailList += $"<li><label>Web Profile:</label><span><a href=\"{CoachInfo.WebProfileUrl.HTMLEncode()}\" target=\"_blank\">{CoachInfo.WebProfileUrl.HTMLEncode()}</a></span></li>";
      }

      html = $@"
        <div class=""flex1 overflow-y-auto mb20"" id=""slideout-partner-details"">
          <div class=""align-center mb20""><img class=""profile-image"" src=""{PathHelper.Images.UserPhoto(CoachInfo, PathHelper.Images.UserPhotoSize.Large, true)}"" alt=""Profile Image""></div>
          <ul class=""details-list"">
            <li><label>Name:</label><span class=""strong"">{CoachInfo.GetFullName().HTMLEncode()} {statusHtml}</span></li>
            <li><label>Company:</label><span>{CoachInfo.OrgName.HTMLEncode()}</span></li>
            {htmlDetailList}
          </ul>
        </div>";

      StringBuilder buttonAreaHtml = new StringBuilder();
      if (CanViewCoachProfile) {

        buttonAreaHtml.Append($@"<button id=""btnBack"" class=""btn btn-primary mr20 hidden"" type=""button"">Back</button>");
        if (!CoachInfo.CalendlyUrlName.IsNullOrEmptyOrWhitespace()) {
          buttonAreaHtml.Append($"<button id=\"btnBookCall\" class=\"btn btn-primary cal-book-link mr20\" type=\"button\">Book Meeting</button>");
        }
        buttonAreaHtml.Append($@"<a class=""btn btn-primary"" href=""{PathHelper.Pages.CoachEdit(CoachInfo.UserId)}"">Profile</a>");

      } else if (IsSelfSelectingCoach) {

        buttonAreaHtml.Append($@"<button id=""btnSelectCoach"" class=""btn btn-primary mr20 float-right"" type=""button"">Select Coach</button>");
      }

      if (buttonAreaHtml.Length > 0) {
        html += $@"<div class=""flex0"">";
        html += buttonAreaHtml.ToString();
        html += $"</div>";
      }

      if (!CoachInfo.CalendlyUrlName.IsNullOrEmptyOrWhitespace())
        html += $@"<iframe id=""slideout-booking-iframe"" class=""flex1"" src=""{Calendly.GetCalendlyPartnerBookingUrl(CoachInfo.CalendlyUrlName, userInfo.GetFullName(), userInfo.EmailAddress, false)}""></iframe>";

      return html;
    }

  }
}
