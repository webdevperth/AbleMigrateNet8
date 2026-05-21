using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ProgramInformation : AppCode.PageBaseClasses.ProgramPageBase {

    public List<DbHelper.AblePrograms.ProgramTeamMember> ProgramTeamMembers;
    public DbHelper.AblePrograms.ProgramNotesInfo ProgramNotesInfo;

    public class FormFields {
      public const string ProgramJobId = "ProgramJobId";
      public const string ProgramJobNumber = "JobNumber";
      public const string ProgramJobName = "JobName";
      public const string ProjectCoordinatorUserId = "ProjectCoordinatorUserId";
      public const string LeadConsultantUserId = "LeadConsultantUserId";
    }

    class FormValues {
      public string ProgramJobNumber;
      public string ProgramJobName;
      public int? ProjectCoordinatorUserId;
      public int? LeadConsultantUserId;
    }

    public class TabName {
      public const string Team = "team";
      public const string Notes = "notes";
    }


    public IActionResult OnGet() {

      this.PageTitle = "Program Information";
      this.PageSubtitle = "";

      ProgramTeamMembers = DbHelper.AblePrograms.GetProgramTeamMembers(ProgramInfo.ProgramJobId);
      ProgramNotesInfo = DbHelper.AblePrograms.GetProgramNotes(ProgramInfo.ProgramJobId);

      return Page();
    }

    public string GetNotesHtml() {

      if (ProgramNotesInfo == null) return "";

      string html = "";
      if (!ProgramNotesInfo.ProgramAISummary.IsNullOrEmpty()) {
        html += GetNoteItemHtml("AI Summary", ProgramNotesInfo.ProgramAISummary, "ai-summary-box");
      }
      html += GetNoteItemHtml("Project Intent", ProgramNotesInfo.ProjectIntent);
      html += GetNoteItemHtml("Program Context", ProgramNotesInfo.ProgramContext);
      html += GetNoteItemHtml("Program Notes", ProgramNotesInfo.ProgramNotes);

      return html;
    }

    public string GetNoteItemHtml(string noteTitle, string noteHtml, string extraClasses = "") {

      return $@"
        <div class=""boxBorder {extraClasses}"">
          <div class=""boxTitle""><h5 class=""mt0 mb0"">{noteTitle.HTMLEncode()}</h5></div>
          <div class=""boxBody"">{noteHtml}</div>
        </div>";
    }

    public string GetTeamCardsHtml() {

      var html = new StringBuilder();

      foreach (var teamMember in ProgramTeamMembers) {
        html.Append(WebHelper.GetProgramTeamMemberCard(teamMember));
      }

      return html.ToString();
    }

  }
}
