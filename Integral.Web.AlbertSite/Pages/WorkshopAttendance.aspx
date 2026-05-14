<%@ Page Language="C#" AutoEventWireup="true" CodeFile="WorkshopAttendance.aspx.cs" Inherits="Integral.Web.PortalSite.Pages_Albert.WorkshopAttendance"
   MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Pages_Albert" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <form id="formWorkshopAttendance" method="post" action="#" onsubmit="return false" class="form-horizontal">

    <table class="table">
      <thead>
        <tr>
          <th>Participant</th>
          <th>Attended</th>
          <th>Confirmed by</th>
        </tr>
      </thead>
      <tbody>
        <% foreach (var item in AttendanceList) { %>
          <tr>
            <td><%= item.Coachee.Fullname %></td>
            <td><%= WebHelper.CustomCheckBox(FormFields.WorkshopAttendanceIds, item.Coachee.Id.ToString(), item.IsConfirmed, "") %></td>
            <td>
              <p><%= item.ConfirmedByUser %></p>
              <p><%= WebHelper.DisplayDate(item.ConfirmedDateTimeUtc.UtcToTZOrNull(null), "") %></p>
            </td>
          </tr>
        <% } %>
      </tbody>
    </table>

  </form>

  <div class="form-group row">
    <label class="control-label col-md-2 col-sm-12 col-xs-12">&nbsp;</label>
    <div class="col-md-10 col-sm-12 col-xs-12">
      <button type="button" class="btn btn-secondary btnCancel mr30" data-mode="cancel">Cancel</button>
      <button type="button" class="btn btn-primary btnUpdate" id="btnUpdate">Save changes</button>
    </div>
  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function ($) {

      var btnUpdate;
      var formWorkshopAttendance;

      $(document).ready(function () {

        formWorkshopAttendance = $("#formWorkshopAttendance");
        btnUpdate = $("#btnUpdate");
        btnUpdate.click(UpdateWorkshop);

        $(".btnCancel").click(function (e) { history.go(-1); });

      }); // ready.

      function UpdateWorkshop() {

        var formData = new Object;
        formData.mode = "UpdateWorkshop";

        AjaxSubmit({
          form: formWorkshopAttendance,
          onSuccess: function () {
            location.reload();
          },
          onFail: function () {
            common_InfoDialog("Couldn't update the Workshop. Please try again later.");
          }
        });
      }
    })(jQuery);
  </script>

</asp:Content>
