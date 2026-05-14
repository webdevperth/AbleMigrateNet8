<%@ Page Language="C#" AutoEventWireup="true" CodeFile="PartnerSlideoutPanel.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.PartnerSlideoutPanel" %>

<%@ Import Namespace="Integral.Web" %>

<%= GetPartnerPanelHtml() %>

<script type="text/javascript">
  (function ($) {

    var divBookingiFrame = $(".slideout-body #slideout-booking-iframe");
    var divDetailList = $("#slideout-partner-details");

    $(document).ready(function () {

      ShowSlideoutDetailList();

      $(".slideout-body #btnBookCall").click(function () {
        $("#btnBookCall").hide();
        $("#btnBack").show();
        ShowSlideoutBookingFrame();
      });

      $("#btnBack").click(function () {
        $("#btnBack").hide();
        $("#btnBookCall").show();
        ShowSlideoutDetailList();
      });

      $("#btnSelectCoach").click(CoachConfirmationModal);

    });

    function CoachConfirmationModal() {

      BootstrapDialog.show({
        type: BootstrapDialog.TYPE_WARNING,
        title: 'Confirmation',
        message: "Are you sure you want to select <%= CoachInfo.GetFullName().HTMLEncode() %> as your coach?",
        buttons: [
          {
            label: 'No',
            cssClass: 'btn-secondary',
            action: function (dialog) {
              dialog.close();
            }
          },
          {
            label: 'Yes',
            cssClass: 'btn-primary',
            action: function (dialog) {
              dialog.close();
              SelectCoachForParticipant();
            }
          }
        ]
      });
    }

    function SelectCoachForParticipant() {
      var btnSelectCoach = $('#btnSelectCoach');

      AjaxSubmit({
        url: "<%= PathHelper.CurrentUrl %>",
        action: "<%= AjaxAction.SelectCoach %>",
        onSuccess: function (jqXHR, data) { },
        onFail: function (jqXHR, data) {
        },
        onError: function (jqXHR, textStatus, errorThrown) {
          common_InfoDialog("Selection failed, please try again later.");
        },
        onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
      });
    }

    function ShowSlideoutDetailList() {
      divBookingiFrame.hide();
      divDetailList.show();
    }

    function ShowSlideoutBookingFrame() {
      divBookingiFrame.show();
      divDetailList.hide();
    }

  })(jQuery);

</script>
