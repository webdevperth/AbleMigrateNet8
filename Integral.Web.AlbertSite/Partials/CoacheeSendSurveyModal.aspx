<%@ Page Language="C#" AutoEventWireup="true" CodeFile="CoacheeSendSurveyModal.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.CoacheeSendSurveyModal" %>

<%@ Import Namespace="Integral.Web" %>

<% if (CoacheeInfo.ProgramJobId == null) { %>
  <div class="alert alert-warning">Participant must belong to a program. Please select a program from the
    <a href="<%= PathHelper.Pages.CoacheeEdit(true) %>">Profile</a> page.</div>
<% } %>

<form class="form-horizontal mb30 mt20" id="clientForm">

  <input type="hidden" name="clientid" value="<%= CoacheeInfo.CoacheeId %>" />

  <div class="form-group ajaxSubmit-field row surveyList">
    <label class="control-label col-md-3 col-sm-12 col-xs-12">Choose Survey To Send:</label>
    <div class="col-md-9 col-sm-12 col-xs-12">
      <select class="form-control select2-word-wrap control-width-select-maximum" data-placeholder="survey template" id="selSurvey" name="<%= FormFields.SurveyId %>" size="1">
        <%= GetSurveySelectOptions() %>
      </select>
    </div>
  </div>

  <div class="closeDates">

    <%= WebHelper.GetInputDateRow("When to Send Survey:", FormFields.SendDate,
      TimeHelper.UtcToAppDefaultTimeZone(DateTime.UtcNow).Value, 3, 9, "Select a future date<br/>to schedule delivery.") %>

    <div class="coacheeCloseDateArea">
      <%= WebHelper.GetInputDateRow("Participant Close Date:", FormFields.SelfClose, GetSelfCloseDate(), 3, 9, "") %>
    </div>

    <div class="ratersCloseDateArea">
      <%= WebHelper.GetInputDateRow("Rater (Final) Close Date:", FormFields.RaterClose, GetRaterCloseDate(), 3, 9, "") %>
    </div>

  </div>

      <%= WebHelper.GetTextDisplayRow("", @"
        <div class=""ratersOnlyMessage"">Note: This is a ""Raters-Only"" survey.<br/>The survey will be sent to all raters in<br/>the participant's most recent 360 survey.</div>
        <div class=""noRatersMessage"">Note: This survey requires no raters.<br />The participant will not be asked to select raters.</div>
        <div class=""mt20""><button type=""button"" id=""btnSend"" class=""btn btn-primary"">Send Survey Now</button></div>") %>

</form>

<script type="text/javascript">
  (function ($) {

    var clientForm = $("#clientForm");
    var btnSend, selSurvey, inpSelfClose, inpRaterClose, inpSendDate;
    var coacheeCloseDateArea, ratersCloseDateArea

    $(document).ready(function () {

      btnSend = $("#btnSend");
      selSurvey = $("#selSurvey");
      coacheeCloseDateArea = $(".coacheeCloseDateArea");
      ratersCloseDateArea = $(".ratersCloseDateArea");
      inpSelfClose = $('input[name="<%= FormFields.SelfClose %>"]');
      inpRaterClose = $('input[name="<%= FormFields.RaterClose %>"]');
      inpSendDate = $('input[name="<%= FormFields.SendDate %>"]');

      btnSend.click(SendSurvey);

      selSurvey.change(SurveyChanged);
      SurveyChanged();

      inpSelfClose.change(function (e) {
        var addDays = <%= ConfigHelper.AbleSurveyDefaultOpenDays_Rater - ConfigHelper.AbleSurveyDefaultOpenDays_Self %>;
        var date = moment(new Date($(this).val())); //.format("MMM Do YY");
        date = date.add(addDays, 'days')
        inpRaterClose.val(date.format("D MMM YYYY"));
      });

      inpSendDate.change(SendWhenChanged);
      SendWhenChanged();

    }); // ready.

    function SendWhenChanged() {
      var sendDate = new Date(inpSendDate.val());
      if (sendDate > new Date())
        btnSend.text("Schedule to Send on " + inpSendDate.val());
      else
        btnSend.text("Send Survey Now");
    }

    // Adjust options for selected survey.
    function SurveyChanged() {

      var opt = selSurvey.find(":selected");
      if (opt.length != 1) return;
      var ratersOnly = (opt.data("ratersonly") == "1");
      var noRaters = (opt.data("noraters") == "1");
      var selfOpenDays = parseInt(opt.data("selfopendays"));

      // Don't uncomment - this is causing an issue yet to be fixed.
      // When the date picker is updated, the displayed date is incorrect and shown in US format.
      // It should correctly adjust the date and show it in the proper datepicker format.
      // common_UpdateDatePicker(inpSelfClose, moment().add(selfOpenDays, 'days'));

      if (ratersOnly) {
        coacheeCloseDateArea.slideUp(200);
        $(".noRatersMessage").slideUp(200);
        ratersCloseDateArea.slideDown(200);
        $(".ratersOnlyMessage").slideDown(200);
      } else if (noRaters) {
        ratersCloseDateArea.slideUp(200);
        $(".ratersOnlyMessage").slideUp(200);
        coacheeCloseDateArea.slideDown(200);
        $(".noRatersMessage").slideDown(200);
      } else {
        $(".ratersOnlyMessage").slideUp(200);
        $(".noRatersMessage").slideUp(200);
        coacheeCloseDateArea.slideDown(200);
        ratersCloseDateArea.slideDown(200);
      }
    }

    function SendSurvey() {

      common_ConfirmDialog("Confirm", btnSend.text() + "?", function (confirmed) {
        if (confirmed) SendSurveyConfirmed();
      });
    }

    function SendSurveyConfirmed() {

      AjaxSubmit({
        form: clientForm,
        url: "<%= PathHelper.Partials.CoacheeSendSurveyModal(CoacheeInfo.CoacheeId) %>",
        action: "<%= AjaxAction.SendSurvey %>",
        onSuccess: function (jqXHR, data) { },
        onFail: function (jqXHR, data) { },
        onError: function (jqXHR, textStatus, errorThrown) { },
        onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
      });
    }

  })(jQuery);
</script>
