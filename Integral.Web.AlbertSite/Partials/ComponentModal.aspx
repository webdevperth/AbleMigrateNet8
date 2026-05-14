<%@ Page Language="C#" AutoEventWireup="true" CodeFile="ComponentModal.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.ComponentModal" %>

<%@ Import Namespace="Integral.Web" %>

<% if (ComponentInfo != null) { %>

  <div class="components-modal">

    <form class="form-horizontal label-narrow" method="post" action="#" onsubmit="return false;">

      <%= GetComponentDetailsTable() %>

      <hr />

      <table class="table cmp-modal-table">
        <tr>
          <td><%= WebHelper.GetTextDisplayRow("Component Price:", 12, ComponentInfo.ComponentPrice.GetValueOrDefault(0).ToString("C")) %></td>
          <td><%= WebHelper.GetTextDisplayRow("Completed:", 12, WebHelper.DisplayDate(ComponentInfo.CompletedDateUtc, "-")) %></td>
        </tr>
        <tr>
          <td><%= WebHelper.GetTextDisplayRow("P&L:", 12, WebHelper.DisplayDate(ComponentInfo.PLPeriodDate, "-")) %></td>
          <td><%= GetPayRun() %></td>
        </tr>
      </table>

      <div class="cmp-modal-invoice">
        <%= WebHelper.GetSelectRow("Invoice Item:", FormFields.InvoiceItemId, 8, GetInvoiceItemOptions(), "", !CanUpdateComponent) %>
      </div>

      <br />
      <div class="modal-footer-buttons">
        <div><button type="button" class="btn btn-secondary btnClose">Cancel</button></div>
        <div>
          <button type="button" class="btn btn-primary btnUpdateSession <%= CanUpdateComponent ? "" : "disabled" %> " data-action="<%= AjaxAction.Update %>">Update</button>
        </div>
      </div>
      <br />
    </form>
  </div>

<% } %>

<% var scriptId = new Random((int)DateTime.Now.Ticks).Next(100000); %>

<script id="<%= scriptId %>">

  (function ($) {

    var dialogRef = null;
    var modalBody = null;
    var modalForm = null;

    $(document).ready(function () {
      // Find modal objects based on the random ID given to this script tag.
      var scripts = document.getElementsByTagName("script");
      for (var i = scripts.length - 1; i >= 0; i--) {
        if (scripts[i].id == <%= scriptId %>) {
          var thisScript = $(scripts[i]);
          var modalDialog = thisScript.closest(".modal-dialog");
          dialogRef = modalDialog.data("<%= WebHelper.DataAttrName.DialogRef %>");
          modalBody = modalDialog.find(".modal-body");
          modalForm = modalBody.find("form");
          break;
        }
      }

      $(".btnClose").click(function () { dialogRef.close(); });
      $(".btnUpdateSession").click(UpdateComponent);

      $(this).keyup(function (e) {
        if (e.keyCode == 27) {
          $('select[name="<%= FormFields.InvoiceItemId %>"]').select2("close");
        }
      });

    });

    function UpdateComponent(evt) {

      var thisBtn = $(evt.target);
      var ajaxAction = thisBtn.data("action");

      AjaxSubmit({
        form: modalForm,
        action: ajaxAction,
        url: "<%= PathHelper.Partials.ComponentModal(ProjectInfo.JobNumber, ComponentInfo.ComponentId) %>",
        onSuccess: function (jqXHR, data) { },
        onFail: function (jqXHR, data) { },
        onError: function (jqXHR, textStatus, errorThrown) { },
        onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
      });
    }


  })(jQuery);

</script>
