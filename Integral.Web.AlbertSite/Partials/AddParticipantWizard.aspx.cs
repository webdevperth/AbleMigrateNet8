using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using Integral.Integrations;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class AddParticipantWizard : AppCode.PageBaseClasses.LoggedInPageBase {

    public class WizardFormValuesBase {
      public int StepIndexFrom;
      public int StepIndexTo;
    }

    // The signature each validator method should adhere to.
    public delegate bool ValidatorDelegate<TFormValues>(
      AjaxSubmitHelper helper,
      TFormValues formValues,
      int stepIndex);

    public class Wizard<TFormValues> where TFormValues : WizardFormValuesBase {

      public class StepList : List<Step> { }

      public List<Step> Steps { get; } = new List<Step>();

      public class Step {
        public readonly int StepIndex;
        public readonly ValidatorDelegate<TFormValues> ValidatorMethod;
        internal Step(int stepIndex, ValidatorDelegate<TFormValues> validatorMethod) {
          StepIndex = stepIndex;
          ValidatorMethod = validatorMethod;
        }
      }

      public Wizard<TFormValues> AddStep(int stepIndex, ValidatorDelegate<TFormValues> validatorMethod) {
        Steps.Add(new Step(stepIndex, validatorMethod));
        return this;
      }
    }

    public class FormModes {
      public const string GetProgramOptions = "GetProgramOptions";
      public const string Validate = "Validate";
    }

    public class FormFields {
      public const string StepIndexFrom = "StepIndexFrom";
      public const string StepIndexTo = "StepIndexTo";
      public const string CompanyId = "CompanyId";
      public const string CompanyName = "CompanyName";
      public const string TBAJobNumber = "TBAJobNumber";
      public const string ProjectJobNumber = "ProjectJobNumber";
      public const string ProjectName = "ProjectName";
      public const string ProgramJobId = "ProgramJobId";
      public const string ProgramName = "ProgramName";
      public const string PaxFirstName = "PaxFirstName";
      public const string PaxLastName = "PaxLastName";
      public const string PaxEmail = "PaxEmail";
      public const string PaxPhone = "PaxPhone";
      public const string CoachingType = "CoachingType";
      public const string SessionsAllocated = "SessionsAllocated";
      public const string OnBoardingDate = "OnBoardingDate";
      public const string MeetCoachDate = "MeetCoachDate";
      public const string SendWelcomeNow = "SendWelcomeNow";
      public const string CoachUserId = "CoachUserId";
      public const string SendMeetCoachNow = "SendMeetCoachNow";
      public const string EnableNudges = "EnableNudges";
      public const string EnablePulse = "EnablePulse";
      public const string EnableAICoaching = "EnableAICoaching";
      public const string SubscriptionGuid = "SubscriptionGuid";
      public const string CreateNewSubscription = "CreateNewSubscription";
    }

    public class FormValues : WizardFormValuesBase {
      public string FirstName;
      public string LastName;
      public string EmailAddress;
      public bool IsNewCompany;
      public int CompanyId;
      public string CompanyName;
      public int CompanyOrgId;
      public int? ClientLeadUserId;
      public bool IsNewProject;
      public string ProjectJobNumber;
      public string ProjectName;
      public string ProgramName;
      public bool IsNewProgram;
      public int ProgramJobId;
      public int SessionsAllocated;
      public int? CoachingTypeId;
      public string CoachingTypeName;
      public bool EnableNudges;
      public bool EnableAICoaching;
      public bool EnablePulse;
      public Guid SubscriptionGuid;
      public DbHelper.Subscriptions.Org.OrgSubscriptionItem SelectedOrgSubscriptionItem;
      public DbHelper.Subscriptions.User.UserSubscriptionInfo ExistingUserSubscription;
      public bool SendWelcomeNow;
      public DateTime? WelcomeEmailUtc;
      public int CoachUserId;
      public DateTime? MeetCoachEmailUtc;
      public bool SendMeetCoachNow;

      public int QuoteId;
      public string QuoteTitle;
      //public DbHelper.Products.ProductInfo Subscription_ProductInfo;
      public DbHelper.AbleQuotes.QuoteItemForSubscription QuoteItemForSubscription;
    }

    public class AjaxReturnData {
      public const string ValidateNewStepIndex = "ValidateNewStepIndex"; // Wizard step to display after validation.
      public const string ValidateSuccess = "ValidateSuccess"; // Wizard step to display after validation.
      public const string ProgramOptionsHtml = "ProgramOptionsHtml";
      public const string RequiredSubscriptionGuid = "RequiredSubscriptionGuid";
      public const string ExistingSubscriptionGuid = "ExistingSubscriptionGuid";
      public const string PreselectSubscriptionGuid = "PreselectSubscriptionGuid";
      public const string OrganisationGuidForPaymentMethod = "OrganisationGuidForPaymentMethod";
      public const string NextPaneHtml = "NextPaneHtml";
    }

    public bool CanAddParticipant;
    public List<DbHelper.Subscriptions.SubscriptionInfo> AllSubscriptions = null;
    public List<DbHelper.Subscriptions.Org.OrgSubscriptionItem> OrgSubscriptionItems = null;

    public Wizard<FormValues> NewProgramWizard;

    private string _postDialogMessage = string.Empty;

    protected void Page_Load(object sender, EventArgs e) {

      // Add validation methods for the wizard.
      // stepIndex must match the same index of the related front-end step.
      NewProgramWizard = new Wizard<FormValues>()
        .AddStep(stepIndex: 0, ValidateStep_Program)
        .AddStep(stepIndex: 1, ValidateStep_Participant)
        .AddStep(stepIndex: 2, ValidateStep_Features)
        .AddStep(stepIndex: 3, ValidateStep_Plan)
        .AddStep(stepIndex: 4, ValidateStep_Confirm);

      PageTitle = "Add Participant";

      AllSubscriptions = DbHelper.Subscriptions.GetAllSubscriptions();
      AllSubscriptions.Sort((s1, s2) => s1.DisplayOrder.CompareTo(s2.DisplayOrder));

      OrgSubscriptionItems = DbHelper.Subscriptions.Org.GetOrgSubscriptionItems(SessionHelper.UserInfo.OrgId); // DbHelper.Subscriptions.GetAllSubscriptions();
      OrgSubscriptionItems.Sort((s1, s2) => s1.DisplayOrder.CompareTo(s2.DisplayOrder));

      // Get Participants in the company
      CanAddParticipant = SessionHelper.AppAccess.Participants.CanAdd();

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case FormModes.GetProgramOptions:
              GetProgramOptionsHtml(ajax);
              return;

            case FormModes.Validate:
              bool success = ValidateAllSteps(ajax, out int newStepIndex);
              ajax.AddReturnValue(AjaxReturnData.ValidateNewStepIndex, newStepIndex);
              ajax.AddReturnValue(AjaxReturnData.ValidateSuccess, success);
              if (!_postDialogMessage.IsNullOrEmpty()) {
                ajax.AddDialogMessage(_postDialogMessage);
              }
              return;
          }
        });
        WebHelper.EndRequest();
        return;
      }
    }

    public enum PlanFeature { Nudges, Pulse, AICoaching }

    public string GetFeatureCheckbox(PlanFeature planFeature) {

      string featureName, fieldName;
      RequiredSubscriptionArgs requiredSubArgs;

      if (planFeature == PlanFeature.Nudges) {
        featureName = "Nudges";
        fieldName = FormFields.EnableNudges;
        requiredSubArgs = new RequiredSubscriptionArgs() { HasNudges = true };
      } else if (planFeature == PlanFeature.Pulse) {
        featureName = "Pulse Surveys";
        fieldName = FormFields.EnablePulse;
        requiredSubArgs = new RequiredSubscriptionArgs() { HasPulse = true };
      } else if (planFeature == PlanFeature.AICoaching) {
        featureName = "AI Coaching";
        fieldName = FormFields.EnableAICoaching;
        requiredSubArgs = new RequiredSubscriptionArgs() { HasAICoaching = true };
      } else {
        throw new InvalidOperationException($"Unhandled plan feature: {planFeature}");
      }

      return new WebHelper.Form.FormRow() {
        LabelText = $"Enable {featureName}:",
        ContentHtml = new WebHelper.Form.CheckBox() {
          InputName = fieldName,
          Label = $"Requires <strong>{GetLowestRequiredSubscription(requiredSubArgs).SubscriptionName.HTMLEncode()}</strong> Plan",
          LabelIsHtml = true,
          LabelPosition = WebHelper.Form.CheckboxLabelPosition.After,
        }.ToHtml()
      }.ToHtml();
    }

    public bool ValidateAllSteps(AjaxSubmitHelper ajax, out int newStepIndexTo) {

      newStepIndexTo = 0;

      var formValues = new FormValues();

      int totalSteps = NewProgramWizard.Steps.Count;

      // Every step passes From (current step) and To (target step).
      formValues.StepIndexFrom = ajax.CheckFieldInt(FormFields.StepIndexFrom, true);
      formValues.StepIndexTo = ajax.CheckFieldInt(FormFields.StepIndexTo, true);
      if (ajax.HasErrors) return false;

      if (formValues.StepIndexFrom.Outside(0, totalSteps - 1)) throw new InvalidOperationException("Front end stepIndexFrom outside range.");
      if (formValues.StepIndexTo.Outside(0, totalSteps - 1)) throw new InvalidOperationException("Front end stepIndexTo outside range.");

      if (formValues.StepIndexTo < formValues.StepIndexFrom) return true; // Always approve going back, no validation required.

      // Validate all completed steps.
      for (int stepIndex = 0; stepIndex <= formValues.StepIndexFrom; stepIndex++) {
        bool stepValid = NewProgramWizard.Steps[stepIndex].ValidatorMethod(ajax, formValues, stepIndex);
        if (!stepValid || ajax.HasErrors) {
          newStepIndexTo = stepIndex; // Change target to the failed step.
          return false;
        }
      }
      // Success, show the intended target step.
      newStepIndexTo = formValues.StepIndexTo;
      return true;
    }

    private bool ValidateStep_Program(AjaxSubmitHelper ajax, FormValues formValues, int thisStepIndex) {

      // Wizard Step 1 Form (stepIndex 0)
      formValues.FirstName = ajax.CheckFieldRegex(FormFields.PaxFirstName, "First Name", AppHelper.Regex.GeneralText, true, "Please enter a First Name.");
      formValues.LastName = ajax.CheckFieldRegex(FormFields.PaxLastName, "Last Name", AppHelper.Regex.GeneralText, true, "Please enter a Last Name.");
      formValues.EmailAddress = ajax.CheckFieldRegex(FormFields.PaxEmail, "Email Address", AppHelper.Regex.Email, true, "Please enter a valid Email Address.");
      if (ajax.HasErrors) return false;

      formValues.IsNewCompany = WebHelper.GetFormValue(FormFields.CompanyId) == PathHelper.AbleUrlValues.IdNew;
      formValues.IsNewProject = WebHelper.GetFormValue(FormFields.ProjectJobNumber) == PathHelper.AbleUrlValues.IdNew;
      formValues.IsNewProgram = WebHelper.GetFormValue(FormFields.ProgramJobId) == PathHelper.AbleUrlValues.IdNew;

      if (formValues.IsNewCompany && !formValues.IsNewProject) {
        ajax.AddBadField(FormFields.ProjectJobNumber, "New Company requires a new Project");
        return false;
      }

      if (formValues.IsNewProject && !formValues.IsNewProgram) {
        ajax.AddBadField(FormFields.ProjectJobNumber, "New Project requires a new Program");
        return false;
      }

      // Company Info
      if (formValues.IsNewCompany) {

        formValues.CompanyName = ajax.CheckFieldRegex(FormFields.CompanyName, "Company Name", AppHelper.Regex.GeneralText, true, "Use plain characters for Company Name.");
        formValues.CompanyOrgId = SessionHelper.UserInfo.OrgId;
        formValues.ClientLeadUserId = userInfo.UserId;
        if (ajax.HasErrors) return false;

        // Check for existing company name,
        if (DbHelper.ClientCompanies.CompanyNameExists(formValues.CompanyName)) {
          ajax.AddDialogMessage("A Company of that name already exists.");
          return false;
        }

      } else {

        formValues.CompanyId = ajax.CheckFieldID(FormFields.CompanyId, "Company", true, "Please select a Company.");
        if (ajax.HasErrors) return false;

        var companies = DbHelper.ClientCompanies.GetCompanyList(SessionHelper.GetUserInfoOrNull());
        var companyInfo = companies.Find(x => x.CompanyId == formValues.CompanyId);
        if (companyInfo == null) {
          ajax.AddBadField(FormFields.CompanyId, "Company not found");
          return false;
        }
        formValues.CompanyOrgId = companyInfo.OrgId;
      }

      // Project Info
      if (formValues.IsNewProject) {

        formValues.ProjectName = ajax.CheckFieldRegex(FormFields.ProjectName, "Project Name", AppHelper.Regex.GeneralText, true, "Use plain characters for Project Name.");
        if (ajax.HasErrors) return false;

      } else {

        formValues.ProjectJobNumber = ajax.CheckFieldRegex(FormFields.ProjectJobNumber, "Project", AppHelper.Regex.GeneralText, true, "Please select a Project.");
        if (ajax.HasErrors) return false;

        var projectExistsInCompany = DbHelper.Projects.ProjectExistsInCompany(formValues.CompanyId, formValues.ProjectJobNumber);
        if (!projectExistsInCompany) {
          ajax.AddBadField(FormFields.ProjectJobNumber, "Project not found in Company");
          return false;
        }
      }

      // Program Info
      if (formValues.IsNewProgram) {

        formValues.ProgramName = ajax.CheckFieldRegex(FormFields.ProgramName, "Program Name", AppHelper.Regex.GeneralText, true, "Use plain characters for Program Name.");
        if (ajax.HasErrors) return false;

      } else {

        var programsInProject = DbHelper.AblePrograms.GetProjectProgramsList(formValues.ProjectJobNumber);
        if (programsInProject.IsNullOrEmpty()) {
          ajax.AddBadField(FormFields.ProgramJobId, "Programs not found.");
          return false;
        }

        formValues.ProgramJobId = WebHelper.GetFormValueIntOrDefault(FormFields.ProgramJobId, 0);
        var programInfo = programsInProject.Find(p => p.ProgramJobId == formValues.ProgramJobId);
        if (programInfo == null) {
          ajax.AddBadField(FormFields.ProgramJobId, "Program not found.");
          return false;
        }

        // Check if existing coachee with same email exists,
        var existingCoachee = DbHelper.AlbertCoachees.GetCoacheeInfo(formValues.EmailAddress, formValues.ProgramJobId);
        if (existingCoachee != null) {
          ajax.AddDialogMessage("A Participant already exists in this Program with that email address.");
          return false;
        }

        // Check if a soft-deleted coachee exists with the same email address.
        // If so, check if it can be hard deleted. If it can't be hard deleted then user can't add the participant.
        if (DbHelper.AlbertCoachees.TryGetSoftDeletedCoacheeId(null, formValues.EmailAddress, formValues.ProgramJobId, out int softDeletedCoacheeId)) {
          var components = DbHelper.ProgramComponents.GetForCoachee(softDeletedCoacheeId);
          if (components != null && !SessionHelper.AppAccess.Participants.CanHardDelete(softDeletedCoacheeId, components)) {
            ajax.AddDialogMessage("Can't add Coachee with this email address.<br/>"
              + "A previous Coachee in this Program, with the same email address, is connected to existing components that cannot be removed.<br/>"
              + "please contact us if if you need the previous coachee restored.");
            return false;
          }
        }
      }

      return !ajax.HasErrors;
    }

    private bool ValidateStep_Participant(AjaxSubmitHelper ajax, FormValues formValues, int thisStepIndex) {

      formValues.WelcomeEmailUtc = ajax.GetDatePickerToUtc(FormFields.OnBoardingDate, SessionHelper.GetSessionTimeZone(), "Welcome Email Date", false, "Please provide a date.");
      formValues.SendWelcomeNow = ajax.GetCheckbox(FormFields.SendWelcomeNow);
      formValues.CoachUserId = ajax.CheckFieldIDOrNull(FormFields.CoachUserId, "Coach", false, "") ?? ConfigHelper.UserId.Unassigned;
      formValues.MeetCoachEmailUtc = ajax.GetDatePickerToUtc(FormFields.MeetCoachDate, SessionHelper.GetSessionTimeZone(), "Meet-Coach Email Date", false, "Please provide a date.");
      formValues.SendMeetCoachNow = ajax.GetCheckbox(FormFields.SendMeetCoachNow);

      string coachingTypeValue = WebHelper.GetFormValue(FormFields.CoachingType);
      var dbCoachingType = DbHelper.AlbertCoachingTypes.GetCoachingTypeByIntercomValueOrNull(coachingTypeValue);
      if (dbCoachingType == null) {
        ajax.AddBadField(FormFields.CoachingType, "Incorrect Coaching Type selected." + (ConfigHelper.IsLiveServer ? "" : "coachingTypeName = " + coachingTypeValue));
        return false;
      }

      formValues.CoachingTypeName = coachingTypeValue;
      formValues.CoachingTypeId = dbCoachingType.CoachingTypeId;
      formValues.SessionsAllocated = ajax.CheckFieldInt(FormFields.SessionsAllocated, "Sessions Allocated", 0, 99, false, "Please provide the number of allocated sessions.");

      if (formValues.CoachingTypeName == DbHelper.AlbertCoachingTypes.GetIntercomValue_NoCoaching()) {
        formValues.SessionsAllocated = 0;
      } else if (formValues.SessionsAllocated < 1) {
        ajax.AddBadField(FormFields.SessionsAllocated, "You must allocate sessions, if selecting Coaching Type.");
      }

      return !ajax.HasErrors;
    }

    private bool ValidateStep_Features(AjaxSubmitHelper ajax, FormValues formValues, int thisStepIndex) {

      formValues.EnableNudges = ajax.GetCheckbox(FormFields.EnableNudges);
      formValues.EnablePulse = ajax.GetCheckbox(FormFields.EnablePulse);
      formValues.EnableAICoaching = ajax.GetCheckbox(FormFields.EnableAICoaching);

      // Get the required subscription for the above options.
      var requiredOrgSubscription = GetLowestRequiredSubscription(new RequiredSubscriptionArgs() {
        HasNudges = formValues.EnableNudges,
        HasPulse = formValues.EnablePulse,
        HasAICoaching = formValues.EnableAICoaching
      });
      ajax.AddReturnValue(AjaxReturnData.RequiredSubscriptionGuid, requiredOrgSubscription.SubscriptionGuid);

      // Existing subscription (if any) of coachee being added (found by email).
      // User cannot downgrade an existing subscription, in case its features are needed in another program,
      // but user is allowed to upgrade the subscription if desired.
      // However if existing subscription is lower than the *required* subscription, then upgrade is necessary
      // or user may go back and change the desired features to suit the current subscription.
      formValues.ExistingUserSubscription = DbHelper.Subscriptions.User.GetUserSubscriptionInfoByEmail(null, formValues.EmailAddress);
      if (formValues.ExistingUserSubscription != null) {
        ajax.AddReturnValue(AjaxReturnData.ExistingSubscriptionGuid, formValues.ExistingUserSubscription.SubscriptionGuid);
        // Include message?
        // see: ajax.AddReturnValue(AjaxReturnData.ExistingUserSubscriptionMessageHtml, GetActiveSubscriptionHtml(existingUserSubscription));
      }

      // Only pre-select the subscription for the next step if user is currently submitting this step.
      // Pre-select the required subscription, unless user has an existing subscription
      // which is *higher* than the required one - i.e. user's existing sub should not be downgraded.
      // The UI should also disable selecting lower subscriptions than an existing one.
      if (formValues.StepIndexFrom == thisStepIndex) {
        Guid preselectSubscriptionGuid = requiredOrgSubscription.SubscriptionGuid;
        if (formValues.ExistingUserSubscription != null) {
          if (formValues.ExistingUserSubscription.PricePerUserPerMonth > requiredOrgSubscription.PricePerUserPerMonth
            || formValues.ExistingUserSubscription.DisplayOrder > requiredOrgSubscription.DisplayOrder) {
            preselectSubscriptionGuid = formValues.ExistingUserSubscription.SubscriptionGuid;
          }
        }
        ajax.AddReturnValue(AjaxReturnData.PreselectSubscriptionGuid, preselectSubscriptionGuid);
      }

      return !ajax.HasErrors;
    }

    private bool ValidateStep_Plan(AjaxSubmitHelper ajax, FormValues formValues, int thisStepIndex) {

      formValues.SubscriptionGuid = ajax.CheckGuid(FormFields.SubscriptionGuid, "", true, "") ?? Guid.Empty;
      formValues.SelectedOrgSubscriptionItem = OrgSubscriptionItems.Find(s => s.SubscriptionGuid == formValues.SubscriptionGuid);

      if (formValues.SelectedOrgSubscriptionItem == null) {
        ajax.AddDialogMessage("Please choose a Learner Plan.");
        return false;
      }

      // Check if selected plan matches selected features.

      var requiredSubscription = GetLowestRequiredSubscription(new RequiredSubscriptionArgs() {
        HasNudges = formValues.EnableNudges,
        HasPulse = formValues.EnablePulse,
        HasAICoaching = formValues.EnableAICoaching
      });
      if (requiredSubscription.PricePerUserPerMonth > 0 && formValues.SelectedOrgSubscriptionItem.PricePerUserPerMonth < requiredSubscription.PricePerUserPerMonth) {
        ajax.AddDialogMessage($"Selected features for this Program require at least the <b>{requiredSubscription.SubscriptionName.HTMLEncode()}</b> plan.");
        return false;
      }

      string existingPlanName = string.Empty;
      string selectedPlanName = formValues.SelectedOrgSubscriptionItem.SubscriptionName;
      int availableSeats = formValues.SelectedOrgSubscriptionItem.AvailableSeats;
      bool isPlanUpgrade = false;

      if (formValues.ExistingUserSubscription != null) {
        // Disallow downgrade of existing plan.
        if (formValues.SelectedOrgSubscriptionItem.PricePerUserPerMonth < formValues.ExistingUserSubscription.PricePerUserPerMonth
          || formValues.SelectedOrgSubscriptionItem.DisplayOrder < formValues.ExistingUserSubscription.DisplayOrder) {
          ajax.AddDialogMessage($"Participant's existing plan cannot be downgraded.");
          return false;
        }
        // Check if upgrading plan.
        if (formValues.SelectedOrgSubscriptionItem.PricePerUserPerMonth > formValues.ExistingUserSubscription.PricePerUserPerMonth
          || formValues.SelectedOrgSubscriptionItem.DisplayOrder > formValues.ExistingUserSubscription.DisplayOrder) {
          isPlanUpgrade = true;
          existingPlanName = formValues.ExistingUserSubscription.SubscriptionName;
        }
      }

      // Return HTML to show in the final pane, which shows a summary and asks user to confirm the action.

      decimal selectedPlanFee = formValues.SelectedOrgSubscriptionItem.PricePerUserPerMonth;

      string selectedPlanFeeHtml = string.Empty;
      if (selectedPlanFee == 0) {
        selectedPlanFeeHtml = "Free";
      } else {
        if (availableSeats > 0) {
          selectedPlanFeeHtml += @"<span class=""confirm-fee-note"">";
          if (availableSeats == 1) {
            selectedPlanFeeHtml += $"An available seat will be used,";
          } else if (availableSeats > 0) {
            selectedPlanFeeHtml += $"One of the {availableSeats} available seats will be used,";
          }
          selectedPlanFeeHtml += @"</span>";
        } else {
          selectedPlanFeeHtml += $@"{selectedPlanFee:C}";
        }
        selectedPlanFeeHtml += @" <span class=""confirm-fee-note"">while Program and Learner are active.</span>";
        // Add an upgrade notice if the plan we're changing from is not a free one.
        if (isPlanUpgrade && formValues.ExistingUserSubscription.PricePerUserPerMonth > 0) {
          selectedPlanFeeHtml += $@"<br>The participant's existing plan will be upgraded.";
        }
      }

      string messageHtml = $@"
        <h4>Please check the following before proceeding:</h4>
        <div class=""final-summary"">";

      if (formValues.IsNewCompany) {
        messageHtml += $@"
          <label>New Client:</label>
          <div>{formValues.CompanyName.HTMLEncode()}</div>";
      } else {
        var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(formValues.CompanyId, userInfo);
        messageHtml += $@"
          <label>Client:</label>
          <div>{companyInfo?.CompanyName.HTMLEncode()}</div>";
      }

      if (formValues.IsNewProject) {
        messageHtml += $@"
          <label>New Project:</label>
          <div>{formValues.ProjectName.HTMLEncode()}</div>";
      } else {
        var projectInfo = DbHelper.Projects.GetProjectInfoOrNull(formValues.ProjectJobNumber, userInfo);
        messageHtml += $@"
          <label>Project:</label>
          <div>{projectInfo?.ProjectName.HTMLEncode()}</div>";
      }

      if (formValues.IsNewProgram) {
        messageHtml += $@"
          <label>New Program:</label>
          <div>{formValues.ProgramName.HTMLEncode()}</div>";
      } else {
        var programInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(formValues.ProgramJobId, DbHelper.AblePrograms.WhereRelatedUserIs.Tenant_AnyRelated, userInfo);
        messageHtml += $@"
          <label>Program:</label>
          <div>{programInfo?.ProgramJobName.HTMLEncode()}</div>";
      }

      messageHtml += $@"
          <label>New Learner:</label>
          <div>{(formValues.FirstName + " " + formValues.LastName).HTMLEncode()} ({formValues.EmailAddress.HTMLEncode()})</div>
          <label>Learner Plan:</label>
          <div>{selectedPlanName.HTMLEncode()}</div>
          <label>Monthly Plan Fee:</label>
          <div>{selectedPlanFeeHtml}</div>
        </div>";

      messageHtml += "</div>";

      ajax.AddReturnValue(AjaxReturnData.NextPaneHtml, messageHtml);

      return true;
    }

    private bool ValidateStep_Confirm(AjaxSubmitHelper ajax, FormValues formValues, int thisStepIndex) {

      // Called when Confirm button pressed (last panel).
      // No fields to validate on this panel.
      // Return with instruction to show the Stripe payment method popup if:
      // - plan is not free, and
      // - company doesn't have enough existing seats, and
      // - no payment method currently exists.
      // Otherwise continue on to create the new entities and assign seat to new coachee.

      var tenantOrgInfo = DbHelper.TenantOrg.GetTenantOrgById(SessionHelper.UserInfo.OrgId);

      // Ensure stripe customer exists for this org.
      StripeHelper.FindOrCreateStripeCustomerAndSubscription(
        tenantOrgInfo,
        out bool createdNewCustomer,
        out string stripeCustomerDefaultPaymentMethodId,
        out bool createdNewSubscription,
        out string stripeSubscriptionId,
        out var subscriptionItemsDto);

      // If no default payment method exists, or the existing one is invalid,
      // trigger showing the stripe payment method selector.
      if (stripeCustomerDefaultPaymentMethodId.IsNullOrEmpty()
        && OrgSubscriptionItems.Exists(s => s.PricePerUserPerMonth > 0 && s.TotalSeats > 0)) {

        ajax.AddReturnValue(AjaxReturnData.OrganisationGuidForPaymentMethod, tenantOrgInfo.OrgGuid);
        return false;
      }

      // All ok, create the new entities and assign seat to new coachee.
      // TODO: If there's a free seat in the org, use that instead of adding a new seat.

      DbHelper.AlbertCoachees.AlbertCoacheeInfo newCoachee = null;

      bool dbUpdated = DbHelper.Common.UsingTransaction(trans => {

        // Create pax plus project/program as needed.
        if (!CreateEntitiesForParticipant(ajax, trans, formValues, out newCoachee)) {
          return false; // rollback
        }

        if (SessionHelper.AppAccess.Participants.CanCreateQuoteForNewParticipant()) {
          CreateQuote(trans,
            formValues,
            newCoachee,
            out formValues.QuoteItemForSubscription);
        }

        // Update user subscription data then update org subs if required.
        DbHelper.Subscriptions.User.UpdateCoacheeSubscription(trans, newCoachee, formValues.QuoteItemForSubscription, false, true);
        DbHelper.Subscriptions.Org.UpdateOrgSubscriptionsForAssignedSeats(trans, tenantOrgInfo.OrgId);

        return true;
      });
      if (!dbUpdated) return false;

      // Reload updated org subscription info. Total and used Seats will reflect latest values.
      OrgSubscriptionItems = DbHelper.Subscriptions.Org.GetOrgSubscriptionItems(SessionHelper.UserInfo.OrgId);

      // Reload coachee to get complete object.
      newCoachee = DbHelper.AlbertCoachees.GetCoacheeInfo(newCoachee.CoacheeId);

      // Update reference in form values.
      formValues.SelectedOrgSubscriptionItem = OrgSubscriptionItems.Find(s => s.SubscriptionGuid == formValues.SubscriptionGuid);
      if (formValues.SelectedOrgSubscriptionItem == null) { // Shouldn't happen, error description provided to help debug in case.
        throw new InvalidOperationException($"SelectedOrgSubscription not found after updating OrgSubscriptions.");
      }

      // Update with existing items synced with org's current quantities.
      subscriptionItemsDto = StripeHelper.EnsureOrgSubscriptionItemsToDto(OrgSubscriptionItems, subscriptionItemsDto);
      StripeService.UpdateSubscriptionItems(tenantOrgInfo.StripeCustomerSubscriptionId, subscriptionItemsDto);

      SendEmail(ajax, formValues, newCoachee);

      try {
        SendIntercomEvents(newCoachee, formValues);
      } catch (Exception) {
        // ignore
      }

      return true;
    }

    public struct RequiredSubscriptionArgs { public bool HasNudges, HasPulse, HasAICoaching; }

    public DbHelper.Subscriptions.Org.OrgSubscriptionItem GetLowestRequiredSubscription(RequiredSubscriptionArgs args) {

      var requiredSubscriptions = new List<DbHelper.Subscriptions.Org.OrgSubscriptionItem>();

      // Create list of subscriptions that satisfy selected features.
      foreach (var sub in OrgSubscriptionItems) {
        if (args.HasNudges && !sub.HasNudges) continue;
        if (args.HasPulse && !sub.HasPulse) continue;
        if (args.HasAICoaching && !sub.HasAICoaching) continue;
        requiredSubscriptions.Add(sub);
      }

      // Return the lowest level subscription allowed.
      return requiredSubscriptions
        .OrderBy(s => s.PricePerUserPerMonth)
        .ThenBy(s => s.DisplayOrder)
        .FirstOrDefault();
    }

    public string GetCoachDropdown() {

      var coaches = DbHelper.AlbertCoaches.GetCoachInfoList(true, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);

      var dropdownInfo = new WebHelper.PartnerDropdownInfo() {
        PartnerInfoList = coaches,
        FormName = FormFields.CoachUserId,
        SelectedPartnerUserId = ConfigHelper.UserId.Unassigned,
        CanViewHiddenPartners = SessionHelper.AppAccess.Coaches.CanViewHiddenPartners(),
        CanViewInactivePartners = SessionHelper.AppAccess.Coaches.CanViewInactivePartners(),
        IncludeUnassignedUser = true,
        DropdownPurpose = WebHelper.PartnerDropdownPurpose.AssignCoachForParticipant
      };

      string dropdownOptionsHtml = WebHelper.GetPartnerDropdownOptionsHtml(dropdownInfo);

      var selectInfo = new WebHelper.SelectInfo() {
        IsReadOnly = dropdownInfo.IsReadOnly,
        InputName = dropdownInfo.FormName,
        Class = WebHelper.CSSClasses.PartnerDropdownClass,
        TopOptionsHtml = dropdownOptionsHtml
      };

      return WebHelper.GetSelect(selectInfo);
    }

    public void GetProgramOptionsHtml(AjaxSubmitHelper ajax) {

      var projectJobNumber = WebHelper.GetFormValue(FormFields.ProjectJobNumber);
      if (projectJobNumber == null) {
        ajax.AddReturnValue(AjaxReturnData.ProgramOptionsHtml, string.Empty);
        return;
      }

      var programsInProject = DbHelper.AblePrograms.GetProgramsByJobNumber(projectJobNumber);
      if (programsInProject == null || programsInProject.ProgramInfoList.IsNullOrEmpty()) {
        ajax.AddReturnValue(AjaxReturnData.ProgramOptionsHtml, string.Empty);
        return;
      }

      var optionsHtml = new StringBuilder();
      foreach (var prog in programsInProject.ProgramInfoList) {
        optionsHtml.Append($@"<option value=""{prog.ProgramJobId}"">{prog.ProgramJobName.HTMLEncode()}</option>");
      }

      ajax.AddReturnValue(AjaxReturnData.ProgramOptionsHtml, optionsHtml.ToString());
    }

    public List<WebHelper.ButtonGroupButton> GetCoachingSessionButtons() {

      var buttons = new List<WebHelper.ButtonGroupButton>();
      foreach (var ct in DbHelper.AlbertCoachingTypes.GetCoachingTypeList()) {
        if (!ct.UIHidden) {
          string buttonText = ct.IntercomFieldValue;
          if (ct.IntercomFieldValue == DbHelper.AlbertCoachingTypes.GetIntercomValue_NoCoaching()) {
            buttonText = "No Coaching";
          }
          buttons.Add(new WebHelper.ButtonGroupButton(buttonText, ct.IntercomFieldValue));
        }
      }
      return buttons;
    }

    public List<WebHelper.SelectOption> GetCompanyOptions() {

      var companies = DbHelper.ClientCompanies.GetCompanyList(SessionHelper.GetUserInfoOrNull());

      var options = new List<WebHelper.SelectOption>();
      options.Add(new WebHelper.SelectOption(PathHelper.AbleUrlValues.IdNew, "[Add New Company"));
      foreach (var cmp in companies) {
        options.Add(new WebHelper.SelectOption(cmp.CompanyId.ToString(), cmp.CompanyName));
      }
      return options;
    }

    private bool CreateEntitiesForParticipant(AjaxSubmitHelper ajax, SqlTransaction trans, FormValues formValues,
      out DbHelper.AlbertCoachees.AlbertCoacheeInfo newCoachee) {

      newCoachee = null;

      // Note if creating new entities, the new Ids are saved
      // back in formValues as if the user had selected them, so
      // each db operation can simply pass formValues on from the last one.

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

      } else if (formValues.IsNewProgram) {
        // Only create new program if not already created with new project.
        CreateProgram(trans, formValues);
      }

      newCoachee = CreateParticipant(trans, formValues);

      return true;
    }

    private bool SendEmail(AjaxSubmitHelper ajax, FormValues formValues, DbHelper.AlbertCoachees.AlbertCoacheeInfo newCoachee) {

      string msg = "Participant successfully added";

      if (formValues.SendWelcomeNow || formValues.SendMeetCoachNow) {

        ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(formValues.ProjectJobNumber);
        if (ProjectInfo != null) {

          var coachInfo = DbHelper.AlbertCoaches.GetCoachInfo(userInfo.UserId);
          bool welcomeEmailSent = false;
          bool meetCoachEmailSent = false;

          if (formValues.SendWelcomeNow) {
            welcomeEmailSent = AlbertEmails.ParticipantWelcome.Send(ProjectInfo, newCoachee, coachInfo, ProjectInfo, out EmailHelper.MandrillSentResult sendResult, AlbertEmails.ParticipantWelcome.SetSendDates.Yes);
            msg += $"<br/>Welcome Email {(welcomeEmailSent ? "" : "couldn't not be")} sent.";
          }

          if (formValues.SendMeetCoachNow) {
            meetCoachEmailSent = AlbertEmails.SendMeetCoachEmail(null, newCoachee, ProjectInfo, coachInfo);
            msg += $"<br/>Meet Coachee Email {(meetCoachEmailSent ? "" : "couldn't not be")} sent.";
          }

          if (formValues.SendWelcomeNow) {
            // Send Intercom event for coachee invitation (manual welcome email)
            var participantExternalId = ConfigHelper.UserRole.Leader.ToExternalUserId(newCoachee.UserGuid);
            if (welcomeEmailSent && participantExternalId.HasValue) {
              SendEvent(
                intercom => intercom.CoacheeInvited()
                  .FromSession()
                  .WithCoacheeEmailAddress(newCoachee.EmailAddress)
                  .WithOrganisation(newCoachee.TenantOrgId, newCoachee.OrgName),
                operationName: "AddParticipant_CoacheeInvited",
                requestRawUrl: SystemWeb.RequestRawUrl,
                telemetryProperties: new Dictionary<string, object> {
                  ["ParticipantEmail"] = newCoachee?.EmailAddress
                }
              );
            }
          }
        }
      }

      ajax.SetRedirectUrl(PathHelper.Pages.ProgramParticipants(formValues.ProgramJobId), msg, AjaxSubmitHelper.PageMessageType.SuccessDialog);
      return true;
    }

    private void CreateCompany(SqlTransaction trans, FormValues formValues) {

      // Add new company & get id.
      var newCompanyInfo = DbHelper.ClientCompanies.CreateCompanyBrief(trans, userInfo.OrgId, formValues.CompanyName, formValues.ClientLeadUserId);
      formValues.CompanyId = newCompanyInfo.CompanyId;
      formValues.CompanyOrgId = userInfo.OrgId;
    }

    private void CreateProjectAndProgram(SqlTransaction trans, FormValues formValues) {

      DbHelper.Projects.CreateProjectAndProgram(
        trans: trans,
        companyId: formValues.CompanyId,
        projectName: formValues.ProjectName,
        preferredProgramName: formValues.ProgramName,
        tenantOrgId: SessionHelper.UserInfo.OrgId, // Set new project parent org to same as the users.
        canSelfSelectCoach: false,
        createdByUserId: SessionHelper.UserInfo.UserId,
        // Save results in formValues.
        newJobNumber: out formValues.ProjectJobNumber,
        newProgramJobId: out formValues.ProgramJobId
      );
    }

    private void CreateProgram(SqlTransaction trans, FormValues formValues) {

      var newProgram = new DbHelper.AblePrograms.AbleProgramInfo() {
        ProgramJobNumber = formValues.ProjectJobNumber,
        ProgramJobName = formValues.ProgramName,
        CompanyId = formValues.CompanyId,
        LeadConsultantUserId = SessionHelper.UserInfo.UserId
      };

      DbHelper.AblePrograms.CreateProgram(trans, newProgram);

      formValues.ProgramJobId = newProgram.ProgramJobId;
    }

    private DbHelper.AlbertCoachees.AlbertCoacheeInfo CreateParticipant(SqlTransaction trans, FormValues formValues) {

      if (DbHelper.AlbertCoachees.TryGetSoftDeletedCoacheeId(trans, formValues.EmailAddress, formValues.ProgramJobId, out int softDeletedCoacheeId)) {
        // If a soft-deleted coachee exists, previous validation allows it to now be hard deleted.
        DbHelper.AlbertCoachees.HardDeleteCoachee(trans, softDeletedCoacheeId);
      }

      var coachee = new DbHelper.AlbertCoachees.AlbertCoacheeInfo();

      coachee.FirstName = formValues.FirstName;
      coachee.LastName = formValues.LastName;
      coachee.EmailAddress = formValues.EmailAddress;
      coachee.ProgramStatusId = DbHelper.CoacheeProgramStatus.GetStatus_WaitingLaunch().ProgramStatusId;
      coachee.TenantOrgId = formValues.CompanyOrgId;
      coachee.CompanyId = formValues.CompanyId;
      coachee.ProgramJobId = formValues.ProgramJobId;
      coachee.CoachUserId = userInfo.UserId; // By default set the creating user as the Coach
      coachee.SubscriptionUser = true;
      coachee.UserActivity.SessionsAllocated = formValues.SessionsAllocated;
      coachee.CoachingTypeId = formValues.CoachingTypeId;

      coachee.CoacheeId = DbHelper.AlbertCoachees.CreateCoachee(trans, coachee);

      // This is a separate update..
      coachee.UserActivity.SessionsAllocated = formValues.SessionsAllocated;
      coachee.CoachingTypeId = formValues.CoachingTypeId;
      coachee.UserSubscription = new DbHelper.Subscriptions.User.UserSubscriptionInfo();
      coachee.UserSubscription.SubscriptionId = formValues.SelectedOrgSubscriptionItem.SubscriptionId;
      coachee.UserSubscription.UserHasAICoachEnabled = formValues.EnableAICoaching;
      coachee.PulseSurveyEnabled = formValues.EnablePulse;
      coachee.DisableNudges = formValues.EnableNudges;
      coachee.WelcomeEmailUtc = formValues.WelcomeEmailUtc;
      coachee.MeetCoachEmailUtc = formValues.MeetCoachEmailUtc;
      DbHelper.AlbertCoachees.UpdateCoachee(trans, coachee);

      return coachee;
    }

    private void CreateQuote(SqlTransaction trans,
      FormValues formValues,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
      out DbHelper.AbleQuotes.QuoteItemForSubscription quoteItemForSubscription) {

      quoteItemForSubscription = null;

      var allProducts = DbHelper.Products.GetAllProducts();
      var subscriptionProductInfo = allProducts.Find(
        x => x.SubscriptionId == formValues.SelectedOrgSubscriptionItem.SubscriptionId &&
        x.ProductCategoryId == (int)DbHelper.Products.ProductCategory.Subscription);

      var newQuoteInfo = new DbHelper.AbleQuotes.NewQuoteInfo(
        jobNumber: formValues.ProjectJobNumber,
        ownerUserId: userInfo.UserId,
        leadConsultantUserId: ConfigHelper.SelfCreatedUserDefaults.ProgramData.PLC,
        proposalDesignerUserId: null,
        contactUserId: userInfo.UserId,
        quoteTitle: $"Able Quote {WebHelper.DisplayDate(DateTime.UtcNow)} - {formValues.FirstName} {formValues.LastName}",
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

      // Create quote items...

      // Coaching
      if (formValues.CoachingTypeName != DbHelper.AlbertCoachingTypes.GetIntercomValue_NoCoaching()) {

        var coachingQuoteItemId = CreateQuoteItems(trans, formValues.QuoteId, ConfigHelper.Participants.DefaultCoachingProductId, formValues.SessionsAllocated, ConfigHelper.Participants.DefaultCoachingProductDescription);

        if (coachingQuoteItemId == 0) {
          throw new Exception("Couldn't create Quote Item Id for Coaching");
        }

        // Create Session Components
        var updateComponentsInfo = new DbHelper.ProgramComponents.UpdateSessionComponentsInfo(coacheeInfo);
        // Add each session to components object
        for (int i = 1; i <= formValues.SessionsAllocated; i++) {
          updateComponentsInfo.AddSessionToUpdate(i, 0, coachingQuoteItemId);
        }
        // Create session components
        DbHelper.ProgramComponents.UpdateSessionComponents(trans, updateComponentsInfo);
      }

      // Add quote item for Subscription.

      if (subscriptionProductInfo != null && formValues.SelectedOrgSubscriptionItem.SubscriptionId != null) {

        int subscriptionQuantity = 1;
        var subscriptionQuoteItemId = CreateQuoteItems(trans, formValues.QuoteId, subscriptionProductInfo.ProductId, subscriptionQuantity, subscriptionProductInfo.ProductDescription);

        if (subscriptionQuoteItemId == 0) {
          throw new Exception("Couldn't create Quote Item for Subscription");
        }

        // Create QuoteItemForSubscription object to link created QuoteItem to subscription
        quoteItemForSubscription = new DbHelper.AbleQuotes.QuoteItemForSubscription(
          quoteItemId: subscriptionQuoteItemId,
          description: subscriptionProductInfo.ProductDescription,
          displayOrder: 1,
          unitPrice: 0,
          subscriptionId: formValues.SelectedOrgSubscriptionItem.SubscriptionId.Value,
          productTitle: subscriptionProductInfo.DisplayTitle,
          allocatedSubscriptions: 1,
          assignedSubscriptions: 0);
      }
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

    void SendIntercomEvents(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo, FormValues formValues) {

      // Reload CoacheeInfo to get the UserGuid which is needed for Intercom events
      coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(coacheeInfo.CoacheeId);

      var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(formValues.CompanyId, userInfo);
      var participantExternalId = ConfigHelper.UserRole.Leader.ToExternalUserId(coacheeInfo.UserGuid);
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
            .WithParticipant(participantExternalId.Value, coacheeInfo.EmailAddress)
            .WithProgram(programInfo?.ProgramJobId, programInfo?.ProgramJobName)
            .WithCompany(formValues.CompanyId, companyInfo?.CompanyName)
            .WithParticipantName(coacheeInfo.GetFullName()),
          operationName: "AddParticipant_ParticipantCreated",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["ParticipantEmail"] = coacheeInfo?.EmailAddress
          }
        );

        // Send Intercom event for subscription assignment
        if (formValues.QuoteItemForSubscription != null && coacheeInfo.UserSubscription != null) {
          SendEvent(
            intercom => {
              var builder = intercom.SubscriptionAssigned()
                  .FromSession()
                  .WithParticipant(participantExternalId.Value, coacheeInfo.EmailAddress)
                  .WithOrganisation(coacheeInfo.TenantOrgId, coacheeInfo.OrgName);

              if (coacheeInfo.ProgramJobId.HasValue) {
                builder.WithProject(coacheeInfo.ProgramJobId.Value, programInfo?.ProgramJobName);
              }

              return builder.WithSubscriptionDetails(
                subscriptionType: formValues.QuoteItemForSubscription.ProductTitle ?? "Unknown",
                unitPrice: formValues.QuoteItemForSubscription.UnitPrice
              );
            },
            operationName: "AddParticipant_SubscriptionAssigned",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["ParticipantEmail"] = coacheeInfo?.EmailAddress,
              ["SubscriptionType"] = formValues.QuoteItemForSubscription.ProductTitle
            }
          );
        }
      }
    }
  }
}
