<%@ Page Language="C#" AutoEventWireup="true" CodeFile="SurveyRaters.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.SurveyRaters" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (ErrorVisible) { %>
    <h3>Survey Not Found</h3>
    <p>Unfortunately the link you followed is not valid.</p>
    <p>Please consult your coach if this was unexpected.</p>
  <% } %>

  <% if (ContentVisible) { %>

    <h3><%= SelfParticipantInfo.FullName %></h3>

    <div class="raters-container">

      <% if (SurveyInfo.IsStrictRaterLimits && SurveyInfo.RatersSuggestedMax != null && PartList.Count >= SurveyInfo.RatersSuggestedMax) { %>

        <h4>All Raters Invited, thank you!</h4>

      <% } else { %>

        <div class="row">
          <div class="col-md-12">
            <% if (SurveyInfo.RatersSuggestedMax == 1) { %>
              <h3>Invite Your Rater</h3>
              <h4>Please invite your rater, who will complete their version of the questionnaire.</h4>
            <% } else { %>
              <h3>Invite Your Raters</h3>
              <h4>
                <% if (SurveyInfo.RatersSuggestedMin != null && SurveyInfo.RatersSuggestedMax == null && SurveyInfo.RatersSuggestedMin > 0) { %>
                  Please invite <u>at least <%= SurveyInfo.RatersSuggestedMin %></u> rater<%= SurveyInfo.RatersSuggestedMin > 1 ? "s" : "" %>,
                <% } else if (SurveyInfo.RatersSuggestedMin == null && SurveyInfo.RatersSuggestedMax != null && SurveyInfo.RatersSuggestedMax > 1) { %>
                  Please invite <u>at most <%= SurveyInfo.RatersSuggestedMax %></u> rater<%= SurveyInfo.RatersSuggestedMax > 1 ? "s" : "" %>,
                <% } else if (SurveyInfo.RatersSuggestedMin != null && SurveyInfo.RatersSuggestedMax != null && SurveyInfo.RatersSuggestedMin < SurveyInfo.RatersSuggestedMax) { %>
                  Please invite <u><%= SurveyInfo.RatersSuggestedMin %> to <%= SurveyInfo.RatersSuggestedMax %></u> raters,
                <% } else { %>
                  Please invite <u>at least <%= SurveyInfo.MinimumRatersRequiredForReport %></u> rater<%= SurveyInfo.MinimumRatersRequiredForReport > 1 ? "s" : "" %>,
                <% } %>
                who will complete their version of the questionnaire.
              </h4>
            <% } %>
            <h4 class="mt30">Raters invited so far: <%= SelfParticipantInfo.RatersNotDeclined %></h4>
          </div>
        </div>

        <div class="row mb20">
          <button type="button" class="btn btn-primary float-right mr20 btnInviteRaters" data-<%=DataAttr.AddingType %>="<%= AjaxAction.InviteNewRater %>">Invite New Rater</button>

          <% if (HasPastRaters) { %>
            <button type="button" class="btn btn-primary float-right mr10 btnInviteRaters" data-<%=DataAttr.AddingType %>="<%= AjaxAction.InvitePastRaters %>">Invite Past Raters</button>
          <% } %>
        </div>

        <div id="dlgInviteRater" class="displaynone">
          <form id="frmInviteNew" method="post" action="#" onsubmit="return false;">
            <div class="row content">
              <table class="table noborder width-auto table-halfgap">
                <colgroup>
                  <col width="130" />
                  <col width="300" />
                </colgroup>
                <tr class="ajaxSubmit-field">
                  <td><label>First Name:</label></td>
                  <td><input type="text" class="form-control" name="<%= FormFields.FirstName %>" value="" /></td>
                </tr>
                <tr class="ajaxSubmit-field">
                  <td><label>Last Name:</label></td>
                  <td><input type="text" class="form-control" name="<%= FormFields.LastName %>" value="" /></td>
                </tr>
                <tr class="ajaxSubmit-field">
                  <td><label>Email:</label></td>
                  <td><input type="email" class="form-control" name="<%= FormFields.Email %>" value="" /></td>
                </tr>
              </table>
            </div>
          </form>
        </div>

        <div id="dlgPastRaters" class="displaynone">
          <form id="frmInvitePastRaters" method="post" action="#" onsubmit="return false;">
            <div class="row content">
              <p class="mb10" >If you don't want to invite any of them, you can unselect them.</p>
              <div class="table-responsive w100p" style="height: 500px !important;">
                <table class="tblPastRaters table table-bordered table-hover">
                  <thead>
                    <tr>
                      <th class="">Rater</th>
                    </tr>
                  </thead>
                  <tbody>
                    <% if (PastRatersForParticipant != null) { %>
                      <% foreach (var rater in PastRatersForParticipant) { %>
                        <tr>
                          <td>
                            <%= WebHelper.CustomCheckBox(FormFields.PastRaterId, rater.Email, true, $"{rater.Name} ({rater.Email})") %>
                          </td>
                        </tr>
                      <% } %>
                    <% } %>
                  </tbody>
                </table>
              </div>
            </div>
          </form>
        </div>

      <% } %>

      <div class="table-responsive">
        <table class="table tblRaters" id="tblRaters">
          <colgroup>
            <col width="300" />
            <col width="300" />
            <col width="" />
          </colgroup>
          <thead>
            <tr>
              <th>Name</th>
              <th>Email Address</th>
              <th>&nbsp;</th>
            </tr>
          </thead>
          <tbody>
            <% foreach (var partListItem in PartList) { %>
              <tr class="rowPartItem id_<%= partListItem.PartUID %>">
                <td><%= partListItem.FullName.HTMLEncode() %></td>
                <td><%= partListItem.Email.HTMLEncode() %></td>
                <td><%= invitedIconHtml %>&nbsp;&nbsp;Sent.</td>
              </tr>
            <% } %>
          </tbody>
        </table>
      </div>

      <div class="msgNoParts display-none"><p>No Raters Invited Yet</p></div>

    </div>

    <%= GetUserActionsHtml() %>

  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var $frmInviteNew, $frmInvitePastRaters;
      var noParts = ($(".rowPartItem").length == 0 ? true : false);

      $(document).ready(function () {

        $frmInviteNew = $("#frmInviteNew");
        $frmInvitePastRaters = $("#frmInvitePastRaters");

        if (noParts) {
          $(".msgNoParts").show();
          if ($frmInviteNew.length == 1) $frmInviteNew.find("input:text:first").focus();
        }

        $(".btnInviteRaters").click(GetInviteDialog);

        $(".action-cards-container").hide(); // Hide on page load by default

        $(".btn-next").click(function () {
          $(".btn-next").fadeOut(300);
          $(".raters-container").fadeOut(300, function () {
            $(".action-cards-container").fadeIn(300);
            $(".btn-prev").fadeIn(300).removeClass("display-none");
          });
        });

        $(".btn-prev").click(function () {
          $(".btn-prev").fadeOut(300, function () {
            $(".action-cards-container").fadeOut(300, function () {
              $(".raters-container").fadeIn(300);
              $(".btn-next").fadeIn(300);
            });
          });
        });

      }); // ready.

      function GetInviteDialog(evt) {
        // Get the button that was clicked
        var button = $(evt.target);
        // Get data attributes from the button
        var btnDataAddType = button.data('<%= DataAttr.AddingType%>');
        var isInvitingNew = btnDataAddType == "<%= AjaxAction.InviteNewRater %>" ? true : false;
        var title = isInvitingNew ? "Invite new Rater" : "Invite past Raters";

        var dlg = common_InfoDialog(isInvitingNew ? '#dlgInviteRater' : '#dlgPastRaters', {
          name: "InviteRaters",
          title: title,
          width: 500,
          buttons: [
            { text: "Cancel", class: "btn-secondary mr20", isDefault: false, isPrimary: false, close: true },
            {
              text: "Invite", id: "btnInviteRaters", isDefault: true, isPrimary: true, close: false, click: function (ev) {
                if (isInvitingNew) {
                  InviteNewRater(dlg);
                } else {
                  InvitePastRaters(dlg);
                }
              }
            }
          ],
          shown: function () { },
          hide: function () { }
        });
      }

      function InviteNewRater(dlg) {
        AjaxSubmit({
          form: $frmInviteNew,
          action: "<%= AjaxAction.InviteNewRater %>",
          onSuccess: function (jqXHR, data) { }
        });
      }

      function InvitePastRaters(dlg) {

        var pastRatersChecked = [];
        var hasPastRaters = <%= HasPastRaters.ToJSTrueFalse() %>;

        if (hasPastRaters) {
          $('.tblPastRaters input[type="checkbox"][name="<%= FormFields.PastRaterId %>"]:checked').each(function () {
            pastRatersChecked.push($(this).val());
          });
        }

        var pastRaters = pastRatersChecked.join(',');

        AjaxSubmit({
          form: $frmInvitePastRaters,
          action: "<%= AjaxAction.InvitePastRaters %>",
          data: {
            "<%= FormFields.PastRaterId %>": pastRaters
          },
          onSuccess: function (jqXHR, data) {

          }
        });
      }

    })(jQuery);
  </script>

</asp:Content>

