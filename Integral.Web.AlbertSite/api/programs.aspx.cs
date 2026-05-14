using System;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.api {

  public partial class programs : AppCode.PageBaseClasses.LoggedInPageBase {

    protected void Page_Load(object sender, EventArgs e) {

      if (!SystemWeb.IsHttpPost) return;

      AjaxSubmitHelper.Process(ajax => {
        SubmittedForm(ajax);
      });
    }

    void SubmittedForm(AjaxSubmitHelper ajax) {

      bool isNewProgram = false;
      int newProgramJobId = 0;
      int formProgramJobId = 0;

      if (!SessionHelper.IsUserRoleAdmin) {
        ajax.AddDialogMessage("Access denied.");
        return;
      }

      if (WebHelper.GetFormValue("ProgramJobId") == "new")
        isNewProgram = true;
      else
        formProgramJobId = ajax.CheckFieldID("ProgramJobId", "Program", true, "ProgramJobId is required.");

      if (!isNewProgram) {
        ajax.AddDialogMessage("Can only create new Programs for now.");
        return;
      }

      DbHelper.AblePrograms.AbleProgramInfo programInfo = null;
      int companyId = ajax.CheckFieldID("CompanyId", "Company", isNewProgram, "Please provide the CompanyId for the Program");
      string programJobNumber = ajax.CheckFieldRegex("ProgramNumber", "Program Number", AppHelper.Regex.GeneralText, isNewProgram, "Please provide the Program Number.").TrimWhitespace();
      string programName = ajax.CheckFieldRegex("ProgramName", "Program Name", AppHelper.Regex.GeneralText, isNewProgram, "Please provide the Program Name.").TrimWhitespace();
      DateTime? programStartDateUtc = ajax.GetDatePickerToUtc("ProgramStartDate", SessionHelper.GetSessionTimeZone(), "Program Start Date", true, "Please provide Program start date.");
      DateTime? programEndDateUtc = ajax.GetDatePickerToUtc("ProgramEndDate", SessionHelper.GetSessionTimeZone(), "Program End Date", true, "Please provide Program end date.");
      string programNotes = ajax.CheckFieldRegex("ProgramNotes", "Program Notes", AppHelper.Regex.GeneralText.Replace("[", "[<>"), false, "").TrimWhitespace();

      if (ajax.BadFieldCount > 0) return;

      // Ensure company ID exists.
      var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(companyId, SessionHelper.GetUserInfoOrNull());
      if (companyInfo == null) {
        ajax.AddBadField("CompanyId", "CompanyId does not exist.");
        return;
      }

      // Add new program.
      try {
        programInfo = DbHelper.AblePrograms.CreateProgram(null,
          companyId,
          programJobNumber, programName,
          programStartDateUtc, programEndDateUtc,
          programNotes);
        newProgramJobId = programInfo.ProgramJobId;
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("API_Programs_CreateProgram")
          .WithProperty(ApplicationInsightsConstants.ProgramJobNumber, programJobNumber)
          .WithProperty(ApplicationInsightsConstants.CompanyId, companyId)
          .Track();
        if (ex.Message.Contains("duplicate key")) {
          ajax.AddDialogMessage("That Program Number already exists.");
        } else {
          ajax.AddDialogMessage("Error creating Program: " + ex.Message);
        }
        return;
      }

      if (isNewProgram) ajax.AddReturnValue("NewProgramJobId", newProgramJobId.ToString());

    }

  }
}
