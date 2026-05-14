using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class CoacheeProgramStatus {


      private static List<ProgramStatusInfo> programStatusList;
      private static Dictionary<int, ProgramStatusInfo> programStatusById;
      private static List<string> intercomValueList;
      private static string intercomValue_NA = "";
      private static ProgramStatusInfo programStatus_WaitingLaunch = null;
      private static ProgramStatusInfo programStatus_Onboarding = null;
      private static ProgramStatusInfo programStatus_ActiveProgram = null;
      private static ProgramStatusInfo programStatus_EndProgram = null;
      private static ProgramStatusInfo programStatus_Paused = null;

      public static class Ids {
        public static int WaitingLaunch => programStatus_WaitingLaunch.ProgramStatusId;
        public static int Onboarding => programStatus_Onboarding.ProgramStatusId;
        public static int Active => programStatus_ActiveProgram.ProgramStatusId;
        public static int EndProgram => programStatus_EndProgram.ProgramStatusId;
        public static int Paused => programStatus_Paused.ProgramStatusId;
      }

      static CoacheeProgramStatus() {


        RefreshProgramStatusNames();

      }

      static void RefreshProgramStatusNames() {

        // TODO: Thread safety lock in case we need to call this while running to refresh the list.

        programStatusList = new List<ProgramStatusInfo>();
        programStatusById = new Dictionary<int, ProgramStatusInfo>();
        intercomValueList = new List<string>();

        AddProgramStatusFromDb();

      }

      static void AddProgramStatusFromDb() {

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          string sql = @"

          SELECT ProgramStatusId, IntercomFieldValue, DisplayTitle
          FROM al_CoacheeProgramStatus
          ORDER BY DisplaySortOrder, DisplayTitle";

          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@id", 0);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                int programStatusId = dr.GetInt("ProgramStatusId");
                string programStatusName = dr.GetString("IntercomFieldValue");
                string displayTitle = dr.GetString("DisplayTitle");
                var ps = new ProgramStatusInfo(programStatusId, programStatusName, displayTitle);
                programStatusList.Add(ps);
                programStatusById.Add(programStatusId, ps);
                intercomValueList.Add(ps.IntercomFieldValue);
                if (intercomValue_NA.IsNullOrEmpty()) intercomValue_NA = ps.IntercomFieldValue; // First value should be "n/a".

                // Assign specific values to remember.
                if (ps.IntercomFieldValue.Contains("Waiting")) {
                  // "Waiting Launch" value.
                  if (programStatus_WaitingLaunch == null) programStatus_WaitingLaunch = ps;
                  else throw new ApplicationException("There is more than one ProgramStatus containing string 'Waiting'");
                } else if (ps.IntercomFieldValue.Contains("Onboarding")) {
                  // "Onboarding" value.
                  if (programStatus_Onboarding == null) programStatus_Onboarding = ps;
                  else throw new ApplicationException("There is more than one ProgramStatus containing string 'Onboarding'");
                } else if (ps.IntercomFieldValue.Contains("Active")) {
                  // "Active Program" value.
                  if (programStatus_ActiveProgram == null) programStatus_ActiveProgram = ps;
                  else throw new ApplicationException("There is more than one ProgramStatus containing string 'Active'");
                } else if (ps.IntercomFieldValue.Contains("End-Program")) {
                  // "End Program" value.
                  if (programStatus_EndProgram == null) programStatus_EndProgram = ps;
                  else throw new ApplicationException("There is more than one ProgramStatus containing string 'End-Program'");
                } else if (ps.IntercomFieldValue.Contains("Paused")) {
                  // "Paused" value.
                  if (programStatus_Paused == null) programStatus_Paused = ps;
                  else throw new ApplicationException("There is more than one ProgramStatus containing string 'Paused'");
                }
              }
            }
          }
          if (programStatus_WaitingLaunch == null) throw new ApplicationException("ProgramStatus 'Waiting Launch' not found.");
          if (programStatus_Onboarding == null) throw new ApplicationException("ProgramStatus 'Onboarding' not found.");
          if (programStatus_ActiveProgram == null) throw new ApplicationException("ProgramStatus 'Active Program' not found.");
          if (programStatus_EndProgram == null) throw new ApplicationException("ProgramStatus 'End-Program' not found.");
          if (programStatus_Paused == null) throw new ApplicationException("ProgramStatus 'Paused' not found.");
        }
      }

      public static List<ProgramStatusInfo> GetProgramStatusList() {
        return programStatusList;
      }

      public static List<string> GetIntercomValueList() {
        return intercomValueList;
      }

      public static string GetIntercomValue_NA() {
        return intercomValue_NA;
      }

      public static ProgramStatusInfo GetStatus_Onboarding() {
        return programStatus_Onboarding;
      }

      public static ProgramStatusInfo GetStatus_ActiveProgram() {
        return programStatus_ActiveProgram;
      }

      public static ProgramStatusInfo GetStatus_WaitingLaunch() {
        return programStatus_WaitingLaunch;
      }

      public static ProgramStatusInfo GetStatus_EndProgram() {
        return programStatus_EndProgram;
      }

      public static ProgramStatusInfo GetStatus_Paused() {
        return programStatus_Paused;
      }

      public static ProgramStatusInfo GetProgramStatusByIdOrNull(int? programStatusId) {
        if (programStatusId == null || !programStatusById.ContainsKey((int)programStatusId)) return null;
        return programStatusById[(int)programStatusId];
      }
      public static ProgramStatusInfo GetProgramStatusByIdOrZeroes(int? programStatusId) {
        var ps = GetProgramStatusByIdOrNull(programStatusId);
        if (ps == null) return new ProgramStatusInfo(0, "", "");
        return ps;
      }
      public static ProgramStatusInfo GetProgramStatusById(int programStatusId) {
        var ps = GetProgramStatusByIdOrNull(programStatusId);
        if (ps == null) throw new ArgumentException("Invalid ProgramStatusId: " + programStatusId);
        return ps;
      }

      public static ProgramStatusInfo GetProgramStatusIdByNameOrNull(string intercomFieldValue) {
        if (!intercomFieldValue.IsNullOrEmpty()) {
          foreach (var ps in programStatusList) {
            if (intercomFieldValue.Equals(ps.IntercomFieldValue, StringComparison.InvariantCultureIgnoreCase)) return ps;
          }
        }
        return null;
      }

      public static string GetDisplayTitleOrNull(int? programStatusId) {
        if (programStatusId == null) return null;
        if (!programStatusById.ContainsKey((int)programStatusId)) throw new ArgumentException("programStatusId not found: " + programStatusId);
        return programStatusById[(int)programStatusId].DisplayTitle;
      }

      public class ProgramStatusInfo {
        public int ProgramStatusId { get; private set; }
        public string IntercomFieldValue { get; private set; }
        public string DisplayTitle { get; private set; }
        public ProgramStatusInfo(int programStatusId, string intercomFieldValue, string displayTitle) {
          this.ProgramStatusId = programStatusId;
          this.IntercomFieldValue = intercomFieldValue;
          this.DisplayTitle = displayTitle;
        }
      }

    }
  }
}

