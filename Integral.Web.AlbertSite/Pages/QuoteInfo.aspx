<%@ Page Language="C#" AutoEventWireup="true" CodeFile="QuoteInfo.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.QuoteInfo" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <style>
    .autosave-header { margin-top: -39px; }
    .productRequiresSubscription { background-color: #634CFF; color: #fff; border-radius: 4px; padding: 5px; width: 100%; text-align:center; margin-bottom: 10px; }
    .qtyLeft { border-radius: 6px 0px 0px 6px; border-right: none;}
    .qtyRight { border-radius: 0px 6px 6px 0px; }
  </style>

  <form class="form-horizontal quoteInfo" id="clientForm">

    <%-- These are updated dynamically when new row is created, after submitting the first tab/page. --%>
    <input type="hidden" name="<%= FormFields.QuoteGUID %>" value="<%= QuoteInfo.PublicGuid %>" />
    <input type="hidden" name="<%= FormFields.ActiveTab %>" value="" />


    <div class="btnholder-header float-right autosave-header ">
      <span id="AutoSaveCountdownlbl" class="float-right"></span>
      <br />
      <div class="mt15 mb10 flex">
        <% if (CanCopyQuote) { %>
          <a href="<%= PathHelper.Pages.QuotePublicView(QuoteInfo.PublicGuid, true) %>" class="btn btn-primary btn-sm mr20"
            target="_blank"><ion-icon name="eye-outline"></ion-icon>View Client Quote</a>
          <button type="button" id="btnCopy" class="btn btn-info" title="Copy Quote"><%= WebHelper.Icon.Copy %></button>
        <% } %>
        <% if (CanDeleteQuote) { %>
          <button type="button" id="btnDelete" class="btn btn-warning ml10" title="Delete Quote"><%= WebHelper.Icon.Delete %></button>
        <% } %>
        <% if (CanEditQuote || CanEditQuoteProject || CanEditQuoteSplits) { %>
          <button type="button" id="btnUpdate" class="btn btn-primary ml10"><%= IsNewQuote ? "Save Quote" : "Save" %></button>
        <% } %>
      </div>
    </div>

    <ul class="nav nav-tabs nav-tabs-underlined" id="formTabs">
      <li role="presentation" class="active">
        <a class="nav-link" data-tabname="<%= PathHelper.QuoteTabEnum.project %>" id="tab-<%= PathHelper.QuoteTabEnum.project %>" data-toggle="tab" href="#panel-<%= PathHelper.QuoteTabEnum.project %>" role="tab" aria-controls="panel-<%= PathHelper.QuoteTabEnum.project %>" aria-selected="true"><%= GetNextTabNumber() %>. Project</a>
      </li>
      <% if (!IsClientView) { %>
        <li role="presentation">
          <a class="nav-link" data-tabname="<%= PathHelper.QuoteTabEnum.settings %>" id="tab-<%= PathHelper.QuoteTabEnum.settings %>" data-toggle="tab" href="#panel-<%= PathHelper.QuoteTabEnum.settings %>" role="tab" aria-controls="panel-<%= PathHelper.QuoteTabEnum.settings %>"><%= GetNextTabNumber() %>. Settings</a>
        </li>
        <% if (CanViewQuoteSplits) { %>
          <li role="presentation">
            <a class="nav-link" data-tabname="<%= PathHelper.QuoteTabEnum.splits %>" id="tab-<%= PathHelper.QuoteTabEnum.splits %>" data-toggle="tab" href="#panel-<%= PathHelper.QuoteTabEnum.splits %>" role="tab" aria-controls="panel-<%= PathHelper.QuoteTabEnum.splits %>"><%= GetNextTabNumber() %>. Splits</a>
          </li>
        <% } %>
      <% } %>
      <li role="presentation">
        <a class="nav-link" data-tabname="<%= PathHelper.QuoteTabEnum.components %>" id="tab-<%= PathHelper.QuoteTabEnum.components %>" data-toggle="tab" href="#panel-<%= PathHelper.QuoteTabEnum.components %>" role="tab" aria-controls="panel-<%= PathHelper.QuoteTabEnum.components %>"><%= GetNextTabNumber() %>. Components</a>
      </li>
      <% if (!IsClientView || (IsClientView && !QuoteInfo.CoverLetterHtml.IsNullOrEmptyOrWhitespace())) { %>
        <li role="presentation">
          <a class="nav-link" data-tabname="<%= PathHelper.QuoteTabEnum.coverLetter %>" id="tab-<%= PathHelper.QuoteTabEnum.coverLetter %>" data-toggle="tab" href="#panel-<%= PathHelper.QuoteTabEnum.coverLetter %>" role="tab" aria-controls="panel-<%= PathHelper.QuoteTabEnum.coverLetter %>"><%= GetNextTabNumber() %>. Cover Letter</a>
        </li>
      <% } %>
      <% if (QuoteInfoSaved) { %>
        <li role="presentation">
          <a class="nav-link" data-tabname="<%= PathHelper.QuoteTabEnum.info %>" id="tab-<%=
            PathHelper.QuoteTabEnum.info %>" data-toggle="tab" href="#panel-<%= PathHelper.QuoteTabEnum.info %>"
            role="tab" aria-controls="panel-<%= PathHelper.QuoteTabEnum.info %>"><%= GetNextTabNumber() %>. Info</a>
        </li>
      <% } %>
    </ul>

    <div class="tab-content">
      <div class="tab-pane tab-quote tab-<%= PathHelper.QuoteTabEnum.project %> fade in active" id="panel-<%= PathHelper.QuoteTabEnum.project %>" role="tabpanel" aria-labelledby="tab-<%= PathHelper.QuoteTabEnum.project %>"></div>
      <% if (!IsClientView) { %>
        <div class="tab-pane tab-quote tab-<%= PathHelper.QuoteTabEnum.settings %> fade in" id="panel-<%= PathHelper.QuoteTabEnum.settings %>" role="tabpanel" aria-labelledby="tab-<%= PathHelper.QuoteTabEnum.settings %>"></div>
        <% if (CanViewQuoteSplits) { %>
          <div class="tab-pane tab-quote tab-<%= PathHelper.QuoteTabEnum.splits %> fade in" id="panel-<%= PathHelper.QuoteTabEnum.splits %>" role="tabpanel" aria-labelledby="tab-<%= PathHelper.QuoteTabEnum.splits %>"></div>
        <% } %>
      <% } %>
      <div class="tab-pane tab-quote tab-<%= PathHelper.QuoteTabEnum.components %> fade in" id="panel-<%= PathHelper.QuoteTabEnum.components %>" role="tabpanel" aria-labelledby="tab-<%= PathHelper.QuoteTabEnum.components %>"></div>
      <% if (!IsClientView || (IsClientView && !QuoteInfo.CoverLetterHtml.IsNullOrEmptyOrWhitespace())) { %>
        <div class="tab-pane tab-quote tab-<%= PathHelper.QuoteTabEnum.coverLetter %> fade in" id="panel-<%= PathHelper.QuoteTabEnum.coverLetter %>" role="tabpanel" aria-labelledby="tab-<%= PathHelper.QuoteTabEnum.coverLetter %>"></div>
      <% } %>
      <% if (QuoteInfoSaved) { %>
        <div class="tab-pane tab-quote tab-<%= PathHelper.QuoteTabEnum.info %> fade in" id="panel-<%= PathHelper.QuoteTabEnum.info %>" role="tabpanel" aria-labelledby="tab-<%= PathHelper.QuoteTabEnum.info %>"></div>
      <% } %>
    </div>

    <div class="tab-panel" data-appendTo="panel-<%= PathHelper.QuoteTabEnum.project %>">

      <% new WebHelper.Form.SectionTitle() {
          TitleText = "Client and Project",
          HelpText = "Choose a project and client to start your quote.",
          HelpLinkUrl = "https://help.helloable.co/en/articles/12007475-create-a-quote"
      }.WriteHtml(); %>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Customer Company:",
          ContentHtml = new WebHelper.Form.Select() {
            InputName = FormFields.CompanyId,
            Options = GetCompanyOptions(),
            IsReadOnly = !CanEditQuote,
          }.ToHtml()
        }.WriteHtml(); %>

      <div class="displaynone add-new-fields" id="newCompanyInfo">

        <% new WebHelper.Form.FormRow() {
            LabelText = "New Company Name:",
            ContentHtml = new WebHelper.Form.TextInput() {
              InputName = FormFields.CompanyName,
            }.ToHtml()
          }.WriteHtml(); %>

        <%= GetCompanyLeadDropdownHtml() %>
      </div>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Project:",
          ContentHtml = new WebHelper.Form.Select() {
            InputName = FormFields.ProjectJobNumber,
            Placeholder = IsUpdatingAcceptedQuote ? "Select Project" : "Select or add Project",
            Options = GetProjectTopOptions(),
            Value = QuoteInfo?.JobNumber,
            IsReadOnly = !CanEditQuoteProject
          }.ToHtml()
        }.WriteHtml(); %>

      <div class="displaynone add-new-fields" id="newProjectInfo">
        <div id="project_noedit">

          <% new WebHelper.Form.FormRow() {
              LabelText = "New Job Number:",
              ContentHtml = new WebHelper.Form.TextInput() {
                InputName = FormFields.TBAJobNumber,
                Placeholder = "TBA",
                WidthPreset = WebHelper.Form.ControlWidthPreset.JobNumber,
                IsReadOnly = true
              }.ToHtml()
            }.WriteHtml();
          %>

          <% new WebHelper.Form.FormRow() {
              LabelText = "New Project Name:",
              ContentHtml = new WebHelper.Form.TextInput() {
                InputName = FormFields.ProjectName
              }.ToHtml()
            }.WriteHtml();
          %>
        </div>
      </div>

      <% new WebHelper.Form.FormRow() {
          LabelText = "Customer Contact:",
          ContentHtml = new WebHelper.Form.Select() {
            ID = "selFindUser",
            IsReadOnly = !CanEditQuote,
            InputName = FormFields.ContactUserId,
            Placeholder = "[Select or add new Contact]",
            Options = IsNewQuote ? null : GetSelectedUserOption(),
            SearchPlaceholder = "Name or Email",
            AjaxSearch = new WebHelper.Form.AjaxSearch() {
              SearchKey = UserSearchQueryKey
            }
          }.ToHtml()
        }.WriteHtml(); %>

      <div class="displaynone add-new-fields" id="newContactInfo">
        <% new WebHelper.Form.FormRow() { LabelText = "First Name:", ContentHtml = new WebHelper.Form.TextInput() { InputName = FormFields.ContactFirstName }.ToHtml() }.WriteHtml(); %>
        <% new WebHelper.Form.FormRow() { LabelText = "Last Name:", ContentHtml = new WebHelper.Form.TextInput() { InputName = FormFields.ContactLastName }.ToHtml() }.WriteHtml(); %>
        <% new WebHelper.Form.FormRow() { LabelText = "Email:", ContentHtml = new WebHelper.Form.TextInput() { InputName = FormFields.ContactEmail }.ToHtml() }.WriteHtml(); %>
        <% new WebHelper.Form.FormRow() { LabelText = "Role:", ContentHtml = new WebHelper.Form.TextInput() { InputName = FormFields.ContactRole }.ToHtml() }.WriteHtml(); %>
        <% new WebHelper.Form.FormRow() { LabelText = "Phone:", ContentHtml = new WebHelper.Form.TextInput() { InputName = FormFields.ContactPhone }.ToHtml() }.WriteHtml(); %>
        <% new WebHelper.Form.FormRow() { LabelText = "City:", ContentHtml = new WebHelper.Form.TextInput() { InputName = FormFields.ContactCity }.ToHtml() }.WriteHtml(); %>
      </div>

      <hr />
      <% new WebHelper.Form.FormRow() {
          LabelText = "Deal Source:",
          ContentHtml = new WebHelper.Form.Select() {
            InputName = FormFields.QuoteDealSourceId,
            Options = GetDealSources(),
            IsReadOnly = !CanEditQuoteDealSource,
          }.ToHtml()
        }.WriteHtml(); %>

      <% if (CanEditQuote) { %>
        <div class="form-group mt30">
          <div class="col-md-offset-6 col-md-2 col-xs-12">
            <button type="button" id="btnP1Next" class="btn btn-primary btnContinue float-right">Next: Settings</button>
          </div>
        </div>
      <% } %>

    </div>

    <% if (!IsClientView) { %>

      <div class="tab-panel" data-appendTo="panel-<%= PathHelper.QuoteTabEnum.settings %>">

        <% new WebHelper.Form.SectionTitle() {
            TitleText = "Settings",
            HelpText = "Customize quote settings: title, status, branding, start date, and more.",
          }.WriteHtml(); %>

        <% new WebHelper.Form.FormRow() {
            LabelText = "Quote Title:",
            ContentHtml = new WebHelper.Form.TextInput() {
              InputName = FormFields.QuoteTitle,
              Value = QuoteInfo.QuoteTitle,
              IsReadOnly = !CanEditQuote
            }.ToHtml()
          }.WriteHtml(); %>

        <% new WebHelper.Form.FormRow() {
            LabelText = "Quote Status:",
            ContentHtml = new WebHelper.Form.Select() {
              InputName = FormFields.QuoteStatusId,
              Options = GetStatusOptions(),
              IsReadOnly = !CanEditQuote
            }.ToHtml()
          }.WriteHtml(); %>

        <% if (CanViewQuoteBranding) { %>
          <% new WebHelper.Form.FormRow() {
              LabelText = "Quote Branding:",
              ContentHtml = new WebHelper.Form.Select() {
                InputName = FormFields.BrandingOrgId,
                Options = GetBrandingOrgOptions(),
                IsReadOnly = !CanEditQuoteBranding
              }.ToHtml()
            }.WriteHtml(); %>
        <% } %>

        <% if (SessionHelper.AppAccess.Quotes.CanEditStartDate(ProjectInfo)) { %>
          <% new WebHelper.Form.FormRow() {
              LabelText = "Est. Start Date:",
              LabelHelpText = "Time in Western Standard Time (WST)",
              LabelPosition = WebHelper.Form.LabelPosition.LeftWide,
              ContentAlign = WebHelper.Form.ContentAlign.Right,
              ContentHtml = new WebHelper.Form.DatePicker() {
                InputName = FormFields.EstimatedStartDateLocal,
                IsReadOnly = !CanEditQuote,
                Value = TimeHelper.UtcToAppDefaultTimeZone(QuoteInfo.EstimatedStartDateUtc ?? DateTime.UtcNow),
              }.ToHtml()
            }.WriteHtml(); %>
        <% } %>

        <% new WebHelper.Form.FormRow() {
            LabelText = "Embedded Sales Material:",
            LabelHelpText = "Choose whether to include sales material on your public quote.",
            LabelHelpUrl = "https://help.helloable.co/en/articles/12079040-embedded-sales-material",
            LabelPosition = WebHelper.Form.LabelPosition.LeftWide,
            ContentAlign = WebHelper.Form.ContentAlign.Right,
            ContentHtml = new WebHelper.Form.Select() {
              WidthPreset = WebHelper.Form.ControlWidthPreset.DatePicker,
              InputName = FormFields.QuoteSalesContentTypeId,
              Options = GetSalesContentTypeOptions(),
              ID = "selSalesContent",
              IsReadOnly = !CanEditQuote,
            }.ToHtml(),
          }.WriteHtml(); %>

        <div id="sales-content-container"></div>

        <div class="sales-content-inputs hidden" id="sales-content-<%= ConfigHelper.QuoteSalesContentTypeId.UrlList %>">
          <% new WebHelper.Form.FormRow() {
              LabelText = "Template:",
              LabelHelpText = "Choose a template to display as your cover page.",
              ContentHtml = new WebHelper.Form.Select() {
                InputName = FormFields.QuoteSalesContentUrlId,
                Options = GetSalesContentUrlOptions(),
                ID = "selSalesContentUrlId",
                IsReadOnly = !CanEditQuote,
              }.ToHtml(),
            }.WriteHtml(); %>
        </div>

        <div class="sales-content-inputs hidden" id="sales-content-<%= ConfigHelper.QuoteSalesContentTypeId.PDF %>">
          <% new WebHelper.Form.FormRow() {
              LabelText = "PDF:",
              LabelHelpText = "Upload Your PDF",
              ContentHtml = @"
                    <input class=""filepond"" name=""filepond"" type=""file"" data-max-file-size=""20MB""
                    data-max-files=""1""></input>"
            }.WriteHtml(); %>
        </div>

        <div class="sales-content-inputs hidden" id="sales-content-<%= ConfigHelper.QuoteSalesContentTypeId.WebPageUrl %>">
          <% new WebHelper.Form.FormRow() {
              LabelText = "Web Page URL:",
              ContentHtml = new WebHelper.Form.TextInput() {
                LeftSideLabelText = "https://",
                InputName = FormFields.QuoteSalesContentWebPageUrl,
                Value = QuoteInfo.QuoteSalesContentWebPageUrl,
                IsReadOnly = !CanEditQuote
              }.ToHtml()
            }.WriteHtml(); %>
        </div>

        <div class="sales-content-inputs hidden" id="sales-content-<%= ConfigHelper.QuoteSalesContentTypeId.Qwilr %>">
          <% new WebHelper.Form.FormRow() {
              LabelText = "Qwilr Embed URL:",
              ContentHtml = new WebHelper.Form.TextInput() {
                InputName = FormFields.QwilrUrl,
                Value = QuoteInfo.QwilrUrl,
                IsReadOnly = !CanEditQuote,
                Placeholder = "https://pages.qwilr.com/Coaching-Proposal-4hQK2VAS4ezo",
              }.ToHtml()
            }.WriteHtml(); %>

          <% new WebHelper.Form.FormRow() {
              LabelText = "Qwilr PDF URL:",
              ContentHtml = new WebHelper.Form.TextInput() {
                InputName = FormFields.QwilrPDFUrl,
                Value = QuoteInfo.QwilrPDFUrl,
                IsReadOnly = !CanEditQuote
              }.ToHtml()
            }.WriteHtml(); %>
        </div>

        <% new WebHelper.Form.FormRow() {
            LabelText = "GST Applicable:",
            ContentHtml = new WebHelper.Form.CheckBox() {
              InputName = FormFields.GSTApplicable,
              Checked = DbHelper.XeroTaxType.GetGSTApplicableFromQuoteTaxTypeOrNull(QuoteInfo.XeroTaxType) ?? true,
              IsReadOnly = !CanEditQuote
            }.ToHtml()
          }.WriteHtml();
        %>

        <% new WebHelper.Form.FormRow() {
            LabelText = "Exclude From Sales Incentive:",
            ContentHtml = new WebHelper.Form.CheckBox() {
              InputName = FormFields.ExcludeFromSalesIncentive,
              Checked = QuoteInfo.ExcludeFromSalesIncentive,
              IsReadOnly = !CanUpdateExcludeFromSalesIncentive
            }.ToHtml()
          }.WriteHtml();
        %>

        <% if (CanEditFreshSalesOption) { %>
          <% new WebHelper.Form.FormRow() {
              LabelText = "Add to FreshSales:",
              ContentHtml = new WebHelper.Form.CheckBox() {
                InputName = FormFields.AddToFreshSales,
                Checked = IsNewQuote ? true : QuoteInfo.AddToFreshSales,
                IsReadOnly = !CanEditQuote
              }.ToHtml()
            }.WriteHtml();
          %>
        <% } %>

        <% if (CanEditQuote) { %>
          <div class="form-group mt30">
            <div class="col-md-offset-6 col-md-2 col-xs-12">
              <button type="button" id="btnP2Next" class="btn btn-primary btnContinue float-right">Next: Splits</button>
            </div>
          </div>
        <% } %>

      </div>

      <% if (CanViewQuoteSplits) { %>

        <div class="tab-panel" data-appendTo="panel-<%= PathHelper.QuoteTabEnum.splits %>">

          <% new WebHelper.Form.SectionTitle() {
              TitleText = "Team Members",
              HelpText = "Add team members by searching and selecting.",
            }.WriteHtml(); %>

          <%= GetTeamMembersDropdownHtml() %>

          <div id="SplitsCalculation">
            <h4 class="ServiceSplitsHeader mt20 mb15">Project Roles</h4>
            <div class="table-responsive w760">
              <table class="table table-bordered" id="tblSplitRoles">
                <thead>
                  <tr>
                    <th class="w200">Project Roles</th>
                    <th class="w200">Project Split (%)</th>
                    <th colspan="2">Name</th>
                  </tr>
                </thead>
                <%= GetProjectSplits_Roles() %>
                <tfoot>
                  <tr>
                    <td>Sub-Total</td>
                    <td colspan="3"><b><span class="<%= DataAttrs.TotalTableSum %> lblPercentageValue">0</span>%</b></td>
                  </tr>
                </tfoot>
              </table>
            </div>

            <h4 class="ServiceSplitsHeader mt20 mb15">Project Services</h4>
            <div class="table-responsive w760">
              <table class="table table-bordered" id="tblSplitServices">
                <thead>
                  <tr>
                    <th class="w200">Project Service</th>
                    <th class="w200">Project Split (%)</th>
                    <th>Name</th>
                    <th>Select</th>
                  </tr>
                </thead>
                <%= GetProjectSplits_Services() %>
                <tfoot>
                  <tr>
                    <td>Sub-Total</td>
                    <td colspan="3"><b><span class="<%= DataAttrs.TotalTableSum %> lblPercentageValue">0</span>%</b></td>
                  </tr>
                </tfoot>
              </table>
            </div>

            <h4 class="ServiceSplitsHeader mt20 mb15">Total</h4>
            <div class="table-responsive w760">
              <table class="table table-bordered">
                <thead>
                  <tr>
                    <th class="w200">Delivery Team</th>
                    <th class="w200">Project Split (%)</th>
                    <th colspan="2"></th>
                  </tr>
                </thead>
                <%= GetProjectSplits_Total() %>
                <tfoot>
                  <tr class="select-percentages">
                    <td>Total</td>
                    <td colspan="3"><b><span id="<%= FormFields.SplitsTotalPercentage %>" class="lblPercentageValue">0</span>%</b></td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>

          <% if (CanEditQuote) { %>
            <div class="form-group mt30">
              <div class="col-md-offset-5 col-md-2 col-xs-12">
                <button type="button" id="btnP3Next" class="btn btn-primary btnContinue float-right">Next: Components</button>
              </div>
            </div>
          <% } %>

        </div>
      <% } %>
    <% } %>

    <div class="tab-panel quoteComponents" data-appendTo="panel-<%= PathHelper.QuoteTabEnum.components %>">

      <% new WebHelper.Form.SectionTitle() {
          TitleText = "Program Components ",
        }.WriteHtml();
      %>

      <% if (!IsClientView) { %>
        <div id="WarningMessages" class="quoteComponentWarningMessage" style="display: none;"></div>
      <% } %>

      <table id="ProductListHead" class="table">
        <thead>
          <tr>
            <th class="col w50"></th>
            <th class="col">Description</th>
            <th class="col align-center" width="90px">Optional</th>
            <% if (CanViewQuoteComponentPrice) { %>
              <th class="col align-center" width="120px">Unit Price</th>
            <% } %>
            <th class="col align-center" width="100px">Quantity</th>
            <% if (CanViewQuoteComponentPrice) { %>
              <th class="col align-center" width="120px">Line Total</th>
            <% } %>
            <th class="col" width="60px"></th>
          </tr>
        </thead>
        </table>

      <div id="ProductListBody" class="<%= CanEditQuote ? "" : "readonly" %>"></div>

      <div id="ProductListFoot" class="quoteInfo">
        <div class="content-action-bar">
          <div class="left">
            <% if (CanEditQuote) { %>
              <a href="#" class="btn btn-secondary" id="btnAddProduct"><i><%= WebHelper.Icon.Circle_Plus %></i><label>Add<br/>Component</label></a>
            <% } %>
          </div>
          <div class="right">
            <div class="col mr20" id="GrandTotalText"><b>Quote Total</b><%= QuoteInfo.IsAccepted ? "" : " <small>(if all options accepted)</small>" %>:</div>
            <div class="col" id="GrandTotalAmount">$0.00</div>
          </div>
        </div>
      </div>

      <div id="dlgAddProduct" class="displaynone" data-editrow="">

        <div class="productRequiresSubscription displaynone"><%= ProductRequiresSubscriptionMsg %></div>

        <table class="mt0 w100p"><tr>
          <td class="w100 align-right pr10 bordernone">Product:</td>
          <td class="bordernone"><%= WebHelper.GetSelect(new WebHelper.SelectInfo() {
              ID = "selProductId",
              Select2WordWrap =true,
              TopOptionsHtml = GetAddProductOptions(),
              Placeholder = "Select Product"
            }) %></td>
        </tr></table>
        <table class="mt20 w100p"><tr>
          <td class="w100 align-right pr10 bordernone">Description:</td>
          <td class="bordernone fixeddescription">
            <textarea class="form-control" rows="5" id="inpDlgItemDescription"></textarea>
          </td>
        </tr></table>
        <table class="mt20 mb20 width-auto"><tr>
          <td class="w100 align-right pr10 bordernone">Unit Price:</td>
          <td class="">
            <div class="flex flex-align-center gap10">
              <input type="text" class="form-control inp-currency" data-decimalplaces="2" id="inpUnitPrice" />
              <p>Qty:</p>
              <div class="flex">
                <input type="text" maxlength="<%= ((int)WebHelper.InputMaxLength.QuoteItemQuantity).ToString() %>"
                class="form-control align-right qtyLeft" id="inpQuantity" />
                <input type="text" maxlength="<%= ((int)WebHelper.InputMaxLength.QuoteItemUnitType).ToString() %>"
                class="form-control qtyRight" id="inpQtyDescr" placeholder="Unit Type" />
              </div>
              <p>Optional:</p>
              <select class="w150" size="1" id="inpOptional"><%= GetOptionalOptions() %></select>
            </div>
          </td>
        </tr></table>
      </div>

    </div>

    <% if (!IsClientView || (IsClientView && !QuoteInfo.CoverLetterHtml.IsNullOrEmptyOrWhitespace())) { %>
      <div class="tab-panel" data-appendTo="panel-<%= PathHelper.QuoteTabEnum.coverLetter %>">

        <% new WebHelper.Form.SectionTitle() {
            TitleText = "Cover Letter",
          }.WriteHtml(); %>

        <textarea <%= CanEditQuote ? "" : "disabled" %> class="form-control" id="txtCoverLetter" rows="15" name="<%= FormFields.CoverLetterHtml %>">
          <%= QuoteInfo.CoverLetterHtml.HTMLEncode() %>
        </textarea>

      </div>
    <% } %>

    <div class="tab-panel" data-appendTo="panel-<%= PathHelper.QuoteTabEnum.info %>">

      <% if (IsAccepted) { %>
        <div class="row mb10"><div class="col-md-6"><h4>Sign-off Details</h4></div></div>
        <div>
          <%= WebHelper.GetTextInputDual("Client name:", "", QuoteInfo.ClientFirstName, "", "", QuoteInfo.ClientLastName, "", true, WebHelper.InputMaxLength.EmailName, 6) %>
          <%= WebHelper.GetTextInput("Client email:", "", QuoteInfo.ClientEmailAddress, 6, "", true) %>
          <%= WebHelper.GetTextInputDual("Accounts Payable name:", "", QuoteInfo.AccPayFirstName, "", "", QuoteInfo.AccPayLastName, "", true, WebHelper.InputMaxLength.EmailName, 6) %>
          <%= WebHelper.GetTextInput("Client email:", "", QuoteInfo.AccPayEmailAddress, 6, "", true) %>
          <hr />
          <%= WebHelper.GetTextInput("Quote Signed Date:", "", QuoteInfo.ClientAcceptedUtc.UtcToTZOrNull().ToString(), 6, "", true) %>
          <%= WebHelper.GetTextInput("Quote Accepted Amount:", "", QuoteInfo.ClientAcceptedAmount.GetValueOrDefault(QuoteInfo.QuoteItemTotalAmount).ToString("C").ToString(), 6, "", true) %>
        </div>
        <hr />
      <% } %>

      <% if (QuoteInfoSaved) { %>

        <div class="mt20">

          <% new WebHelper.Form.FormRow() {
              LabelText = "Client Quote",
              LabelHelpText = "Share the Client Quote link or PDF with your Client.",
              LabelHelpUrl = "",
              ContentHtml = $@"
                <div class=""flex flex-align-center gap10"">
                  <a href=""{PathHelper.Pages.QuotePublicView(QuoteInfo.PublicGuid, true)}"" class=""btn btn-primary btn-sm""
                    target=""_blank""><ion-icon name=""eye-outline""></ion-icon>View Client Quote</a>
                  <button id=""copy-link-btn"" class=""btn btn-primary btn-sm mr20""
                    data-target=""{PathHelper.Pages.QuotePublicView(QuoteInfo.PublicGuid, true)}""><ion-icon name=""link-outline""></ion-icon>Copy Link</button>
                  <a href=""{PathHelper.Pages.QuotePublicPDF(QuoteInfo.PublicGuid, true)}"" class=""btn btn-primary btn-sm""
                    target=""_blank""><ion-icon name=""document-outline""></ion-icon>Download PDF</a>
                  {(IsAccepted && !QuoteInfo.QwilrUrl.IsNullOrEmpty()
                ? $@"<a href=""{QuoteInfo.QwilrUrl.HTMLEncode()}"" class=""btn btn-primary btn-sm ml30"" target=""_blank"">Qwilr Proposal</a>"
                : "")}
                </div>",
            }.WriteHtml(); %>

          <hr />

          <% if (!SessionHelper.IsUserRoleClient) { %>
            <% new WebHelper.Form.FormRow() {
                LabelText = "Notes:",
                LabelHelpText = "Notes only appear to your Project Team.",
                ContentHtml = new WebHelper.Form.TextArea() {
                  InputName = FormFields.QuoteNotes,
                  Value = QuoteInfo.QuoteNotes,
                  IsReadOnly = !CanEditQuote,
                  IsRichText = true,
                }.ToHtml()
              }.WriteHtml(); %>
          <% } %>

        </div>
      <% } %>

    </div>

  </form>

  <div id="dlgCopyQuote" class="displaynone">
    <form id="frmCopyQuote" method="post" action="#" onsubmit="return false;">
      <div class="row">
        <div class="col w150 align-left mb20 pr10">New Quote Title:</div>
        <div class="col"><input type="text" class="form-control w760" name="<%= FormFields.CopyQuoteTitle %>" /></div>
      </div>
    </form>
  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <% if (IsNewQuote || QuoteInfo.QuoteItems.Count == 0) { %>
    <script> function AddExistingProducts(ProductRowInfo, AddProductRow) { } </script>
  <% } else { %>
    <script>
      function AddExistingProducts(ProductRowInfo, AddProductRow) {
        <% foreach(var qi in QuoteInfo.QuoteItems) { %>
          AddProductRow(new ProductRowInfo(
            <%= qi.ProductId.GetValueOrDefault(0) %>
            , "<%= qi.ItemDescription.ToStringOrEmptyIfNull().RegexReplace("[\r\n]", "").Replace("\"", "\\\"") %>"
            , <%= qi.OptionalId %>
            , <%= qi.UnitPrice.GetValueOrDefault(0) %>
            , <%= qi.Quantity.GetValueOrDefault(0) %>
            , "<%= qi.QuantityDescr.ToStringOrEmptyIfNull() %>"
            , <%= qi.IsAccepted.ToJSTrueFalseOrNull() %>
            , <%= qi.MinAllowedQuotePrice.GetValueOrDefault(0) %>
            , <%= qi.RequiresSubscription.ToJSTrueFalse() %>
            , <%= qi.SubscriptionId.GetValueOrDefault(0) %>
            , <%= qi.IsQuantityPerPerson.ToJSTrueFalse() %>
            , <%= qi.IsSubscription.ToJSTrueFalse() %>
          ));
        <% } %>
      }
    </script>
  <% } %>

  <script type="text/javascript">
    (function($) {

      var isReadOnly = <%= (!CanEditQuote).ToJSTrueFalse() %>;
      var isClientView = <%= (IsClientView).ToJSTrueFalse() %>;
      var CanEditQuoteProject = <%= (CanEditQuoteProject).ToJSTrueFalse() %>;
      var CanEditQuoteSplits = <%= (CanEditQuoteSplits).ToJSTrueFalse() %>;
      var canViewQuoteComponentPrice = <%= (CanViewQuoteComponentPrice).ToJSTrueFalse() %>;

      var $clientForm = $("#clientForm");
      var $formTabs = $("#formTabs");
      var $selCompanyId = $('select[name="<%= FormFields.CompanyId %>"]');
      var $selProjectJobNumber = $('select[name="<%= FormFields.ProjectJobNumber %>"]');
      var $selContactUserId = $('select[name="<%= FormFields.ContactUserId %>"]');
      var $activeTabName = $('input:hidden[name="<%= FormFields.ActiveTab %>"]');
      var $servicesTotalPercentage = $('span[name="<%= FormFields.ServicesTotalPercentage %>"]');
      var $newCompanyInfo = $("#newCompanyInfo");
      var $newProjectInfo = $("#newProjectInfo");
      var $newContactInfo = $("#newContactInfo");
      var $selFindUser = $("#selFindUser");
      var $btnSel2AddContact = $('<button class="btn btn-primary btn-xsm">add a new user</button>');
      var $quoteInfoSaved = <%= QuoteInfoSaved.ToJSTrueFalse() %>;
      var $btnAddProduct = $("#btnAddProduct");
      var $selDlgProducts = $("#selProductId");
      var $dlgAddProduct = $("#dlgAddProduct");
      var $inpDlgItemDescription = $("#inpDlgItemDescription");
      var $inpDlgUnitPrice = $("#inpUnitPrice");
      var $inpDlgQuantity = $("#inpQuantity");
      var $inpDlgQtyDescr = $("#inpQtyDescr");
      var $inpDlgOptional = $("#inpOptional");
      var $ProductListBody = $("#ProductListBody");
      var $btnUpdate = $("#btnUpdate");
      var $btnCopy = $("#btnCopy");
      var $frmCopyQuote = $("#frmCopyQuote");
      var $inpCopyQuoteTitle = $frmCopyQuote.find('input[name="<%= FormFields.CopyQuoteTitle %>"]');
      var $btnDelete = $("#btnDelete");
      var divWarningMessages = $("#WarningMessages");
      var $selSalesContent = $("#selSalesContent");
      var $copyToClipboardButton = $("#copy-link-btn");
      var $pdfUpload = $("#pdf-upload");

      var isNewQuote= <%= IsNewQuote ? "true" : "false" %>;
      var $rowCurrentlyEditing = null;
      var isExistingClient = <%= IsExistingClient.ToJSTrueFalse() %>;

      var CalculatePlatformFee_Busy = false;
      var allowQuoteUpdate, countdownTimer, autoSaveCountdownlbl, isLoadingPage, isUpdatingAcceptedQuote;
      var IsComponentsModalOpen = false;
      var projectTabName, componentTabName, currentTabName;
      var hasPreviouslyRemovedSubscription = false;

      function ProductRowInfo(productId, productName, isOptional, unitPrice, quantity, qtyDescr, isAccepted, minAllowedQuotePrice, requiresSubscription, defaultSubscriptionId, isQuantityPerPerson, isSubscription) {
        var thisObj = this;
        InitValues(productId, productName, isOptional, unitPrice, quantity, qtyDescr, isAccepted, minAllowedQuotePrice, requiresSubscription, defaultSubscriptionId, isQuantityPerPerson, isSubscription);
        function InitValues(productId, productName, isOptional, unitPrice, quantity, qtyDescr, isAccepted, minAllowedQuotePrice, requiresSubscription, defaultSubscriptionId, isQuantityPerPerson, isSubscription) {
          thisObj.IsNote = productId == null || productId == 0 || isNaN(productId);
          thisObj.ProductId = toDecimalInt(productId, 0);
          thisObj.ProductName = productName || "";
          thisObj.IsOptional = toDecimalInt(isOptional, <%= DbHelper.AbleQuotes.OptionalEnum.No.Id %>);
          thisObj.UnitPrice = unitPrice || 0;
          thisObj.Quantity = quantity || 0;
          thisObj.QtyDescr = "" + qtyDescr;
          thisObj.isAccepted = isAccepted; // can be true, false or null
          thisObj.MinAllowedQuotePrice = minAllowedQuotePrice || 0;
          thisObj.RequiresSubscription = requiresSubscription;
          thisObj.DefaultSubscriptionId = defaultSubscriptionId || "";
          thisObj.IsQuantityPerPerson = isQuantityPerPerson;
          thisObj.IsSubscription = isSubscription;
        }
        this.GetFromDialog = function () {
          var selectedVal = $selDlgProducts.val();
          var selectedProduct = $selDlgProducts.find('option[value="' + selectedVal + '"]');

          thisObj.IsNote = $selDlgProducts.val() === "<%= ProductOptionValueForNote %>";
          thisObj.ProductId = parseInt($selDlgProducts.val(), 10);
          thisObj.ProductName = $inpDlgItemDescription.val();
          thisObj.IsOptional = toDecimalInt($inpDlgOptional.val(), <%= DbHelper.AbleQuotes.OptionalEnum.No.Id %>);
          thisObj.UnitPrice = parseFloat($inpDlgUnitPrice.val().replace(/[^0-9.-]/g, ""));
          thisObj.Quantity = parseFloat($inpDlgQuantity.val().replace(/[^0-9.-]/g, ""));
          thisObj.QtyDescr = "" + $inpDlgQtyDescr.val();
          thisObj.MinAllowedQuotePrice = $selDlgProducts.find("option:selected").data("minprice") || 0;
          thisObj.RequiresSubscription = selectedProduct.attr("data-<%= DataAttrs.RequiresSubscription %>") === "true";
          thisObj.DefaultSubscriptionId = selectedProduct.attr("data-<%= DataAttrs.SubscriptionId %>") || "";
          thisObj.IsQuantityPerPerson = selectedProduct.attr("data-<%= DataAttrs.IsQuantityPerPerson %>") === "true";
          thisObj.IsSubscription = selectedProduct.attr("data-<%= DataAttrs.IsSubscription %>") === "true";
          return thisObj;
        };
        this.GetFromRow = function ($row) {
          if (!isJQuery($row) || $row.length != 1 || !$row.hasClass("product-row")) return null;
          InitValues();
          $row.find("input:hidden[name]").each(function(i, e) {
            var name = (e.name.match(/_[a-z]+$/i) || "").toString(); // names are "prodxx_name", need only chars from "_" onwards.
            var $e = $(e);
            var value = $e.val();
            switch (name) {
              case "<%= FormFields.ProdKey_IsNote %>": thisObj.IsNote = (value === "true"); break;
              case "<%= FormFields.ProdKey_Id %>": thisObj.ProductId = parseInt(value, 10) || 0; break;
              case "<%= FormFields.ProdKey_Name %>": thisObj.ProductName = value; break;
              case "<%= FormFields.ProdKey_Optional %>": thisObj.IsOptional = toDecimalInt(value, <%= DbHelper.AbleQuotes.OptionalEnum.No.Id %>); break;
              case "<%= FormFields.ProdKey_Price %>": thisObj.UnitPrice = parseFloat(value) || 0; break;
              case "<%= FormFields.ProdKey_Qty %>": thisObj.Quantity = parseFloat(value) || 0; break;
              case "<%= FormFields.ProdKey_QtyDescr %>": thisObj.QtyDescr = "" + value; break;
              case "<%= FormFields.ProdKey_MinAllowedQuotePrice %>": thisObj.MinAllowedQuotePrice = parseFloat(value) || 0; break;
              case "<%= FormFields.ProdKey_RequiresSubscription %>": thisObj.RequiresSubscription = (value === "true"); break;
              case "<%= FormFields.ProdKey_DefaultSubscriptionId %>": thisObj.DefaultSubscriptionId = parseFloat(value, 10) || 0; break;
              case "<%= FormFields.ProdKey_IsQuantityPerPerson %>": thisObj.IsQuantityPerPerson = (value === "true"); break;
              case "<%= FormFields.ProdKey_IsSubscription %>": thisObj.IsSubscription = (value === "true"); break;
            }
          });
          return thisObj;
        };
      };

      $(document).ready(function () {

        // Auto save Quote variables
        autoSaveCountdownlbl = $("#AutoSaveCountdownlbl");
        countdownSeconds = 60;
        allowQuoteUpdate = !isReadOnly || CanEditQuoteProject || CanEditQuoteSplits;
        isLoadingPage = true;
        projectTabName = "<%= PathHelper.QuoteTabEnum.project %>";
        componentTabName = "<%= PathHelper.QuoteTabEnum.components %>";
        currentTabName = "<%= SelectedQuoteTab %>";
        isUpdatingAcceptedQuote = <%= IsUpdatingAcceptedQuote.ToJSTrueFalse() %>;
        hasPreviouslyRemovedSubscription = false;

        if (isNewQuote) {
          $formTabs.hide();
        }

        if ($formTabs.length == 1 && !isNewQuote) {
          // Activate initially selected tab.

          $('a[href="#panel-<%= SelectedQuoteTab %>"]').click();

          $formTabs.click(function (e) {
            currentTabName = $(e.target).data("tabname");
            UpdateUrlAddress(currentTabName);
            if (currentTabName == componentTabName) {
              StopAutosaveTimer(); // Don't run auto-saving on Components tab
            } else {
              StartAutosaveTimer();
            }
          });
        }

        SafeSetupFilePond();

        // Can only execute auto-save actions if update is allowed.
        if (allowQuoteUpdate) {

          if (isUpdatingAcceptedQuote) {
            $btnUpdate.click(DoQuoteUpdate); // Update on Update button click.

          } else {

            // Stop or start coundown of auto-save process based on if the page/window is being seen in the browser.
            function UpdateAutoSaveStatus() {
              if (document.hidden) {
                StopAutosaveTimer();
              } else {
                if (IsComponentsModalOpen) {
                  StopAutosaveTimer();
                } else if (!$quoteInfoSaved && currentTabName === projectTabName) {
                  StopAutosaveTimer();  // Do not start the timer if the quote is being created and it's on Project tab
                } else if (currentTabName === componentTabName) {
                  StopAutosaveTimer();  // Do not start the timer if the tab is component
                } else {
                  StartAutosaveTimer(); // Update automatically every 60 seconds.
                }
              }

              $btnUpdate.click(SetupQuoteUpdate); // Update on Update button click.
              $(".btnContinue").click(SetupQuoteUpdate); // Update on Continue/Next button click.
              $formTabs.click(function () {
                if (!isLoadingPage) { // A tab click is triggered by code on page load, so this is important.

                  // Do not update Components on tab click
                  if ($('li.active a[data-tabname="components"]').length > 0) return;

                  SetupQuoteUpdate(); // Update on tab change/click.
                }
              });
            }
            // Start event to listen and do UpdateAutoSaveStatus
            document.addEventListener("visibilitychange", UpdateAutoSaveStatus, false);
            UpdateAutoSaveStatus();
          }
        }

        // Company dropdown.
        $selCompanyId.change(ChangeCompany);
        ChangeCompany(); // Set company related info and gets list of existing projects.
        // Project dropdown.
        $selProjectJobNumber.change(ChangeProject);

        new jBox('Tooltip', {
          attach: 'input[name="<%= FormFields.QwilrUrl %>"]', position: { y: 'top' }, offset: { x: 150 },
          title: 'How to Find the Qwilr Url:', content: '<img src="<%= PathHelper.UrlPath.Images %>qwilr-url-hint.png" height="35" />'
        });

        $selFindUser.change(ChangeUser);

        AddExistingProducts(ProductRowInfo, AddProductRow);
        GetProductsWarningMsgs();

        $btnCopy.click(DoCopy);
        $btnDelete.click(DoDelete);

        $(".btnTabBack").click(BackClicked);
        $(".btnContinue").click(function (ev) { ContinueClicked($(ev.target)) });

        // Component/Product list controls.
        if (!isReadOnly) {
          $ProductListBody.sortable({ handle: ".dragHandle" });
          $ProductListBody.on("click", "td.clickForEdit", ShowEditProduct);
          $ProductListBody.on("click", ".btnRemoveProduct", ShowRemoveProduct);
          $btnAddProduct.on("click", ShowAddProduct);
          // Product details dialog.
          $selDlgProducts.change(SetProductDialogSelectedProductValues);
        }

        function SafeSetupFilePond() {
          if (typeof FilePond === 'undefined') {
            setTimeout(SafeSetupFilePond, 100);
            return;
          }
          SetupFilePond();
        }

        function SetupFilePond() {

          // https://pqina.nl/filepond/docs/patterns/api/filepond-instance/
          FilePond.registerPlugin(FilePondPluginFileValidateType);
          FilePond.registerPlugin(FilePondPluginFileValidateSize);
          filePond = FilePond.create(
            document.querySelector('input.filepond'), {
            maxFileSize: '<%= ConfigHelper.ServerMaxUploadFileSizeMB %>MB',
            acceptedFileTypes: ['<%= WebHelper.GetContentTypeString(WebHelper.HttpContentType.pdf) %>'],
            fileValidateTypeLabelExpectedTypes: 'Expects PDF files',
            maxFiles: 1,
            labelIdle: 'Attach <b>1 </b>file, <b><%= ConfigHelper.ServerMaxUploadFileSizeMB %>MB max</b> per file.<br/>Drag & Drop here or <span class="filepond--label-action"> Browse </span>',
            itemInsertLocation: "after",
            labelTapToRetry: "Click to retry",
            labelTapToUndo: "",
            server: {
              url: '',
              process: {
                url: location.href,
                method: 'POST',
                withCredentials: false,
                headers: {
                  "<%= AppHelper.HttpHeaders.AjaxAction %>": "<%= AjaxAction.Upload %>"
                },
                timeout: 7000,
                onload: null,
                onerror: null,
                ondata: null
              },
              revert: {
                method: 'POST',
                headers: {
                  "<%= AppHelper.HttpHeaders.AjaxAction %>": "<%= AjaxAction.UploadRevert %>"
                }
              }
            },
            oninitfile: function (thisFile) {
              var files = filePond.getFiles();
              for (var i = 0; i < files.length; i++) {
                if (files[i].filename == thisFile.filename && files[i].id != thisFile.id) {
                  filePond.removeFile(thisFile.id);
                  common_InfoDialog("File is already in the list.");
                  break;
                }
              }
            },
            onprocessfilestart: function (file) {
              //console.log("onprocessfilestart");
            },
            onprocessfileprogress: function (file, progress) {
              var progBar = $("#filepond--item-" + file.id + " .filepond--panel-top");
              var width = Math.ceil(progress * 100);
              progBar.css("width", width + "%");
            },
            onaddfilestart: function (file) {
              //console.log("onaddfilestart");
            },
            onaddfilestart: function (file) {
              //console.log("onaddfilestart");
            },
            onaddfile: function (error, file) {
              var fileName = $("#filepond--item-" + file.id + " .filepond--file-info-main");
            },
            credits: null
          }
          );
        }

        // PDF Upload
        $pdfUpload.on('change', function (e) {
          const file = this.files[0];
          if (!file) return;

          $(".pdf-button-message").html('<small class="text-info">Uploading…</small>');

          const formData = new FormData();
          formData.append('pdf_file', file);

          jQuery.ajax({
            method: "post",
            url: "<%= PathHelper.Pages.QuoteDetails(QuoteInfo.PublicGuid, null) %>",
            data: formData,
            contentType: false,
            processData: false
          }).done(function (response) {
            $(".pdf-button-message").html('<small class="text-info">Uploaded!</small>');
          }).fail(function () {
            $(".pdf-button-message").html('<small class="text-danger">Upload failed!</small>');
          });
        });

        // Copy to Clipboard functionality
        $copyToClipboardButton.on("click", function () {
          const $btn = $(this);
          const url = $btn.data("target");
          const originalHtml = $btn.html()
          navigator.clipboard.writeText(url)
            .then(() => {

              $btn.text("Copied!")

              setTimeout(() => {
                $btn.html(originalHtml)
              }, 2000);
            }).catch(err => {
              console.error('Failed to copy:', err);
              alert('Copy failed. Please copy manually.');
            });

        });


        SetActiveTabValue();

        <% // Because of various async JS going on in the first tab, it's easier to
           // determine new quote initial focus from DTO state instead of for field values.
          if (IsNewQuote) {
            if (QuoteInfo.CompanyInfo == null || QuoteInfo.CompanyInfo.CompanyId == 0) {
              // Company is blank so focus that first.
              %>$selCompanyId.focus();<%
            } else if (QuoteInfo.ContactUserId == 0) {
              // Contact is blank so focus that first.
              %>$selContactUserId.focus();<%
            }
          }
        %>

        if (isClientView) {
          TinyMCEInit("#txtCoverLetter", { autoresize_bottom_margin: 20, min_height: 400, plugins: 'autoresize lists link image paste code' });
        } else {
          TinyMCEInit("#txtCoverLetter", { autoresize_bottom_margin: 20, min_height: 400, plugins: 'autoresize lists link image paste code', toolbar: 'undo redo | formatselect | bold italic <a href="DevelopmentPlanForm.aspx">DevelopmentPlanForm.aspx</a>underline | alignleft aligncenter alignright | bullist numlist | link  | removeformat | code' });
        }

        // Preventing default action when 'Enter' is pressed in an input, so it won't accidentally submit a form or delete something from another tab.
        // Note this is a hack fix for a strange behaviour where line 567 picks up Enter keypresses from other inputs:
        // $ProductListBody.on("click", ".btnRemoveProduct", ShowRemoveProduct);
        // Need to investigate further. Could be mangled html causing events to propagate the wrong way.
        $('.form-control').keydown(function (event) {
          if (event.keyCode === 13) {
            event.preventDefault();
          }
        });

        // When adding a new company, and the Client Lead selection changes,
        // Change the text in splits to avoid confusion.
        $('select[name="<%= FormFields.ClientLeadUserId %>"]').change(GetClientLeadInfo);

        $('#SplitsCalculation input[name="<%= FormFields.PlatformServiceIds %>"]').change(SplitCheckBoxChange);
        $('#SplitsCalculation input.inp-percent').change(CalculateSplitsTotal);
        $('select[name="<%= FormFields.ProposalDesignerUserId %>"]').change(SplitSelectChange);
        CalculateSplitsTotal();

        // Calculations of components are done in jQuery, if we don't populate the amounts the calculations won't be be correct.
        // Remove these columns for user on page load after calculations took place, if not allowed to see component price.
        if (!canViewQuoteComponentPrice) {
          $("td.clickForEdit").has(".divUnitPrice, .divLineTotal").remove();
        } else {
          // Hidden by default when created, in case the user is not supposed to see it before calculations.
          $("td.clickForEdit").has(".divUnitPrice, .divLineTotal").removeClass('hidden');
        }

        $selSalesContent.change(SalesContentChanged);
        SalesContentChanged();

        if (isLoadingPage) isLoadingPage = false; // Always keep last to allow all settings to get done on page load.

      }); // ready.

      function StartAutosaveTimer() {
        StopAutosaveTimer();
        countdownSeconds = 60; // Reset countdown to 60 seconds
        UpdateCountdownDisplay();
        countdownTimer = setInterval(UpdateCountdown, 1000);
      }

      function StopAutosaveTimer() {
        clearInterval(countdownTimer);
        autoSaveCountdownlbl.text("");
      }

      function UpdateCountdown() {
        countdownSeconds--;
        if (countdownSeconds <= 0) {
          StopAutosaveTimer();
          DoQuoteUpdate();
        }
        UpdateCountdownDisplay();
      }

      function SetupQuoteUpdate() {
        $btnUpdate.focus();
        countdownSeconds = 0;
        UpdateCountdown();
      }

      function UpdateCountdownDisplay() {
        if (countdownSeconds <= 0) {
          autoSaveCountdownlbl.text("Saving...");
        } else {
          autoSaveCountdownlbl.text("Auto-save in " + countdownSeconds + " seconds");
        }
      }

      function UpdateUrlAddress(tabName) {
        window.history.pushState('', '', '<%= PathHelper.Pages.QuoteDetails(QuoteInfo.PublicGuid, null) %>' + tabName);
      }

      function GetClientLeadInfo() {
        var selectedLeadId = $(this).val();
        var selectedLeadName = selectedLeadId ? $(this).children("option:selected").text() : 'Unassigned';

        $(".<%= DataAttrs.ClientLeadUserInfo_Class %> .user-name").removeAttr("data-*");
        $(".<%= DataAttrs.ClientLeadUserInfo_Class %> span.user-name").text(selectedLeadName);

        if (selectedLeadId) {
          var triggerPanelPath = '<%= PathHelper.Partials.PartnerSlideoutPanel(null) %>' + selectedLeadId;
          var avatarLinkHtml = $('.<%= DataAttrs.ClientLeadUserInfo_Class %> a.user-avatar-horizontal');
          avatarLinkHtml.attr('data-<%= WebHelper.DataAttrName.SlideoutPartialUrl %>', triggerPanelPath);
          avatarLinkHtml.attr('data-<%= WebHelper.DataAttrName.SlideoutTrigger %>', 'true');
          avatarLinkHtml.attr('data-<%= WebHelper.DataAttrName.SlideoutTitle %>', 'Partner Details');
          avatarLinkHtml.removeClass('nohover');
        } else {
          $('.<%= DataAttrs.ClientLeadUserInfo_Class %> a.user-avatar-horizontal').addClass('nohover');
        }
      }

      function SplitCheckBoxChange() {
        var isChecked = $(this).prop('checked');
        var value = isChecked ? $(this).data('<%= DataAttrs.Percent %>') : '0';
        var targetClass = $(this).data('<%= DataAttrs.TargetFormClass %>');
        var spanForm = $('.' + targetClass);
        spanForm.text(value);
        spanForm.attr('data-<%= DataAttrs.Percent %>', value);
        CalculateSplitsTotal();
      }

      function SplitSelectChange() {
        var selectedOption = $(this).find(':selected');
        var platFee = selectedOption.data('<%= DataAttrs.PlatFee %>') || '0';
        var spanForm = $('.<%= FormFields.ProposalDesignerUserId %>');
        spanForm.text(platFee);
        spanForm.attr('data-<%= DataAttrs.Percent %>', platFee);
        CalculateSplitsTotal();
      }

      function GetSplitTableTotal(tableName) {
        var sum = 0;
        $(tableName +' input.inp-percent').each(function (i, e) {
            sum += parseFloat(e.value);
        });
        $(tableName +' span[data-<%= DataAttrs.Percent %>]').each(function() {
          sum += parseFloat($(this).attr('data-<%= DataAttrs.Percent %>'));
        });

        $(tableName +' .<%= DataAttrs.TotalTableSum %>').text(sum.toFixed(0));

        return sum;
      }

      function CalculateSplitsTotal() {

        // Ensure this isn't re-entered because of recursive events.
        if (CalculatePlatformFee_Busy) return;
        CalculatePlatformFee_Busy = true;

        var txtDevTeamPer = $('.<%= FormFields.DeliveryPercentage %>');
        var inpTotal = $('#<%= FormFields.SplitsTotalPercentage %>');

        var totalSplitRoles = GetSplitTableTotal('#tblSplitRoles');
        var totalSplitServices = GetSplitTableTotal('#tblSplitServices');
        var totalPercentage = totalSplitRoles + totalSplitServices;
        var deliverytotal = 100 - totalPercentage;

        txtDevTeamPer.text(deliverytotal.toFixed(0));
        totalPercentage += deliverytotal;
        inpTotal.text(totalPercentage.toFixed(0));

        CalculatePlatformFee_Busy = false;
      }

      function DoCopy() {
        $inpCopyQuoteTitle.val("");
        ShowCopyDialog();
      }

      function ShowCopyDialog() {
        var dlg = common_InfoDialog("#dlgCopyQuote", {
          name: "CopyQuote",
          title: "Copy Quote",
          width: 800,
          focus: $inpCopyQuoteTitle,
          buttons: [
            { text: "Cancel", class: "btn-secondary mr20", isDefault: false, isPrimary: false, close: true },
            { text: "Create Copy", id: "btnCopyCreate", isDefault: true, isPrimary: true, close: false, click: function(ev) { CopyDialogSubmit(ev, dlg); } }
          ],
          shown: function() { },
          hide: function() { }
        });
      }

      function CopyDialogSubmit(clickEvent, dialog) {

        var $btnCopyCreate = $("#btnCopyCreate");

        AjaxSubmit({
          form: $frmCopyQuote,
          action: "<%= AjaxAction.Copy %>",
          onSuccess: function (jqXHR, data) { },
          onFail: function (jqXHR, data) { },
          onError: function(jqXHR, textStatus, errorThrown) {
            if (app_isDev) common_InfoDialog(jqXHR.responseText);
            else common_InfoDialog("Delete failed, please try again later.");
          },
          onAlways: function(data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

      function GetEditingRow() {
        return $rowCurrentlyEditing; // Get row being edited.
      }
      function SetEditingRow($row) {
        $rowCurrentlyEditing = $row; // Remember row being edited.
      }
      function SetProductDialogValues(rowInfo) {
        if (!rowInfo) rowInfo = new ProductRowInfo();
        $selDlgProducts.val(rowInfo.IsNote ? "<%= ProductOptionValueForNote %>" : (rowInfo.ProductId || "")).trigger("change");
        $inpDlgItemDescription.val(rowInfo.ProductName || "");
        $inpDlgOptional.val(rowInfo.IsNote ? "" : rowInfo.IsOptional).trigger("change");
        $inpDlgUnitPrice.val(rowInfo.IsNote ? "" : (rowInfo.UnitPrice || "")).trigger("change");
        $inpDlgQuantity.val(rowInfo.IsNote ? "" : (rowInfo.Quantity || ""));
        $inpDlgQtyDescr.val("" + rowInfo.QtyDescr);
      }
      function EnableDlgInputs(enable) {
        enable = isBool(enable) ? enable : true;
        $inpDlgUnitPrice.val("").prop("disabled", !enable);
        $inpDlgQuantity.val("").prop("disabled", !enable);
        if ($inpDlgQuantity.val() == '') {
            $inpDlgQuantity.val(1);
        }
        $inpDlgQtyDescr.val("").prop("disabled", !enable);
        $inpDlgOptional.val(0).prop("disabled", !enable).trigger("change");
      }
      function SetProductDialogSelectedProductValues() {
        // When a product is selected, fill in certain values.
        var $selectedOption = $selDlgProducts.find("option:selected");

        if ($selectedOption.length != 1) return;
        if ($selectedOption.prop("value") == "<%= ProductOptionValueForNote %>") {
          EnableDlgInputs(false);
        } else {
          EnableDlgInputs(true);
          if ($inpDlgItemDescription.val() == "" && $selectedOption.data("description")) $inpDlgItemDescription.val($selectedOption.data("description")).change();
          $inpDlgUnitPrice.val($selectedOption.data("defaultprice")).trigger("change");

          // If the product is fixed description, disable the description editor.
          var isFixedDescription = $selectedOption.data("isfixeddescription");
          var descriptionEditor = $('.fixeddescription .tox-editor-container');
          if (isFixedDescription) {
            descriptionEditor.addClass('disabled');
          } else {
            descriptionEditor.removeClass('disabled');
          }

          // Display div that indicates product requires sub if applies
          var requiresSubscription = $selectedOption.data("<%= DataAttrs.RequiresSubscription %>");
          $(".productRequiresSubscription").toggleClass("displaynone", !requiresSubscription);
        }
      }



      function ShowProductDialog(name, title, buttonText, fnOnShown) {

        IsComponentsModalOpen = true;
        StopAutosaveTimer(); // Prevent timer to work while Product modal is open

        var dlg = common_InfoDialog("#dlgAddProduct", {
          name: name,
          title: title,
          width: 850,
          focus: $selDlgProducts,
          buttons: [
            { text: "Cancel", class: "btn-secondary mr20 left", isDefault: false, isPrimary: false, close: true },
            { text: buttonText, class:"btnAddProduct", isDefault: true, isPrimary: true, close: false, click: function(ev) { ProductDialogSubmit(ev, dlg); } }
          ],
          shown: function() {
            TinyMCEInit("#inpDlgItemDescription", { autoresize_min_height: 400, plugins: 'autoresize lists link image paste code', toolbar: 'undo redo | formatselect | bold italic underline | alignleft aligncenter alignright | bullist numlist | link  | removeformat | code' });
            if (isFunction(fnOnShown)) fnOnShown();
          },
          hide: function () {
            var mce = $inpDlgItemDescription.data("editor");
            if (mce != null) {
              var description = mce.getContent();
              mce.remove();
              mce.destroy();
              $inpDlgItemDescription.val(description);
              StartAutosaveTimer();
            }
            IsComponentsModalOpen = false;
          }
        });
      }

      function ShowEditProduct(ev) {
        ev.preventDefault();

        // Show the products with "hidden flag when attempting to edit.
        ShowOrHideProductsInSelect(true);

        var $row = $(ev.target);
        if (!$row.hasClass("product-row")) $row = $row.closest(".product-row");
        if ($row.length != 1) return;

        rowInfo = new ProductRowInfo().GetFromRow($row);
        SetEditingRow($row);
        ShowProductDialog("editProduct", "Update Component", "Update", () => SetProductDialogValues(rowInfo));
      }

      function ShowAddProduct(ev) {
        ev.preventDefault();
        // Hide the products with "hidden flag when attempting to add a new one.
        ShowOrHideProductsInSelect(false);
        SetEditingRow(null);
        ShowProductDialog("addProduct", "Add Component", "Add Component", () => SetProductDialogValues(new ProductRowInfo()));
      }

      function AutomaticallyAddSubscriptionProduct(productId, quantity) {
        SetEditingRow(null);
        SetProductDialogValues(new ProductRowInfo());

        // Pre-select the default SubscriptionId
        $selDlgProducts.val(productId).trigger('change');
        SetProductDialogSelectedProductValues();
        // Set the quantity of subscriptions
        $('#inpQuantity').val(quantity);
        // Submit product.
        ProductDialogSubmit(null, null, true);
      }

      function ShowOrHideProductsInSelect(isEditing) {
        $selDlgProducts.select2({
          templateResult: function (result) {
            if (result.element && $(result.element).data('ishidden')) {
              // If it's editing but has the hidden flag, then show it. Otherwise, hide it.
              if (isEditing) {
                return result.text;
              } else {
                return null;
              }
            } else {
              return result.text;
            }
          }
        });
      }

      function ShowRemoveProduct(ev) {
        ev.preventDefault();

        var $btn = $(ev.target);
        var $row = $btn.closest(".product-row");
        if ($row.length != 1) return;

        let rowInfo = new ProductRowInfo().GetFromRow($row);

        $row.addClass("remove-confirm");
        var confirmRemove = confirm("Remove this item?");
        $row.removeClass("remove-confirm");

        if (confirmRemove) {
          if (rowInfo.IsSubscription === true) {
            hasPreviouslyRemovedSubscription = true;
          }
          $row.remove();
          SetQuoteTotal();
          GetProductsWarningMsgs();
          SetupQuoteUpdate(); // Update Quote on component removal.
        }
      }

      function ProductDialogSubmit(clickEvent, dialog, isAutoAddingSubscription = false) {
        var productInfo = new ProductRowInfo().GetFromDialog();

        productInfo.ProductName = ("" + productInfo.ProductName).trim();
        if (productInfo.ProductName == "") {
          common_InfoDialog("Please provide the Description.", function(){ $inpDlgItemDescription.focus(); });
          return;
        }
        if (!productInfo.IsNote) {
          if (!isNumber(productInfo.ProductId)) {
            common_InfoDialog("Please select a Product or Note.", function () { $selDlgProducts.focus(); });
            return;
          }
          if (!isNumber(productInfo.UnitPrice)) {
            common_InfoDialog("Please enter a valid Unit Price.", function () { $inpDlgUnitPrice.focus(); });
            return;
          }
          if (!isNumber(productInfo.Quantity)) {
            common_InfoDialog("Please enter a valid Quantity.", function () { $inpDlgQuantity.focus(); });
            return;
          }
          if (isNumber(productInfo.MinAllowedQuotePrice) && productInfo.UnitPrice < productInfo.MinAllowedQuotePrice) {
            common_InfoDialog("Minimum price for this product is <b>$" + productInfo.MinAllowedQuotePrice.toString() + ".", function () { $inpDlgQuantity.focus(); });
            return;
          }
          if (productInfo.Quantity <= 0) {
            common_InfoDialog("Please enter Quantity above zero.", function () { $inpDlgQuantity.focus(); });
            return;
          }
          if (productInfo.Quantity.toString().indexOf(".") >= 0) {
            common_InfoDialog("Only whole numbers allowed for Quantity.", function () { $inpDlgQuantity.focus(); });
            return;
          }
        }

        if (dialog) {
          dialog.hide();
        }

        if (isAutoAddingSubscription) AddProductRow(productInfo);
        else if (dialog.options.name == "addProduct") AddProductRow(productInfo);
        else if (dialog.options.name == "editProduct") UpdateProductRow(GetEditingRow(), productInfo);

        SetupQuoteUpdate(); // Update Quote on component edition.
      }

      function CreateHidden(classes, lineNum, nameKey, value) {
        return $('<input class="' + classes + '" type="hidden" name="<%= FormFields.ProdKey_Prefix %>' + lineNum + nameKey + '" />').val(value);
      }
      function CreateLabelValue(classes, labelText, valueText) {
        var $div = $('<div class="' + classes + '"></div>');
        if (isString(labelText) && labelText != "") $div.append($('<label />').text(labelText));
        $div.append($('<span class="valueBox" />').text(valueText));
        return $div;
      }

      function AddProductRow(rowInfo) { // class ProductRowInfo

        // Add to list of products.

        // Find the highest prod number in the list.
        var newProdNum = 1;
        $ProductListBody.find(".product-row").each(function(i, e) {
          var prodNum = $(e).data("prodnum");
          if (prodNum >= newProdNum) newProdNum = prodNum + 1;
        });

        var row = $('<div class="product-row"></div>');
        row.data("prodnum", newProdNum);
        if (rowInfo.isAccepted === false) row.addClass("not-accepted");
        var table = $('<table width="100%" class="table"></table>');
        var tr = $('<tr></tr>');
        row.append(table.append(tr));

        var td = $('<td class="w50 dragHandle"><%= CanEditQuote ? WebHelper.Icon.DraggableRow.HTML : "" %></td>');
        tr.append(td);

        td = $('<td class="clickForEdit"></td>');
        td.append(CreateHidden("hidIsNote", newProdNum, "<%= FormFields.ProdKey_IsNote %>", ""));
        td.append(CreateHidden("hidProductId", newProdNum, "<%= FormFields.ProdKey_Id %>", ""));
        td.append(CreateHidden("hidProductName", newProdNum, "<%= FormFields.ProdKey_Name %>", ""));
        td.append(CreateHidden("hidOptional", newProdNum, "<%= FormFields.ProdKey_Optional %>", ""));
        td.append(CreateHidden("hidUnitPrice", newProdNum, "<%= FormFields.ProdKey_Price %>", ""));
        td.append(CreateHidden("hidMinAllowedQuotePrice", newProdNum, "<%= FormFields.ProdKey_MinAllowedQuotePrice %>"))
        td.append(CreateHidden("hidQuantity", newProdNum, "<%= FormFields.ProdKey_Qty %>", ""));
        td.append(CreateHidden("hidQtyDescr", newProdNum, "<%= FormFields.ProdKey_QtyDescr %>", ""));
        td.append(CreateHidden("hidRequiresSubscription", newProdNum, "<%= FormFields.ProdKey_RequiresSubscription %>", ""));
        td.append(CreateHidden("hidDefaultSubscriptionId", newProdNum, "<%= FormFields.ProdKey_DefaultSubscriptionId %>", ""));
        td.append(CreateHidden("hidIsQuantityPerPerson", newProdNum, "<%= FormFields.ProdKey_IsQuantityPerPerson %>", ""));
        td.append(CreateHidden("hidIsSubscription", newProdNum, "<%= FormFields.ProdKey_IsSubscription %>", ""));
        td.append($('<div class="divProductName" />'));
        tr.append(td);

        td = $('<td class="clickForEdit w100 align-center"></td>');
        td.append(CreateLabelValue("divOptional", "", ""));
        tr.append(td);

        td = $('<td class="clickForEdit w100 pr10"></td>').addClass('<%= CanViewQuoteComponentPrice ? "" : "hidden" %>');
        td.append(CreateLabelValue("divUnitPrice", "", ""));
        tr.append(td);

        td = $('<td class="clickForEdit w125 align-center"></td>');
        td.append(CreateLabelValue("divQuantity", "", ""));
        tr.append(td);

        td = $('<td class="clickForEdit w100"></td>').addClass('<%= CanViewQuoteComponentPrice ? "" : "hidden" %>');
        td.append(CreateLabelValue("divLineTotal", "", ""));
        tr.append(td);

        td = $(
          '<td width="50">' +
            (isReadOnly ? "" : '<button class="btnRemoveProduct"><%= WebHelper.Icon.Trash %></button>') +
          '</td>');
        tr.append(td);

        $ProductListBody.append(row);
        UpdateProductRow(row, rowInfo)
        SetQuoteTotal();
      }

      function UpdateProductRow($row, rowInfo) {
        if (!isJQuery($row) || $row.length != 1 || !$row.hasClass("product-row")) return null;

        // If product requires subscription, show tooltip indicating that.
        var subscriptionTooltipHtml = '<%= WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, $"{ProductRequiresSubscriptionMsg}", "", "mr10").Replace("\r\n", "").Replace("\n", "").Replace("\r", "") %>';
        var productTooltip = rowInfo.RequiresSubscription ? subscriptionTooltipHtml : "";
        var productName = rowInfo.ProductName;
        if (productTooltip != "") {
          productName = '<div class="flex"><div>' + rowInfo.ProductName + '</div>' + productTooltip + '</div>';
        }

        $row.find(".hidIsNote").val(rowInfo.IsNote ? "true" : "false");
        $row.find(".hidProductId").val(rowInfo.ProductId);
        $row.find(".hidProductName").val(rowInfo.ProductName);
        $row.find(".hidOptional").val(rowInfo.IsOptional);
        $row.find(".hidUnitPrice").val(rowInfo.UnitPrice);
        $row.find(".hidMinAllowedQuotePrice").val(rowInfo.MinAllowedQuotePrice);
        $row.find(".hidQuantity").val(rowInfo.Quantity);
        $row.find(".hidQtyDescr").val(rowInfo.QtyDescr);
        $row.find(".divProductName").html(productName);
        $row.find(".divOptional .valueBox").text(GetItemOptionalText(rowInfo.IsOptional));
        $row.find(".divUnitPrice .valueBox").text(rowInfo.IsNote ? "" : CurrencyFormatter.format(rowInfo.UnitPrice));
        $row.find(".divQuantity .valueBox").text(rowInfo.IsNote ? "" : (rowInfo.Quantity + " " + rowInfo.QtyDescr));
        $row.find(".divLineTotal .valueBox").text(rowInfo.IsNote ? "" : CurrencyFormatter.format(rowInfo.UnitPrice * rowInfo.Quantity));
        $row.find(".hidRequiresSubscription").val(rowInfo.RequiresSubscription);
        $row.find(".hidDefaultSubscriptionId").val(rowInfo.DefaultSubscriptionId);
        $row.find(".hidIsQuantityPerPerson").val(rowInfo.IsQuantityPerPerson);
        $row.find(".hidIsSubscription").val(rowInfo.IsSubscription);
        SetQuoteTotal();

        if (productTooltip != "") {
          common_UpdateUI($row);
        }

        if (!isLoadingPage) {
          // Do not get messages for each product when page is loading, this function is called on page load to avoid blinking behavior.
          GetProductsWarningMsgs();
        }
      }

      function GetItemOptionalText(optionalId) {
        <%
          foreach (var option in DbHelper.AbleQuotes.OptionalEnum.Options) {
            Response.Write("if (optionalId === " + option.Id + ") return \"" + option.Text.HTMLEncode() + "\";\n        ");
          }
        %>
        return "";
      }

      function SetQuoteTotal() {
        var total = 0;
        $(".product-row .divLineTotal .valueBox").each(function(i, e) {
          total += (parseFloat($(e).text().replace(/[^0-9.-]/g, "")) || 0);
        });
        $("#GrandTotalAmount").text(CurrencyFormatter.format(total));
      }

      // Ajax call to get "warning messages" depending on selected products.
      function GetProductsWarningMsgs() {

        AjaxSubmit({
          form: $clientForm,
          action: "<%= AjaxAction.GetProdWarningMgs %>",
          onSuccess: function (jqXHR, data) {
            var warnMsgs = data["<%= AjaxReturnData.ProdWarningMsgs %>"];
            if (!warnMsgs) {
              divWarningMessages.fadeOut("fast");
            } else {
              divWarningMessages.hide().html(warnMsgs).fadeIn("fast");
            }
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            divWarningMessages.fadeOut("fast");
          }
        });
      }

      function GetTabLink(tabName) {
        return $("#tab-" + tabName);
      }
      function GetNextTabLink() {
        return GetActiveTab().next().find("a.nav-link");
      }
      function ShowTab(tabName) {
        var tab = GetTabLink(tabName);
        if (tab && tab.length && tab.length == 1) tab.tab("show");
      }
      function ShowNextTab(tabName) {
        GetNextTabLink().tab("show");
      }
      function GetActiveTab() {
        return $formTabs.children(".active");
      }
      function GetActiveTabName() {
        return GetActiveTab().data("tabname");
      }
      function GetActiveTabPanel() {
        return $("#panel-" + GetActiveTabName());
      }
      function SetActiveTabValue() {
        var tabName = GetActiveTabName();
        $activeTabName.val("" + tabName);
        return tabName;
      }

      function ChangeUser(ev) {
        var $selUser = $(ev.target);
        var userId = $selUser.val();
        if (userId == "<%= PathHelper.AbleUrlValues.IdNew %>") {
          $newContactInfo.slideDown(300, SetNewContactFocus);
        } else {
          $newContactInfo.slideUp(300);
        }
      }
      function SetNewContactFocus() {
        $newContactInfo.find("input:text").eq(0).focus();
      }

      // Set company related info and gets list of existing projects.
      function ChangeCompany(ev) {
        var companyId = $selCompanyId.val();
        if (companyId == "<%= PathHelper.AbleUrlValues.IdNew %>") {
          $newCompanyInfo.slideDown(300, SetNewCompanyFocus);
          $selProjectJobNumber.val("<%= PathHelper.AbleUrlValues.IdNew %>").trigger("change");
        } else {
          $newCompanyInfo.slideUp(300);
        }
        companyId = parseInt(companyId, 10) || 0; // Will be zero if new company.
        GetIsExistingClient(companyId); // Update isExistingClient - will be false for new company.
        GetProjects(companyId); // Update projects list - will be blank for new company.
      }

      // Ajax call to get "existing client" status of selected Company.
      function GetIsExistingClient(companyId) {
        AjaxSubmit({
          url: document.location.href,
          action: "<%= AjaxAction.GetIsExistingClient %>",
          data: {
            "<%= FormFields.CompanyId %>": companyId
          },
          onSuccess: function (jqXHR, data) {
            isExistingClient = data.IsExistingClient === true;
          },
        });
      }

      function SetNewCompanyFocus() {
        $newCompanyInfo.find("input:text").focus();
      }

      function GetProjects(cid) {
        var selectedValue = $selProjectJobNumber.val() || "<%= QuoteInfo?.JobNumber %>";
        var foundSelected = false;
        $selProjectJobNumber.find('option:not([value=""],[value="<%= PathHelper.AbleUrlValues.IdNew %>"])').remove();
        $.get("<%= PathHelper.Endpoints.ProjectsForCompanyId(null) %>" + cid, function(data) {
          if (data && data.Projects && data.Projects.InfoList){
            for(var i_project in data.Projects.InfoList) {
              var project = data.Projects.InfoList[i_project];
              var $option = $("<option/>", {
                value: project.JobNumber,
                text: project.JobNumber + ": " + project.ProjectName,
              })
              .data("jobnumber", project.JobNumber);
              if (selectedValue && $option.prop("value") === selectedValue) {
                $option.prop("selected", true);
                foundSelected = true;
              }
              $selProjectJobNumber.append($option);
            }
            if (!foundSelected) setTimeout(function(){ $selProjectJobNumber.focus().select2("open");}, 200);
          } else if ($selCompanyId.val() != "") {
            $selProjectJobNumber.find('option[value="<%= PathHelper.AbleUrlValues.IdNew %>"]').prop("selected", true);
          }
          $selProjectJobNumber.trigger("change");
        }, "json");
      }

      function ChangeProject(ev) {
        if ($selProjectJobNumber.val() != "" && $selCompanyId.val() == "") {
          if (ev) ev.preventDefault();
          common_InfoDialog("Please select a company (or new company) first.", function(){ $selProjectJobNumber.val("").trigger("change"); });
          return;
        }
        var pid = $selProjectJobNumber.val();
        if (pid != "<%= PathHelper.AbleUrlValues.IdNew %>") {
          $newProjectInfo.slideUp(300);
        } else {
          $newProjectInfo.slideDown(300);
        }
      }

      function DisableProjectFields() {
        $("#project_noedit").find('input:not([name="<%= FormFields.TBAJobNumber %>"])').prop("readonly", true);
      }
      function EnableProjectFields() {
        $("#project_noedit").find('input:not([name="<%= FormFields.TBAJobNumber %>"])').prop("readonly", false);
      }

      function BackClicked(ev) {
        var $btnBack = $(ev.target);
        var $activeTabPane = $btnBack.closest(".tab-pane");
        if ($activeTabPane.length != 1) return;
        var $activeTabLink = $("#" + $activeTabPane.attr("aria-labelledby"));
        if ($activeTabLink.length != 1) return;
        var $previousTabLink = $activeTabLink.closest("li").prev().find("a.nav-link");
        if ($previousTabLink.length != 1) return;
        $previousTabLink.tab("show");
      }

      function ContinueClicked($btnContinue) {

        if (isNewQuote) return;

        var tabName = SetActiveTabValue();
        var $activeTabPane = $btnContinue.closest(".tab-pane");
        if ($activeTabPane.length != 1) return;
        var $activeTabLink = $("#" + $activeTabPane.attr("aria-labelledby"));
        if ($activeTabLink.length != 1) return;
        var $nextTabLink = GetNextTabLink();
        if ($nextTabLink.length != 1) return;
        $nextTabLink.tab("show");
        return;
        // Do this later (submit for each tab change)..
        if (tabName == "<%= PathHelper.QuoteTabEnum.project %>") SubmitTab_Project($btnContinue, $activeTabPane, ShowNextTab);
        else if (tabName == "<%= PathHelper.QuoteTabEnum.settings %>") SubmitTab_Details($btnContinue, $activeTabPane, ShowNextTab);
        else if (tabName == "<%= PathHelper.QuoteTabEnum.splits %>") SubmitTab_Splits($btnContinue, $activeTabPane, ShowNextTab);
        else if (tabName == "<%= PathHelper.QuoteTabEnum.components %>") SubmitTab_Components($btnContinue, $activeTabPane);
      }

      function SubmitTab_Project($btnContinue, $activeTabPane, onSuccess) {

        AjaxSubmit({
          form: $clientForm,
          onSuccess: function (jqXHR, returnData) {
            if (onSuccess) onSuccess();
          },
          onFail: function() {
            common_InfoDialog("Update failed, please try again later.");
          },
          onAlways: function() { }
        });

      }

      function DoDelete() {
        if (!confirm("Delete this Quote?")) return;
        AjaxSubmit({
          form: $clientForm,
          action: "<%= AjaxAction.Delete %>",
          onSuccess: function (jqXHR, data) { },
          onFail: function (jqXHR, data) { },
          onError: function(jqXHR, textStatus, errorThrown) {
            if (app_isDev) common_InfoDialog(jqXHR.responseText);
            else common_InfoDialog("Delete failed, please try again later.");
          },
          onAlways: function(data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
        });
      }

      function DoQuoteUpdate() {

        if (!allowQuoteUpdate) return;
        if ($btnUpdate.data('busy')) return; // Block quote update if has process on-going

        $btnUpdate.data('busy', true);

        var isMissingSub, requiredSubscription_ProductId, requiredSubscription_ProductId_Quantity;

        AjaxSubmit({
          form: $clientForm,
          action: GetAjaxAction(),
          onSuccess: function (jqXHR, data) {
            if (currentTabName == componentTabName) {
              isMissingSub = data["<%= AjaxReturnData.QuoteItems_MissingSubscription %>"] === "true";
              requiredSubscription_ProductId = data["<%= AjaxReturnData.QuoteItems_RequiredSubscription_ProductId %>"];
              requiredSubscription_ProductId_Quantity = data["<%= AjaxReturnData.QuoteItems_RequiredSubscription_ProductId_Quantity %>"];
            }
          },
          onFail: function(jqXHR, data) {
            if (data && data["<%= RtnShowTabKey %>"]) {
              ShowTab(data["<%= RtnShowTabKey %>"]);
            }
          },
          onError: function (jqXHR, textStatus, errorThrown) {
            common_InfoDialog("Update failed, please try again later.");
          },
          onAlways: function () {
            autoSaveCountdownlbl.text("");
            $btnUpdate.data('busy', false);
            if (isMissingSub && requiredSubscription_ProductId && !hasPreviouslyRemovedSubscription) {
              AutomaticallyAddSubscriptionProduct(requiredSubscription_ProductId, requiredSubscription_ProductId_Quantity);
            } else if (!isUpdatingAcceptedQuote && !isNewQuote && currentTabName != componentTabName) {
              // Only start counter when: IsNewQuote is false, is not updating a signed quote, the current tab is not components.
              StartAutosaveTimer();
            }
          }
        });
      }

      function GetAjaxAction() {

        if (isUpdatingAcceptedQuote) {
          return "<%= AjaxAction.UpdateAcceptedQuote %>";
        }

        // Get active tab
        var $activeA =
          $('#formTabs a.nav-link.active').first().length
          ? $('#formTabs a.nav-link.active').first()
          : $('#formTabs li.active > a.nav-link').first();

        if ($activeA.length === 0) {
          $activeA = $('#formTabs a.nav-link[aria-selected="true"]').first();
        }

        var tabName = ($activeA.data('tabname') || '').toString().toLowerCase();

        switch (tabName) {

          case '<%= PathHelper.QuoteTabEnum.project.ToString() %>':
            return "<%= AjaxAction.Update_Project_Tab %>";

          case '<%= PathHelper.QuoteTabEnum.settings.ToString() %>':
            return "<%= AjaxAction.Update_Settings_Tab %>";

          case '<%= PathHelper.QuoteTabEnum.splits.ToString() %>':
            return "<%= AjaxAction.Update_Splits_Tab %>";

          case '<%= PathHelper.QuoteTabEnum.components.ToString() %>':
            return "<%= AjaxAction.Update_Components_Tab %>";

          case '<%= PathHelper.QuoteTabEnum.coverLetter.ToString().ToLower() %>':
            return "<%= AjaxAction.Update_CoverLetter_Tab %>";

          case '<%= PathHelper.QuoteTabEnum.info.ToString() %>':
            return "<%= AjaxAction.Update_Info_Tab %>";

          default:
            return "";
        }
      }

      function SalesContentChanged(evt) {

        var selectedSalesContentTypeId = $selSalesContent.val();

        $(".sales-content-inputs").slideUp();
        $("#sales-content-" + selectedSalesContentTypeId).slideDown();
      }

    })(jQuery);
  </script>

</asp:Content>
