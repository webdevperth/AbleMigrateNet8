using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Text;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Page_Partials {

  public class AddParticipant_QuickAdd : AppCode.PageBaseClasses.LoggedInPageModel {

    public bool CanAddParticipant;
    public DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo { get; set; }
    public List<DbHelper.ClientCompanies.BriefCompanyInfo> CompanyList;
    public List<DbHelper.Products.ProductInfo> ProductList;
    public List<DbHelper.Subscriptions.SubscriptionInfo> AllSubscriptionsList = null;

    public class FormFields {
      public const string CompanyId = "CompanyId";
      public const string CompanyName = "CompanyName";
      public const string TBAJobNumber = "TBAJobNumber";
      public const string ProjectJobNumber = "ProjectJobNumber";
      public const string ProjectName = "ProjectName";
      public const string ProgramJobId = "ProgramJobId";
      public const string FirstName = "FirstName";
      public const string LastName = "LastName";
      public const string EmailAddress = "EmailAddress";
      public const string Subscription_ProductId = "Subscription_ProductId";
      public const string SessionsAllocated = "SessionsAllocated";
      public const string CoachingType = "CoachingType";
      public const string EnableNudges = "EnableNudges";
      public const string EnableAICoaching = "EnableAICoaching";
      public const string EnablePulse = "EnablePulse";
      public const string SendWelcomeNow = "SendWelcomeNow";
      public const string WelcomeEmailDate = "WelcomeEmailDate";
      public const string SendMeetCoachNow = "SendMeetCoachNow";
      public const string MeetCoachEmailDate = "MeetCoachEmailDate";
      public const string CreateANewSubscription = "CreateANewSubscription";
      public const string HasActiveSubscription = "HasActiveSubscription";
      public const string ActiveSubscriptionId = "ActiveSubscriptionId";
    }

    public class FormValues {
      public bool IsNewCompany;
      public int CompanyId;
      public string CompanyName;
      public int CompanyOrgId;
      public int? ClientLeadUserId;
      public bool IsNewProject;
      public string ProjectJobNumber;
      public string ProjectName;
      public bool IsNewProgram;
      public int ProgramJobId;
      public string FirstName;
      public string LastName;
      public string EmailAddress;
      public DbHelper.Products.ProductInfo Subscription_ProductInfo;
      public DbHelper.AbleQuotes.QuoteItemForSubscription QuoteItemForSubscription;
      public int SessionsAllocated;
      public int? CoachingTypeId;
      public string CoachingTypeName;
      public bool EnableNudges;
      public bool EnableAICoaching;
      public bool EnablePulse;
      public DbHelper.Subscriptions.SubscriptionInfo SubscriptionInfo;
      public bool SendWelcomeNow;
      public DateTime? WelcomeEmailUtc;
      public DateTime? MeetCoachEmailUtc;
      public bool SendMeetCoachNow;
      public bool CreateANewSubscription;
      public bool HasActiveSubscription;
      public int QuoteId;
      public string QuoteTitle;
    }

    public class AjaxAction {
      public const string AddParticipant = "AddParticipant";
      public const string GetIsExistingClient = "GetIsExistingClient";
      public const string GetProgramsInProject = "GetProgramsInProject";
      public const string GetSubscriptionSettings = "GetSubscriptionSettings";
      public const string SearchUsersActiveSubscription = "SearchUsersActiveSubscription";
    }

    public class AjaxReturnData {
      public const string ProgramsInProjectHtml = "ProgramsInProjectHtml";
      public const string StepTabWithBadFields = "StepTabWithBadFields";
      public const string SubscriptionSettingsHtml = "SubscriptionSettingsHtml";
      public const string ActiveSubscriptionHtml = "ActiveSubscriptionHtml";
      public const string ActiveProductId = "ActiveProductId";
    }

    public class DataNames {
      public const string Durations = "durations";
    }

    public class DataAttr {
      public const string DeliveryPercentage = "DeliveryPercentage";
      public const string ActiveSubscriptionId = "ActiveSubscriptionId";
    }

    public class FormClasses {
      public const string ActiveSubscription = "ActiveSubscription";
      public const string SubscriptionSettings = "SubscriptionSettings";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Add Participant";

      ProjectInfo = null;
      ProgramInfo = null;
      CoacheeInfo = new DbHelper.AlbertCoachees.AlbertCoacheeInfo();

      CompanyList = DbHelper.ClientCompanies.GetCompanyList(SessionHelper.GetUserInfoOrNull());
      ProductList = DbHelper.Products.GetAllProducts();
      AllSubscriptionsList = DbHelper.Subscriptions.GetAllSubscriptions();

      // Get Participants in the company
      CanAddParticipant = SessionHelper.AppAccess.Participants.CanAdd();

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.AddParticipant:
              if (!CanAddParticipant) {
                ajax.AddDialogMessage("Operation not allowed.");
                return;
              }
              AddParticipant(ajax);
              return;

            case AjaxAction.GetProgramsInProject:
              GetProgramsInProject(ajax);
              return;

            case AjaxAction.GetSubscriptionSettings:
              GetSubscriptionSettings(ajax);
              return;

            case AjaxAction.SearchUsersActiveSubscription:
              SearchUsersActiveSubscription(ajax);
              return;
          }
        });
        return new EmptyResult();
      }

      return Page();
    }

    private void AddParticipant(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();

      if (!GetFormValues_InfoTab(ajax, formValues) || ajax.BadFieldCount > 0) {
        ajax.AddReturnValue(AjaxReturnData.StepTabWithBadFields, WebHelper.PurchaseServices.StepPanelType.ParticipantInformation.ToString());
        return;
      }

      if (!GetFormValues_Subscription(ajax, formValues) || ajax.BadFieldCount > 0) {
        ajax.AddReturnValue(AjaxReturnData.StepTabWithBadFields, WebHelper.PurchaseServices.StepPanelType.Subscription.ToString());
        return;
      }

      if (!GetFormValues_Coaching(ajax, formValues) || ajax.BadFieldCount > 0) {
        ajax.AddReturnValue(AjaxReturnData.StepTabWithBadFields, WebHelper.PurchaseServices.StepPanelType.Coaching.ToString());
        return;
      }

      if (!GetFormValues_Settings(ajax, formValues) || ajax.BadFieldCount > 0) {
        ajax.AddReturnValue(AjaxReturnData.StepTabWithBadFields, WebHelper.PurchaseServices.StepPanelType.ParticipantSettings.ToString());
        return;
      }

      if (ajax.HasErrors) return;

      bool committed = DbHelper.Common.UsingTransaction(trans => {

        if (formValues.IsNewCompany) {
          CreateCompany(trans, formValues);
          formValues.IsNewProject = true;
        }

        if (formValues.IsNewProject) {
          CreateProjectAndProgram(trans, formValues);
          if (formValues.ProjectJobNumber.IsNullOrEmpty() || formValues.ProgramJobId == 0) {
            ajax.AddBadField(FormFields.ProjectName, "Couldn't create project");
            return false;
          }
          formValues.IsNewProgram = true;
        }

        CreateParticipant(trans, formValues);

        // Create a quote if participant doesn't have a subscription, or will have coaching.
        if (formValues.Subscription_ProductInfo != null || formValues.CoachingTypeName != DbHelper.AlbertCoachingTypes.GetIntercomValue_NoCoaching()) {
          CreateQuote(trans, formValues);
        }

        CreateParticipantSettings(trans, formValues);

        return true;
      });

      if (committed) {

        // Reload CoacheeInfo to get the UserGuid which is needed for Intercom events
        CoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(CoacheeInfo.CoacheeId);

        var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(formValues.CompanyId, userInfo);
        var participantExternalId = ConfigHelper.UserRole.Leader.ToExternalUserId(CoacheeInfo.UserGuid);
        var programInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(formValues.ProgramJobId, DbHelper.AblePrograms.WhereRelatedUserIs.Tenant_AnyRelated, SessionHelper.UserInfo);

        // Send Intercom event for project creation
        if (formValues.IsNewProject) {
          var projectInfo = DbHelper.Projects.GetProjectInfoOrNull(formValues.ProjectJobNumber);
          if (projectInfo != null) {
            SendEvent(
              intercom => intercom.ProjectCreated()
                .FromSession()
                .WithProject(projectInfo.ProjectId, formValues.ProjectName)
                .WithProjectJobNumber(formValues.ProjectJobNumber)
                .WithCompany(formValues.CompanyId, companyInfo?.CompanyName),
              operationName: "AddParticipant_ProjectCreated",
              requestRawUrl: SystemWeb.RequestRawUrl,
              telemetryProperties: new Dictionary<string, object> {
                ["ProjectJobNumber"] = formValues.ProjectJobNumber
              }
            );
          }
        }

        // Send Intercom event for quote creation
        if (formValues.QuoteId > 0) {
          SendEvent(
            intercom => intercom.QuoteCreated()
              .FromSession()
              .WithQuote(formValues.QuoteId, formValues.QuoteTitle)
              .WithClientCompany(formValues.CompanyId, companyInfo?.CompanyName),
            operationName: "AddParticipant_QuoteCreated",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["QuoteId"] = formValues.QuoteId
            }
          );
        }

        // Send Intercom event for participant creation
        if (participantExternalId.HasValue) {
          SendEvent(
            intercom => intercom.ParticipantCreated()
              .FromSession()
              .WithParticipant(participantExternalId.Value, CoacheeInfo.EmailAddress)
              .WithProgram(programInfo?.ProgramJobId, programInfo?.ProgramJobName)
              .WithCompany(formValues.CompanyId, companyInfo?.CompanyName)
              .WithParticipantName(CoacheeInfo.GetFullName()),
            operationName: "AddParticipant_ParticipantCreated",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["ParticipantEmail"] = CoacheeInfo?.EmailAddress
            }
          );

          // Send Intercom event for subscription assignment
          if (formValues.QuoteItemForSubscription != null && CoacheeInfo.UserSubscription != null) {
            SendEvent(
              intercom => {
                var builder = intercom.SubscriptionAssigned()
                  .FromSession()
                  .WithParticipant(participantExternalId.Value, CoacheeInfo.EmailAddress)
                  .WithOrganisation(CoacheeInfo.TenantOrgId, CoacheeInfo.OrgName);

                if (CoacheeInfo.ProgramJobId.HasValue) {
                  builder.WithProject(CoacheeInfo.ProgramJobId.Value, programInfo?.ProgramJobName);
                }

                return builder.WithSubscriptionDetails(
                  subscriptionType: formValues.QuoteItemForSubscription.ProductTitle ?? "Unknown",
                  unitPrice: formValues.QuoteItemForSubscription.UnitPrice
                );
              },
              operationName: "AddParticipant_SubscriptionAssigned",
              requestRawUrl: SystemWeb.RequestRawUrl,
              telemetryProperties: new Dictionary<string, object> {
                ["ParticipantEmail"] = CoacheeInfo?.EmailAddress,
                ["SubscriptionType"] = formValues.QuoteItemForSubscription.ProductTitle
              }
            );
          }
        }

        string msg = "Participant successfully added";

        if (formValues.SendWelcomeNow || formValues.SendMeetCoachNow) {

          ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(formValues.ProjectJobNumber);
          if (ProjectInfo != null) {

            var coachInfo = DbHelper.AlbertCoaches.GetCoachInfo(userInfo.UserId);

            if (formValues.SendWelcomeNow) {
              bool welcomeEmailSent = AlbertEmails.ParticipantWelcome.Send(ProjectInfo, CoacheeInfo, coachInfo, ProjectInfo, out EmailHelper.MandrillSentResult sendResult, AlbertEmails.ParticipantWelcome.SetSendDates.Yes);
              msg += $"<br/>Welcome Email {(welcomeEmailSent ? "" : "couldn't not be")} sent.";

              // Send Intercom event for coachee invitation (manual welcome email)
              if (welcomeEmailSent && participantExternalId.HasValue) {
                SendEvent(
                  intercom => intercom.CoacheeInvited()
                    .FromSession()
                    .WithCoacheeEmailAddress(CoacheeInfo.EmailAddress)
                    .WithOrganisation(CoacheeInfo.TenantOrgId, CoacheeInfo.OrgName),
                  operationName: "AddParticipant_CoacheeInvited",
                  requestRawUrl: SystemWeb.RequestRawUrl,
                  telemetryProperties: new Dictionary<string, object> {
                    ["ParticipantEmail"] = CoacheeInfo?.EmailAddress
                  }
                );
              }
            }

            if (formValues.SendMeetCoachNow) {
              bool meetCoachEmailSent = AlbertEmails.SendMeetCoachEmail(null, CoacheeInfo, ProjectInfo, coachInfo);
              msg += $"<br/>Meet Coachee Email {(meetCoachEmailSent ? "" : "couldn't not be")} sent.";
            }
          }
        }

        ajax.SetRedirectUrl(PathHelper.Pages.Coachees(), msg, AjaxSubmitHelper.PageMessageType.SuccessDialog);
        return;
      }

    }

    public bool GetFormValues_InfoTab(AjaxSubmitHelper ajax, FormValues formValues) {

      // Participant Info
      formValues.FirstName = ajax.CheckFieldRegex(FormFields.FirstName, "First Name", AppHelper.Regex.GeneralText, true, "Please enter a First Name.");
      formValues.LastName = ajax.CheckFieldRegex(FormFields.LastName, "Last Name", AppHelper.Regex.GeneralText, true, "Please enter a Last Name.");
      formValues.EmailAddress = ajax.CheckFieldRegex(FormFields.EmailAddress, "Email Address", AppHelper.Regex.Email, true, "Please enter a valid Email Address.");

      // Check if a participant with that email already exists in the program.
      var coacheeInProgram = DbHelper.AlbertCoachees.GetCoacheeInfo(formValues.EmailAddress, formValues.ProgramJobId);
      if (coacheeInProgram != null) {
        ajax.AddBadField(FormFields.EmailAddress, "A participant with this email already exists in the program.");
        return false;
      }

      formValues.IsNewCompany = WebHelper.GetFormValue(FormFields.CompanyId) == PathHelper.AbleUrlValues.IdNew;
      formValues.IsNewProject = WebHelper.GetFormValue(FormFields.ProjectJobNumber) == PathHelper.AbleUrlValues.IdNew;
      formValues.IsNewProgram = WebHelper.GetFormValue(FormFields.ProgramJobId) == PathHelper.AbleUrlValues.IdNew || formValues.IsNewProject;

      if (formValues.IsNewCompany && !formValues.IsNewProject) {
        ajax.AddDialogMessage("New Company requires a new Project");
        return false;
      }

      // Company Info
      if (formValues.IsNewCompany) {

        formValues.CompanyName = ajax.CheckFieldRegex(FormFields.CompanyName, "Company Name", AppHelper.Regex.GeneralText, true, "Use plain characters for Company Name.");
        formValues.ClientLeadUserId = userInfo.UserId;

      } else {

        formValues.CompanyId = ajax.CheckFieldID(FormFields.CompanyId, "Company", true, "Please select a Company.");

        var companyInfo = CompanyList.Find(x => x.CompanyId == formValues.CompanyId);
        if (companyInfo == null) {
          ajax.AddReturnValue(FormFields.CompanyId, "Company not found");
          return false;
        } else {
          formValues.CompanyOrgId = companyInfo.OrgId;
        }
      }

      // Project Info
      if (formValues.IsNewProject) {

        formValues.ProjectName = ajax.CheckFieldRegex(FormFields.ProjectName, "Project Name", AppHelper.Regex.GeneralText, true, "Use plain characters for Project Name.");

      } else {

        var selectedProject = ajax.CheckFieldRegex(FormFields.ProjectJobNumber, "Project", AppHelper.Regex.GeneralText, true, "Please select a Project.");
        var projectExistsInCompany = DbHelper.Projects.ProjectExistsInCompany(formValues.CompanyId, selectedProject);

        if (!projectExistsInCompany) {

          ajax.AddBadField(FormFields.ProjectJobNumber, "Project not found in Company");

        } else {

          formValues.ProjectJobNumber = selectedProject;

          // Program Info
          var programsInProject = DbHelper.AblePrograms.GetProjectProgramsList(formValues.ProjectJobNumber);
          var selectedProgram = WebHelper.GetFormValueIntOrDefault(FormFields.ProgramJobId, 0);

          if (programsInProject == null || programsInProject.Count == 0) {

            // No Programs in Project, create one.
            formValues.IsNewProgram = true;

          } else {

            var programInfo = programsInProject.Find(x => x.ProgramJobId == selectedProgram);

            if (selectedProgram == 0) {
              // There are program(s) in Project, and none was selected.
              ajax.AddBadField(FormFields.ProgramJobId, "You must select a Program");

            } else if (!programsInProject.Exists(x => x.ProgramJobId == selectedProgram)) {
              // The received Program doesn't exists in Project.
              ajax.AddBadField(FormFields.ProgramJobId, "Program not found in Project");

            } else {
              // A valid Program was selected.
              formValues.ProgramJobId = selectedProgram;
            }
          }
        }
      }

      if (ajax.BadFieldCount > 0) return false;

      return true;
    }

    public bool GetFormValues_Subscription(AjaxSubmitHelper ajax, FormValues formValues) {

      int subscription_ProductId = WebHelper.GetFormValueIntOrDefault(FormFields.Subscription_ProductId, 0);
      formValues.HasActiveSubscription = WebHelper.GetFormValue(FormFields.HasActiveSubscription) == "1";
      formValues.CreateANewSubscription = WebHelper.GetFormValue(FormFields.CreateANewSubscription) == "on"; // 'on' value for custom checked box

      // If the user doesn't have an active subscription or wants to create a new one, then proceed to look for product.
      if (!formValues.HasActiveSubscription || (formValues.HasActiveSubscription && formValues.CreateANewSubscription)) {

        if (subscription_ProductId == 0) {
          ajax.AddBadField(FormFields.Subscription_ProductId, "You must select a subscription");
        }

        var selectedSubscription_ProductInfo = ProductList.Find(x => x.ProductId == subscription_ProductId);
        if (selectedSubscription_ProductInfo == null) {
          ajax.AddBadField(FormFields.Subscription_ProductId, "Invalid Subscription.");
        } else {
          var selectedSubscription = AllSubscriptionsList.Find(x => x.SubscriptionId == selectedSubscription_ProductInfo.SubscriptionId);
          if (selectedSubscription == null) {
            ajax.AddBadField(FormFields.Subscription_ProductId, "Invalid Subscription.");
          }
          formValues.SubscriptionInfo = selectedSubscription;
        }

        formValues.Subscription_ProductInfo = selectedSubscription_ProductInfo;
      } else {

        var activeSubscriptionId = WebHelper.GetFormValue(FormFields.ActiveSubscriptionId);
        if (!activeSubscriptionId.IsNullOrEmpty()) {
          var currentSub = AllSubscriptionsList.Find(x => x.SubscriptionId.ToString() == activeSubscriptionId);
          formValues.SubscriptionInfo = currentSub;
        }
      }

      if (ajax.BadFieldCount > 0) return false;

      return true;
    }

    public bool GetFormValues_Coaching(AjaxSubmitHelper ajax, FormValues formValues) {

      string coachingTypeValue = WebHelper.GetFormValue(FormFields.CoachingType);
      var dbCoachingType = DbHelper.AlbertCoachingTypes.GetCoachingTypeByIntercomValueOrNull(coachingTypeValue);
      if (dbCoachingType == null) {
        ajax.AddBadField(FormFields.CoachingType, "Incorrect Coaching Type selected." + (ConfigHelper.IsLiveServer ? "" : "coachingTypeName = " + coachingTypeValue));
      }

      formValues.CoachingTypeName = coachingTypeValue;
      formValues.CoachingTypeId = dbCoachingType.CoachingTypeId;
      formValues.SessionsAllocated = ajax.CheckFieldInt(FormFields.SessionsAllocated, "Sessions Allocated", 0, 99, false, "Please provide the number of allocated sessions.");

      if (formValues.CoachingTypeName != DbHelper.AlbertCoachingTypes.GetIntercomValue_NoCoaching() && formValues.SessionsAllocated == 0) {
        ajax.AddBadField(FormFields.SessionsAllocated, "You must allocate sessions, if selecting Coaching Type.");
      }

      if (ajax.BadFieldCount > 0) return false;

      return true;
    }

    public bool GetFormValues_Settings(AjaxSubmitHelper ajax, FormValues formValues) {

      if (formValues == null || formValues.SubscriptionInfo == null) return false;

      formValues.WelcomeEmailUtc = ajax.GetDatePickerToUtc(FormFields.WelcomeEmailDate, SessionHelper.GetSessionTimeZone(), "Welcome Email Date", false, "Please provide a date.");
      formValues.SendWelcomeNow = WebHelper.GetFormValue(FormFields.SendWelcomeNow) == "1";
      formValues.MeetCoachEmailUtc = ajax.GetDatePickerToUtc(FormFields.MeetCoachEmailDate, SessionHelper.GetSessionTimeZone(), "Meet-Coach Email Date", false, "Please provide a date.");
      formValues.SendMeetCoachNow = WebHelper.GetFormValue(FormFields.SendMeetCoachNow) == "1";

      // Subscription settings

      if (formValues.SubscriptionInfo.HasNudges) {
        formValues.EnableNudges = ajax.CheckFieldBool(FormFields.EnableNudges, WebHelper.YesNoButton_ValueNo);
      }

      if (formValues.SubscriptionInfo.HasPulse) {
        formValues.EnablePulse = ajax.CheckFieldBool(FormFields.EnablePulse, WebHelper.YesNoButton_ValueYes);
      }

      if (formValues.SubscriptionInfo.HasAICoaching) {
        formValues.EnableAICoaching = ajax.CheckFieldBool(FormFields.EnableAICoaching, WebHelper.YesNoButton_ValueYes);
      }

      if (ajax.BadFieldCount > 0) return false;

      return true;
    }

    public string GetCompanyOptions() {
      string html = "";
      foreach (var cmp in CompanyList) {
        html += "<option value=\"" + cmp.CompanyId + "\">" + cmp.CompanyName.HTMLEncode() + "</option>";
      }
      return html;
    }

    public List<WebHelper.SelectOption> GetProjectOptions() {
      return new List<WebHelper.SelectOption>() {
          new WebHelper.SelectOption("", "[Select or add Project]"),
          new WebHelper.SelectOption(PathHelper.AbleUrlValues.IdNew, "[Add New Project]")
        };
    }

    public void GetProgramsInProject(AjaxSubmitHelper ajax) {

      var projectJobNumber = WebHelper.GetFormValue(FormFields.ProjectJobNumber);
      if (projectJobNumber == null) {
        ajax.AddErrorToast("Project not found");
        return;
      }

      var programsInProject = DbHelper.AblePrograms.GetProgramsByJobNumber(projectJobNumber);
      if (programsInProject == null || programsInProject.ProgramInfoList.Count == 0) {
        ajax.AddInfoToast("No programs found, one will be automatically created.");
        return;
      }

      var optionsHtml = new StringBuilder();
      optionsHtml.AppendLine("<option value=\"\">[Select Program]</option>");

      foreach (var prog in programsInProject.ProgramInfoList) {
        optionsHtml.Append("<option");
        optionsHtml.Append($@" data-{DataAttr.DeliveryPercentage}=""{prog.Partner_DeliveryPercentage.GetValueOrDefault(ConfigHelper.Financial.DefaultQuoteDeliveryPercentage)}""");
        optionsHtml.Append(" value=\"" + prog.ProgramJobId + "\">");
        optionsHtml.Append(prog.ProgramJobName);
        optionsHtml.AppendLine("</option>");
      }

      ajax.AddReturnValue(AjaxReturnData.ProgramsInProjectHtml, WebHelper.GetSelectRow("Program:", FormFields.ProgramJobId, 8, optionsHtml.ToString(), ""));
    }


    public string GetCoachingTypeOptionsHtml() {

      var ctChosen = DbHelper.AlbertCoachingTypes.GetCoachingTypeByIdOrNull(CoacheeInfo.CoachingTypeId);
      string selectedValue = ctChosen == null ? "" : ctChosen.IntercomFieldValue;
      var buttonList = new List<WebHelper.ButtonGroupButton>();

      foreach (var ct in DbHelper.AlbertCoachingTypes.GetCoachingTypeList()) {
        if (!ct.UIHidden) {
          buttonList.Add(new WebHelper.ButtonGroupButton(ct.IntercomFieldValue, ct.IntercomFieldValue,
            new WebHelper.DataAttributes((DataNames.Durations, ct.GetDurations().ToStringList().EmptyIfNull()))
          ));
        }
      }
      return WebHelper.GetButtonGroup("Coaching Type:", FormFields.CoachingType, buttonList, selectedValue);
    }

    public WebHelper.PurchaseServices.CustomStepPanel GetParticipantInfoStepHtml() {

      return new WebHelper.PurchaseServices.CustomStepPanel(WebHelper.PurchaseServices.StepPanelType.ParticipantInformation, $@"
        {(WebHelper.GetSelectRow("Customer Company:", FormFields.CompanyId, 10,
            "<option value=\"\">[Select or add Company]</option>"
          + "<option value=\"" + PathHelper.AbleUrlValues.IdNew + "\">[Add New Company]</option>"
          + GetCompanyOptions(), "", false))}

        <div class=""displaynone add-new-fields"" id=""newCompanyInfo"">
          {(WebHelper.GetTextInput("New Company Name:", FormFields.CompanyName, "", "", 2, 10))}
        </div>

        {(WebHelper.GetSelectRow(
            new WebHelper.RowOptions("Project:", 10),
            new WebHelper.SelectInfo() {
              IsReadOnly = false,
              InputName = FormFields.ProjectJobNumber,
              Options = GetProjectOptions()
            }))}

        <div class=""displaynone add-new-fields"" id=""newProjectInfo"">
          <div id=""project_noedit"">
            {(WebHelper.GetTextInput("New Job Number:", FormFields.TBAJobNumber, "", "TBA", 2, 2, "", true))}
            {(WebHelper.GetTextInput("New Project Name:", FormFields.ProjectName, "", "", 2, 10, "", false))}
          </div>
        </div>

        <div class=""displaynone programselection""></div>

        <hr />
        <h4>Participant</h4>
        {(WebHelper.GetTextInputDual("Name:",
            FormFields.FirstName, CoacheeInfo.FirstName, "First Name",
            FormFields.LastName, CoacheeInfo.LastName, "Last Name",
            false, WebHelper.InputMaxLength.EmailName, 10))}
        {(WebHelper.GetTextInput("Email Address:", FormFields.EmailAddress, CoacheeInfo.EmailAddress, 10, "", false))}"
      );
    }

    public WebHelper.PurchaseServices.CustomStepPanel GetCustomCoachingStepHtml() {

      return new WebHelper.PurchaseServices.CustomStepPanel(WebHelper.PurchaseServices.StepPanelType.Coaching, $@"
        {(GetCoachingTypeOptionsHtml())}
        <div class=""coachingFields display-none"">
          {(WebHelper.GetTextInput("Total Sessions Allocated:", FormFields.SessionsAllocated, CoacheeInfo.UserActivity?.SessionsAllocated.ToString(), 2, ""))}
        </div>"
      );
    }

    public WebHelper.PurchaseServices.CustomStepPanel GetCustomSettingsStepHtml() {

      var defaultSubscription = AllSubscriptionsList.Find(x => x.SubscriptionId == ConfigHelper.SubscriptionId.AI_Coaching);

      return new WebHelper.PurchaseServices.CustomStepPanel(WebHelper.PurchaseServices.StepPanelType.ParticipantSettings, $@"

        {(WebHelper.GetInputDateRow(
          "On-Boarding Date (" + ConfigHelper.DefaultTimeZoneAbbrev + "):",
          FormFields.WelcomeEmailDate,
          TimeHelper.UtcToAppDefaultTimeZone(CoacheeInfo.WelcomeEmailUtc),
            "<span class=\"ml20\">Send Now: </span>" +
            WebHelper.CustomCheckBox(new WebHelper.CheckboxInfo() {
              InputName = FormFields.SendWelcomeNow,
              Value = "1",
              IsReadOnly = false,
              Class = (CoacheeInfo.CanReceiveWelcomeEmail ? "" : "no-welcome-tip")
            }),
            false, "flex"
        ))}

        {(WebHelper.GetInputDateRow(
          "Meet Coach Date (" + ConfigHelper.DefaultTimeZoneAbbrev + "):", FormFields.MeetCoachEmailDate,
          TimeHelper.UtcToAppDefaultTimeZone(CoacheeInfo.MeetCoachEmailUtc),
          (CoacheeInfo.MeetCoachEmailSentUtc == null ? "" :
            "<span class=\"\">" + "Sent On: " + TimeHelper.UtcToAppDefaultTimeZone(CoacheeInfo.MeetCoachEmailSentUtc).ToString(WebHelper.DATE_OUTPUT_FORMAT) + "</span>"
          ) +
          "<span class=\"ml20\">Send " + (CoacheeInfo.MeetCoachEmailSentUtc == null ? "" : "Again") + " Now:</span>"
          + WebHelper.CustomCheckBox(new WebHelper.CheckboxInfo() {
            InputName = FormFields.SendMeetCoachNow,
            Value = "1"
          }),
          false, "flex"
         ))}

        <hr/>
        <div class=""{FormClasses.SubscriptionSettings}"">
          {(GetSubscriptionSettingsHtml(defaultSubscription))}
        </div>"
      );
    }

    public List<WebHelper.PurchaseServices.CustomStepPanel> GetCustomStepsHtml() {

      var customTabs = new List<WebHelper.PurchaseServices.CustomStepPanel>();

      customTabs.Add(GetParticipantInfoStepHtml());
      customTabs.Add(GetCustomCoachingStepHtml());
      customTabs.Add(GetCustomSettingsStepHtml());

      return customTabs;
    }

    public void GetSubscriptionSettings(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();

      if (!GetFormValues_Subscription(ajax, formValues) || ajax.BadFieldCount > 0) {
        ajax.AddReturnValue(AjaxReturnData.StepTabWithBadFields, WebHelper.PurchaseServices.StepPanelType.Subscription.ToString());
        return;
      }

      ajax.AddReturnValue(AjaxReturnData.SubscriptionSettingsHtml, GetSubscriptionSettingsHtml(formValues.SubscriptionInfo));
    }

    public void SearchUsersActiveSubscription(AjaxSubmitHelper ajax) {

      var emailAddress = ajax.CheckFieldRegex(FormFields.EmailAddress, "Email Address", AppHelper.Regex.Email, true, "Please enter a valid Email Address.");

      if (ajax.BadFieldCount > 0) {
        ajax.AddReturnValue(AjaxReturnData.StepTabWithBadFields, WebHelper.PurchaseServices.StepPanelType.ParticipantInformation.ToString());
        return;
      }

      DbHelper.Common.UsingTransaction(trans => {

        var userSub = DbHelper.Subscriptions.User.GetUserSubscriptionInfoByEmail(trans, emailAddress);
        if (userSub != null) {
          var prodId = ProductList.Find(x => x.SubscriptionId == userSub.SubscriptionId && x.ProductCategoryId == (int)DbHelper.Products.ProductCategory.Subscription)?.ProductId;
          ajax.AddReturnValue(AjaxReturnData.ActiveProductId, prodId);
          ajax.AddReturnValue(AjaxReturnData.ActiveSubscriptionHtml, GetActiveSubscriptionHtml(userSub));
        }

        return true;
      });
    }

    public string GetActiveSubscriptionHtml(DbHelper.Subscriptions.User.UserSubscriptionInfo subscriptionInfo) {

      if (subscriptionInfo == null) return "";

      return $@"
        <div class=""{FormClasses.ActiveSubscription}"">
          <div class=""displaynone"">
            {WebHelper.CustomCheckBox(FormFields.HasActiveSubscription, "1", true, "")}
            {WebHelper.GetTextInput("", FormFields.ActiveSubscriptionId, "", subscriptionInfo.SubscriptionId.ToString())}
          </div>
          This user currently has a {subscriptionInfo.SubscriptionName} subscription, it's valid until {WebHelper.DisplayDate(subscriptionInfo.SubscriptionEndUtc)}. <br/>
          <label class=""custom-checkbox pb10"">
            <input type=""checkbox"" name=""{FormFields.CreateANewSubscription}"">
            <span class=""checkbox-label"">Create a new subscription {WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, "If you create a new subscrition, the current one will be canceled.", "", "ml5")}</span>
          </label>
        </div>
      ";
    }

    private string GetSubscriptionSettingsHtml(DbHelper.Subscriptions.SubscriptionInfo subscriptionInfo) {

      return $@"
        {(WebHelper.GetYesNoButtons("Enable Nudges:", FormFields.EnableNudges, subscriptionInfo.HasNudges, !subscriptionInfo.HasNudges))}
        {(WebHelper.GetYesNoButtons("Enable AI Coach:", FormFields.EnableAICoaching, subscriptionInfo.HasAICoaching, !subscriptionInfo.HasAICoaching))}
        {(WebHelper.GetYesNoButtons("Enable Pulse:", FormFields.EnablePulse, subscriptionInfo.HasPulse, !subscriptionInfo.HasPulse))}";
    }

    private void CreateCompany(SqlTransaction trans, FormValues formValues) {

      // Add new company & get id.
      var newCompanyInfo = DbHelper.ClientCompanies.CreateCompanyBrief(trans, userInfo.OrgId, formValues.CompanyName, formValues.ClientLeadUserId);
      formValues.CompanyId = newCompanyInfo.CompanyId;
      formValues.CompanyOrgId = userInfo.OrgId;
    }

    private void CreateProjectAndProgram(SqlTransaction trans, FormValues formValues) {

      // Add new project & get id.
      DbHelper.Projects.CreateProjectAndProgram(
        trans: trans,
        companyId: formValues.CompanyId,
        projectName: formValues.ProjectName,
        preferredProgramName: $"Program for {formValues.FirstName} {formValues.LastName}",
        tenantOrgId: userInfo.OrgId, // Set new project parent org to same as the users.
        canSelfSelectCoach: false,
        createdByUserId: userInfo.UserId,
        newJobNumber: out formValues.ProjectJobNumber,
        newProgramJobId: out formValues.ProgramJobId
      );
    }

    private void CreateParticipant(SqlTransaction trans, FormValues formValues) {

      var existingCoacheeWasUndeleted = DbHelper.AlbertCoachees.UndeleteCoachee(null, formValues.EmailAddress, formValues.ProgramJobId);

      if (!existingCoacheeWasUndeleted) {

        CoacheeInfo.FirstName = formValues.FirstName;
        CoacheeInfo.LastName = formValues.LastName;
        CoacheeInfo.EmailAddress = formValues.EmailAddress;
        CoacheeInfo.ProgramStatusId = DbHelper.CoacheeProgramStatus.GetStatus_WaitingLaunch().ProgramStatusId;
        CoacheeInfo.TenantOrgId = formValues.CompanyOrgId;
        CoacheeInfo.CompanyId = formValues.CompanyId;
        CoacheeInfo.ProgramJobId = formValues.ProgramJobId;
        CoacheeInfo.CoachUserId = userInfo.UserId; // By default set the creating user as the Coach
        CoacheeInfo.SubscriptionUser = true;
        CoacheeInfo.UserActivity.SessionsAllocated = formValues.SessionsAllocated;
        CoacheeInfo.CoachingTypeId = formValues.CoachingTypeId;

        CoacheeInfo.CoacheeId = DbHelper.AlbertCoachees.CreateCoachee(trans, CoacheeInfo);
      }
    }

    private void CreateQuote(SqlTransaction trans, FormValues formValues) {

      var newQuoteInfo = new DbHelper.AbleQuotes.NewQuoteInfo(
        jobNumber: formValues.ProjectJobNumber,
        ownerUserId: userInfo.UserId,
        leadConsultantUserId: ConfigHelper.SelfCreatedUserDefaults.ProgramData.PLC,
        proposalDesignerUserId: null,
        contactUserId: userInfo.UserId,
        quoteTitle: $"Able Main {WebHelper.DisplayDate(DateTime.UtcNow)} - {formValues.FirstName} {formValues.LastName}",
        brandingOrgId: userInfo.OrgId,
        quoteStatusId: DbHelper.AbleQuoteStatus.GetStatus(DbHelper.AbleQuoteStatus.AppTagEnum.accepted).QuoteStatusId,
        estimatedStartDateUtc: null,
        xeroTaxType: ConfigHelper.SelfCreatedUserDefaults.QuoteData.XeroTaxType,
        customInvoicing: ConfigHelper.SelfCreatedUserDefaults.QuoteData.CustomInvoicing,
        addToFreshSales: ConfigHelper.SelfCreatedUserDefaults.QuoteData.AddToFreshSales,
        excludeFromSalesIncentive: ConfigHelper.SelfCreatedUserDefaults.QuoteData.ExcludeFromSalesIncentive,
        quoteDealSourceId: ConfigHelper.SelfCreatedUserDefaults.QuoteData.QuoteDealSource_Undefined,
        oppPercentage: ConfigHelper.SelfCreatedUserDefaults.QuoteData.OPPPercentage,
        plcPercentage: ConfigHelper.SelfCreatedUserDefaults.QuoteData.PLCPercentage,
        deliveryPercentage: ConfigHelper.SelfCreatedUserDefaults.QuoteData.DeliveryPercentage,
        platformPercentage: ConfigHelper.SelfCreatedUserDefaults.QuoteData.DeliveryPercentage,
        proposalDesignerPercentage: ConfigHelper.SelfCreatedUserDefaults.QuoteData.ProposalDesignerPercentage,
        coverLetterHtml: ""
      );

      formValues.QuoteId = DbHelper.AbleQuotes.CreateQuote(trans, newQuoteInfo);
      formValues.QuoteTitle = newQuoteInfo.QuoteTitle;

      if (formValues.QuoteId == 0) {
        throw new Exception("Couldn't create quote");
      }

      // Add logged in user as Quote Team user
      DbHelper.AbleQuotes.AddQuoteTeamUser(trans, formValues.QuoteId, userInfo.UserId);
      int quoteId = formValues.QuoteId;

      // Create quote items...

      // Coaching
      if (formValues.CoachingTypeName != DbHelper.AlbertCoachingTypes.GetIntercomValue_NoCoaching()) {

        var coachingQuoteItemId = CreateQuoteItems(trans, quoteId, ConfigHelper.Participants.DefaultCoachingProductId, formValues.SessionsAllocated, ConfigHelper.Participants.DefaultCoachingProductDescription);

        if (coachingQuoteItemId == 0) {
          throw new Exception("Couldn't create Quote Item Id for Coaching");
        }

        // Create Session Components
        var updateComponentsInfo = new DbHelper.ProgramComponents.UpdateSessionComponentsInfo(CoacheeInfo);
        // Add each session to components object
        for (int i = 1; i <= formValues.SessionsAllocated; i++) {
          updateComponentsInfo.AddSessionToUpdate(i, 0, coachingQuoteItemId);
        }
        // Create session components
        DbHelper.ProgramComponents.UpdateSessionComponents(trans, updateComponentsInfo);
      }

      // Subscription
      if (formValues.Subscription_ProductInfo != null && formValues.SubscriptionInfo.SubscriptionId != null) {
        int subscriptionQuantity = 1;
        var subscriptionQuoteItemId = CreateQuoteItems(trans, quoteId, formValues.Subscription_ProductInfo.ProductId, subscriptionQuantity, formValues.Subscription_ProductInfo.ProductDescription);

        if (subscriptionQuoteItemId == 0) {
          throw new Exception("Couldn't create Quote Item for Subscription");
        }

        // Create QuoteItemForSubscription object to link created QuoteItem to subscription
        formValues.QuoteItemForSubscription = new DbHelper.AbleQuotes.QuoteItemForSubscription(
          quoteItemId: subscriptionQuoteItemId,
          description: formValues.Subscription_ProductInfo.ProductDescription,
          displayOrder: 1,
          unitPrice: 0,
          subscriptionId: formValues.Subscription_ProductInfo.SubscriptionId.Value,
          productTitle: formValues.Subscription_ProductInfo.DisplayTitle,
          allocatedSubscriptions: 1,
          assignedSubscriptions: 0);
      } else {
        formValues.QuoteItemForSubscription = null;
      }

      // Update Coachee
      DbHelper.AlbertCoachees.UpdateCoachee(trans, CoacheeInfo);
    }

    private int CreateQuoteItems(SqlTransaction trans, int quoteId, int productId, int quantity, string productDescription) {
      return DbHelper.AbleQuotes.CreateQuoteItem(
        trans: trans,
        quoteId: quoteId,
        productId: productId,
        itemDescription: productDescription,
        isOptionalId: DbHelper.AbleQuotes.OptionalEnum.No.Id,
        unitPrice: 0,
        quantity: quantity,
        quantityDescr: ConfigHelper.SelfCreatedUserDefaults.QuoteData.QuoteItems_QuantityDescription,
        isAccepted: true // Accepted by default
      );
    }


    private void CreateParticipantSettings(SqlTransaction trans, FormValues formValues) {

      // Coaching
      CoacheeInfo.UserActivity.SessionsAllocated = formValues.SessionsAllocated;
      CoacheeInfo.CoachingTypeId = formValues.CoachingTypeId;

      // Subscription
      CoacheeInfo.UserSubscription = new DbHelper.Subscriptions.User.UserSubscriptionInfo();
      CoacheeInfo.UserSubscription.SubscriptionId = formValues.SubscriptionInfo.SubscriptionId;
      CoacheeInfo.UserSubscription.UserHasAICoachEnabled = formValues.EnableAICoaching;
      CoacheeInfo.PulseSurveyEnabled = formValues.EnablePulse;
      CoacheeInfo.DisableNudges = formValues.EnableNudges;

      // Welcome Email / Meet Coach Email
      CoacheeInfo.WelcomeEmailUtc = formValues.WelcomeEmailUtc;
      CoacheeInfo.MeetCoachEmailUtc = formValues.MeetCoachEmailUtc;

      // Update Coachee
      DbHelper.AlbertCoachees.UpdateCoachee(trans, CoacheeInfo);

      // Only update/Create subscription if Participant doesn't have an Active Subscription or wants to create a new one
      if (formValues.QuoteItemForSubscription != null) {
        DbHelper.Subscriptions.User.UpdateCoacheeSubscription(trans, CoacheeInfo, formValues.QuoteItemForSubscription, false, true);
      }
    }
  }
}
