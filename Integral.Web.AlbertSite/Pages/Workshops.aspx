<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="Workshops.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.Workshops"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (WorkshopListVisible) { %>

    <% if (WorkshopEventList.IsNullOrEmpty()) { %>

      <%= WebHelper.GetEmptyStatePageHtml(
        title: "Workshops",
        description: $"No workshops yet.{(CanAddWorkshop ? " Add the first one!" : "")}",
        addActionHtml: CanAddWorkshop,
        actionButtonText: "Add Workshop",
        actionButtonPath: PathHelper.Pages.Workshops_Add(ProgramInfo.ProgramJobId)) %>

    <% } else { %>

      <script>
        (function ($) {
          $(document).ready(function () {

            $("#<%= WebHelper.ElementID.SlideoutPanelTitle %>").text("Workshop Booking");

            $(".cal-book-link").click(function (evt) {
              evt.preventDefault();

              $("body").removeClass("slideout-show");

              // Copy details from row daya into slideout content.
              var row = $(evt.target).closest("tr.rowData");
              $('.details-list span[data-name]').each(function (i, e) {
                var $e = $(e);
                $e.html("");
                if (row.length === 1) $e.html(row.data($e.data("name")));
              });
              // Set the iframe calendly url.
              $(".booking-iframe").prop("src", row.data("calendly-url"));

              // Show slideout with a bit of a delay.
              window.setTimeout(function () {
                $("body").addClass("slideout-show");
              }, 500);
            });
          });
        })(jQuery);
      </script>

      <div class="flex flex-column h100p" data-appendTo="<%= WebHelper.ElementID.SlideoutPanelBody %>">
        <ul class="details-list flex0">
          <li><label>Name</label><span data-name="title" class="strong"></span></li>
          <li><label>Date</label><span data-name="date-text"></span></li>
          <li><label>Project</label><span data-name="project-name"></span></li>
          <li class="instructions"><label>Instructions</label><span data-name="instructions-html"></span></li>
        </ul>
        <iframe class="booking-iframe flex1" src="about:blank"></iframe>
      </div>

      <% if (CanAddWorkshop) { %>
        <div class="content-action-bar">
          <div class="right">
            <a href="<%= PathHelper.Pages.Workshops_Add(ProgramInfo.ProgramJobId) %>" class="btn btn-primary">Add Workshop</a>
          </div>
        </div>
      <% } %>

      <div class="table-responsive">
        <table class="table table-bordered" <%= CanNavigateFromWorkshopTable.ToValue($"data-rowlink-url=\"{PathHelper.Pages.Workshops_Edit(ProgramInfo.ProgramJobId, null)}\"", "") %>">
          <thead>
            <tr>
              <th class="type-datetime-2lines">Time</th>
              <th class="type-description">Title</th>
              <th class="type-user-nameWithAvatar">Facilitator</th>
              <th class="type-delivery">Type</th>
              <th class="type-status">Status</th>
              <th class="type-evalscore">Eval Score</th>
              <% if (CanViewTotalRevenue) { %>
                <th class="type-money-sm"><%= RevenueTextDisplay %></th>
              <% } %>
              <% if (CanViewPartnerRevenue) { %>
                <th class="type-money-sm">Partner</th>
              <% } %>
              <% if (CanSeeBookingColumn) { %>
                <th class="w100"></th>
              <% } %>
            </tr>
          </thead>
          <tbody>
            <% if (WorkshopEventList != null) { %>
              <% foreach (var workshop in WorkshopEventList) { %>
                <tr tabindex="0" class="rowData"
                  data-rowlink-id="<%= workshop.WorkshopEventId %>"
                  data-title="<%= workshop.WorkshopTitle.HTMLEncode() %>"
                  data-date-text="<%= GetStartDateForSlideout(workshop).HTMLEncode() %>"
                  data-project-name="<%= (ProjectInfo.ProjectName + " > " + workshop.ProgramJobName).HTMLEncode() %>"
                  data-instructions-html="<%= workshop.WorkshopNotes.SafeHTML().HTMLEncode().ValueIfNullOrEmpty("n/a") %>"
                  data-calendly-url="<%= GetCalendlyGroupBookingUrl(workshop, true) %>">

                  <td class="type-datetime-2lines"><%= GetWorkshopStartDateTimeHtml(workshop) %></td>
                  <td class="type-description"><%= workshop.WorkshopTitle.HTMLEncode() %></td>
                  <td class="type-user-nameWithAvatar">
                      <%= WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(workshop, PathHelper.Images.UserPhotoSize.Thumbnail, true),
                              workshop.KeyFacilitatorFirstName + " " + workshop.KeyFacilitatorLastName.HTMLEncode(), workshop.KeyFacilitatorUserId) %>
                  </td>
                  <td class="type-delivery"><%= WebHelper.GetDeliveryBadge(!workshop.IsVirtual) %> </td>
                  <td class="type-status"><%= WebHelper.GetStatusBadge(workshop.WorkshopStatusName.HTMLEncode()) %></td>
                  <td class="type-evalscore"><%= GetEvalScore(workshop) %></td>
                  <% if (CanViewTotalRevenue) { %>
                    <td class="type-money-sm"><%= workshop.WorkshopRevenue.GetValueOrDefault(0).ToString("C") %></td>
                  <% } %>
                  <% if (CanViewPartnerRevenue) { %>
                    <td class="type-money-sm"><%= WebHelper.GetPartnerRevenueValue(workshop.WorkshopRevenue, ProgramInfo.Partner_DeliveryPercentage, workshop.KeyFacilitatorUserId == userInfo.UserId, CanViewAllDeliveryTeamRevenue) %></td>
                  <% } %>
                  <% if (CanSeeBookingColumn) { %>
                    <td class=""><%= GetListBookingLink(workshop) %></td>
                  <% } %>
                </tr>
              <% } %>
            <% } %>
          </tbody>
        </table>
      </div>

    <% } %>

  <% } %>

  <% if (WorkshopFormVisible) { %>

    <div class="container-fluid">

      <ul class="nav nav-tabs nav-tabs-underlined" id="formTabs">
        <li role="presentation" class="active" data-tabname="<%= TabName.Details %>">
          <a class="nav-link" id="tab-<%= TabName.Details %>" data-toggle="tab" href="#panel-<%= TabName.Details %>" role="tab" aria-controls="panel-<%= TabName.Details %>">Workshop Details</a>
        </li>
        <% if (!IsNewWorkshop) { %>
          <li role="presentation" data-tabname="<%= TabName.Attendance %>">
            <a class="nav-link" id="tab-<%= TabName.Attendance %>" data-toggle="tab" href="#panel-<%= TabName.Attendance %>" role="tab" aria-controls="panel-<%= TabName.Attendance %>" aria-selected="true">Attendance</a>
          </li>
        <% } %>
      </ul>

      <div class="tab-content">
        <div class="tab-pane tab-quote tab-<%= TabName.Details %> fade in active" id="panel-<%= TabName.Details %>" role="tabpanel" aria-labelledby="tab-<%= TabName.Details %>"></div>
        <div class="tab-pane tab-quote tab-<%= TabName.Attendance %> fade in" id="panel-<%= TabName.Attendance %>" role="tabpanel" aria-labelledby="tab-<%= TabName.Attendance %>"></div>
      </div>

      <div class="tab-panel" data-appendTo="panel-<%= TabName.Details %>">

        <div class="row mb10">
          <div class="col-md-6"><h4>Workshop Details</h4></div>
          <% if (CanCopyWorkshop) { %>
            <div class="btnholder-header floatright">
              <button type="button" id="btnCopy" class="btn btn-info" title="Copy Workshop"><%= WebHelper.Icon.Copy %></button>
            </div>
          <% } %>
        </div>

        <form id="formWorkshop" method="post" action="#" onsubmit="return false" class="form-horizontal">

          <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="UpdateWorkshop" />

          <% if (WorkshopEventInfo.HasEvalScore) { %>
            <%= WebHelper.GetTextDisplayRow("Workshop Eval Results:", 10, GetEvalLink(WorkshopEventInfo)) %>
          <% } %>

          <% if (!IsNewWorkshop) { %>

            <%= WebHelper.GetTextDisplayRow("Workshop Booking Link:", 10, GetViewBookingLink()) %>

            <%= WebHelper.GetTextInput("Workshop ID:", FormFields.FriendlyWorkshopId, WorkshopEventInfo.FriendlyWorkshopId, 2,
                IsNewWorkshop ? "(Update to see assigned ID)" : "", true) %>

          <% } %>

          <%= WebHelper.GetTextInput("Workshop Title:", FormFields.WorkshopTitle, WorkshopEventInfo.WorkshopTitle, 5, "", IsLimitedEdit || IsReadOnly, false, WebHelper.InputMaxLength.WorkshopTitle) %>

          <%= WebHelper.GetSelectRow("Session Type:", FormFields.SessionTypeId, 5, GetSessionTypeOptions(), "", IsLimitedEdit || IsReadOnly) %>

          <div class="form-group ajaxSubmit-field row">
            <label class="control-label col-md-2">Workshop Type:</label>
            <div class="col-md-6 col-sm-12 col-xs-12" id="SessionTypeBadge"></div>
          </div>

          <%= WebHelper.GetTextInput("Location:", FormFields.Location, WorkshopEventInfo.Location, 5, "", IsLimitedEdit || IsReadOnly) %>

          <%= GetStatusOptions("Workshop Status:") %>

          <% if (CanViewTotalRevenue) { %>
            <%= WebHelper.GetCurrencyInput("Workshop Revenue:", FormFields.WorkshopRevenue, WorkshopEventInfo.WorkshopRevenue.GetValueOrDefault(0), 2, 4, "", IsLimitedEdit || IsReadOnly, true) %>
          <% } %>

          <% if (PartnerRevenue != 0 && CanViewPartnerRevenue) { %>
            <%= WebHelper.GetCurrencyInput("Partner Revenue:", FormFields.PartnerRevenue, PartnerRevenue, 2, 4, "", true) %>
          <% } %>

          <% if (PLCRevenue != 0) { %>
            <%= WebHelper.GetCurrencyInput("PLC Revenue:", FormFields.PLCRevenue, PLCRevenue, 2, 4, "", true) %>
          <% } %>

          <% if (SalesRevenue != 0) { %>
            <%= WebHelper.GetCurrencyInput("Sales Revenue:", FormFields.SalesRevenue, SalesRevenue, 2, 4, "", true) %>
          <% } %>

          <%= WebHelper.GetSelectRow("Time Zone:", FormFields.TimeZoneIdIana, 4, GetTimeZoneOptions(), "", IsReadOnly) %>

          <%= WebHelper.GetInputDateRow("Date:", FormFields.StartDate, WorkshopEventInfo.WhenStartLocal, "", IsReadOnly) %>
          <%= WebHelper.GetTimePickerRow("Start Time:", FormFields.StartTime, WorkshopEventInfo.WhenStartLocal, "", IsReadOnly) %>
          <%= WebHelper.GetTimePickerRow("End Time:", FormFields.EndTime, WorkshopEventInfo.WhenEndLocal, "", IsReadOnly) %>

          <% if (IsLimitedEdit || IsReadOnly) { %>
            <%= WebHelper.GetTextDisplayRow("Key Facilitator:", (WorkshopEventInfo.KeyFacilitatorFirstName + " " + WorkshopEventInfo.KeyFacilitatorLastName).HTMLEncode()) %>
            <%= WebHelper.GetTextDisplayRow("Co-Facilitator:", (WorkshopEventInfo.CoFacilitatorFirstName + " " + WorkshopEventInfo.CoFacilitatorLastName).HTMLEncode()) %>
          <% } else { %>
            <%= GetPartnerDropdownHtml("Key Facilitator:", FormFields.KeyFacilitatorUserId, 4, WorkshopEventInfo.KeyFacilitatorUserId) %>
            <%= GetPartnerDropdownHtml("Co-Facilitator:", FormFields.CoFacilitatorUserId, 4, WorkshopEventInfo.CoFacilitatorUserId) %>
          <% } %>

          <%= WebHelper.CustomCheckBoxRow("Disable Evals:", FormFields.DisableEvals, WebHelper.DefaultCheckboxValue, WorkshopEventInfo.DisableEvals, IsReadOnly, "") %>

          <%= WebHelper.CustomCheckBoxRow("Add PAX To Invite:", FormFields.AddParticipantsToInvite, WebHelper.DefaultCheckboxValue, WorkshopEventInfo.AddParticipantsToInvite, IsReadOnly, "") %>

          <%= WebHelper.CustomCheckBoxRow("Hide From Program Content:", FormFields.HideFromProgramContent, WebHelper.DefaultCheckboxValue, WorkshopEventInfo.HideFromProgramContent, IsReadOnly, "") %>

          <%= WebHelper.GetRichTextArea("Additional PAX Info:", FormFields.ParticipantAdditionalInfo, 2, 6, WorkshopEventInfo.ParticipantAdditionalInfo, "", IsReadOnly) %>

          <%= WebHelper.GetTextArea("Workshop Notes:", FormFields.WorkshopNotes, 2, 6, WorkshopEventInfo.WorkshopNotes, "", IsReadOnly) %>

          <% if (CanSetQuoteItem) { %>
            <%= WebHelper.GetQuoteItemSelectRow("Quote Item:", 6,
                    FormFields.QuoteItemId,
                    IsNewWorkshop, WorkshopEventInfo.ComponentQuoteInfo,
                    GetQuoteItemOptions(),
                    IsLimitedEdit || IsReadOnly) %>
          <% } %>

          <% if (!IsReadOnly && CanMoveToProgram) { %>
            <%= WebHelper.GetSelectRow("Move to Program:", FormFields.MoveToProgramJobId, 6, GetMoveToProgramOptions(), "", IsLimitedEdit || IsReadOnly) %>
          <% } %>

          <div class="btnholder">
            <% if (!IsReadOnly) { %>
              <button type="button" class="btn btn-primary btnUpdate floatright" id="btnUpdate"><%= IsNewWorkshop ? "Add New" : "Update" %> Workshop</button>
              <% if (CanDeleteWorkshop) { %>
                <button type="button" class="btn btn-warning btnDelete floatleft" id="btnDelete">Delete Workshop</button>
              <% } %>
            <% } %>
            <button type="button" class="btn btn-secondary btnCancel <%= IsReadOnly ? "floatleft" : "floatright mr20" %>" data-mode="cancel"><%= IsReadOnly ? "Back" : "Cancel" %></button>
          </div>

        </form>

        <div id="dlgCopyWorkshop" class="displaynone">
          <form id="frmCopyWorkshop" method="post" action="#" onsubmit="return false;">
            <div class="row pl20 form-horizontal">
              <%= WebHelper.GetTextInput("New Workshop title:", FormFields.CopyWorkshopTitle, "", 8) %>
              <%= WebHelper.GetSelectRow("Quote Item:", FormFields.CopyWorkshopQuoteItemId, 8, GetQuoteItemOptions(), "") %>
            </div>
          </form>
        </div>

      </div>

      <% if (!IsNewWorkshop) { %>
        <div class="tab-panel" data-appendTo="panel-<%= TabName.Attendance %>">

          <div class="row mb10"><div class="col-md-6"><h4>Workshop Attendance</h4></div></div>

          <% if (AttendanceList.Count() != 0) { %>

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
                      <td><%= item.CoacheeFullname %></td>
                      <td><%= WebHelper.CustomCheckBox(FormFields.WorkshopAttendanceIds, item.CoacheeId.ToString(), item.IsConfirmed, "") %></td>
                      <td>
                        <p><%= item.ConfirmedByUser %></p>
                        <p><%= WebHelper.DisplayDate(item.ConfirmedDateTimeUtc.UtcToTZOrNull(null), "") %></p>
                    </td>
                  </tr>
                  <% } %>
                </tbody>
              </table>

              <div class="row mt30">
                <div class="col-md-12 align-center">
                  <button type="button" class="btn btn-secondary btnCancel mr30" data-mode="cancel">Cancel</button>
                  <button type="button" class="btn btn-primary btnUpdate" id="btnUpdateAttendance">Save changes</button>
                </div>
              </div>

            </form>

          </div>
        <% } %>
        <% else { %>
          <p>No record of attendance.</p>
        <% } %>

      <% } %>

    </div>

  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var btnUpdate, formWorkshop, isNewWorkshop, inpStartDate;
      var $btnCopy, $inpCopyWorkshopTitle, $frmCopyWorkshop;

      $(document).ready(function() {

        btnUpdate = $("#btnUpdate");
        btnDelete = $("#btnDelete");
        formWorkshop = $("#formWorkshop");
        isNewWorkshop = <%= IsNewWorkshop.ToJSTrueFalse() %>;
        inpStartDate = $('input[name="<%= FormFields.StartDate %>"]');
        $btnCopy = $("#btnCopy");
        $frmCopyWorkshop = $("#frmCopyWorkshop");
        $inpCopyWorkshopTitle = $frmCopyWorkshop.find('input[name="<%= FormFields.CopyWorkshopTitle %>"]');

        $(".btnCancel").click(function (e) { history.go(-1); });
        $btnCopy.click(DoCopy);

        inpStartDate.on("change", DateChanged);

        btnUpdate.click(UpdateWorkshop);
        btnDelete.click(DeleteWorkshopModal);

        $("#selFindJob").select2({
          placeholder: "Job # or Name",
          minimumInputLength: 3,
          width: 500,
          dropdownAutoWidth: true,
          ajax: {
            url: "#",
            dataType: "json",
            data: function (params) { return { search: params.term, mode: "sel2" }; },
            delay: 750
          }
        })
        .change(function(e) {
          var jobId = $(this).val();
        });

        if (isNewWorkshop) formWorkshop.find("input:text:not(:disabled):first").trigger("focus");

        GetSessionTypeBadge();
        $('select[name="<%= FormFields.SessionTypeId %>"]').change(GetSessionTypeBadge);


      }); // ready.

      function DoCopy() {
        $inpCopyWorkshopTitle.val("");
        ShowCopyDialog();
      }

      function ShowCopyDialog() {
        var dlg = common_InfoDialog("#dlgCopyWorkshop", {
          name: "CopyWorkshop",
          title: "Copy Workshop",
          width: 800,
          focus: $inpCopyWorkshopTitle,
          buttons: [
            { text: "Cancel", class: "btn-secondary mr20", isDefault: false, isPrimary: false, close: true },
            { text: "Create Copy", id: "btnCopyCreate", isDefault: true, isPrimary: true, close: false, click: function (ev) { CopyDialogSubmit(ev, dlg); } }
          ],
          shown: function () { },
          hide: function () { }
        });
      }

      function CopyDialogSubmit(clickEvent, dialog) {

        var $btnCopyCreate = $("#btnCopyCreate");

        AjaxSubmit({
          form: $frmCopyWorkshop,
          action: "<%= AjaxAction.CopyWorkshop %>",
          onError: function (jqXHR, textStatus, errorThrown) {
            common_InfoDialog("Process failed, please try again later.");
          }
        });
      }

      function GetSessionTypeBadge() {
        var selDataWorkshopType = $('select[name="<%= FormFields.SessionTypeId %>"]').find(':selected').data('<%= DataAttr.WorkshopType %>');
        var badgeHtml = "N/A";

        if (selDataWorkshopType) {
          var badgeText = selDataWorkshopType == '<%= WorkshopTypeEnum.F2F %>' ? "In person" : "Online";
          var badgeIcon = selDataWorkshopType == '<%= WorkshopTypeEnum.F2F %>' ? "storefront-outline" : "laptop-outline";

          // Construct the badge HTML
          badgeHtml = '<div class="type-delivery"><span class="badge"><ion-icon name="' + badgeIcon + '" role="img"></ion-icon>&nbsp;' + badgeText + '</span></div>';
        }
        // Set the HTML content of the div with badge
        $('#SessionTypeBadge').html(badgeHtml);
      }


      function DateChanged(e) {
        if ($(e.target).val() == "") {
          $('select[name^="<%= FormFields.StartTime %>"]').val("").trigger("change");
          $('select[name^="<%= FormFields.EndTime %>"]').val("").trigger("change");
        }
      }

      function DeleteWorkshopModal() {

        BootstrapDialog.show({
          type: BootstrapDialog.TYPE_WARNING,
          title: 'Confirmation',
          message: "Delete this Workshop?",
          buttons: [
            {
              label: 'No', cssClass: 'btn-secondary',
              action: function (dialog) { dialog.close(); }
            },
            {
              label: 'Yes', cssClass: 'btn-primary',
              action: function (dialog) { dialog.close(); DeleteWorkshop(); }
            }
          ]
        });
      }

      function DeleteWorkshop() {

        AjaxSubmit({
          form: formWorkshop,
          action: "DeleteWorkshop",
          onError: function (jqXHR, textStatus, errorThrown) {
            common_InfoDialog("Couldn't delete the Workshop. Please try again later.");
          }
        });
      }

      function UpdateWorkshop() {

        AjaxSubmit({
          form: formWorkshop,
          action: "UpdateWorkshop",
          onError: function (jqXHR, textStatus, errorThrown) {
            common_InfoDialog("Couldn't update the Workshop. Please try again later.");
          }
        });
      }

    })(jQuery);
  </script>

  <script type="text/javascript">
    (function ($) {

      var btnUpdateAttendance;
      var formWorkshopAttendance;

      $(document).ready(function () {

        formWorkshopAttendance = $("#formWorkshopAttendance");
        btnUpdateAttendance = $("#btnUpdateAttendance");
        btnUpdateAttendance.click(UpdateAttendance);

        $(".btnCancel").click(function (e) { history.go(-1); });

      }); // ready.

      function UpdateAttendance() {

        AjaxSubmit({
          form: formWorkshopAttendance,
          action: "UpdateAttendance",
          onSuccess: function (jqXHR, data) {
            location.reload();
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_InfoDialog("Couldn't update the Workshop's Attendance. Please try again later.");
          }
        });
      }

    })(jQuery);
  </script>
</asp:Content>


