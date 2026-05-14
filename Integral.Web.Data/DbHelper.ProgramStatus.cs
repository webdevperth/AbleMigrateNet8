using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AlbertProgramStatus {


      private static List<ProgramStatusInfo> programStatusList;
      private static Dictionary<int, ProgramStatusInfo> programStatusById;

      private static ProgramStatusInfo _status_Setup = null;
      private static ProgramStatusInfo _status_Active = null;
      private static ProgramStatusInfo _status_Complete = null;
      private static ProgramStatusInfo _status_ToBeClosed = null;
      private static ProgramStatusInfo _status_Closed = null;

      public static class Ids {
        public static int Setup { get; internal set; } = _status_Setup.ProgramStatusId;
        public static int Active { get; internal set; } = _status_Active.ProgramStatusId;
        public static int Complete { get; internal set; } = _status_Complete.ProgramStatusId;
        public static int ToBeClosed { get; internal set; } = _status_ToBeClosed.ProgramStatusId;
        public static int Closed { get; internal set; } = _status_Closed.ProgramStatusId;
      }
      public static class Statuses {
        public static ProgramStatusInfo Setup { get; internal set; } = _status_Setup;
        public static ProgramStatusInfo Active { get; internal set; } = _status_Active;
        public static ProgramStatusInfo Complete { get; internal set; } = _status_Complete;
        public static ProgramStatusInfo ToBeClosed { get; internal set; } = _status_ToBeClosed;
        public static ProgramStatusInfo Closed { get; internal set; } = _status_Closed;
      }

      static AlbertProgramStatus() {


        RefreshProgramStatusNames();

      }

      static void RefreshProgramStatusNames() {

        // TODO: Thread safety lock in case we need to call this while running to refresh the list.

        programStatusList = new List<ProgramStatusInfo>();
        programStatusById = new Dictionary<int, ProgramStatusInfo>();

        AddProgramStatusFromDb();

      }

      static void AddProgramStatusFromDb() {

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          string sql = @"

          SELECT ProgramStatusId, DisplayTitle
          FROM al_ProgramStatus
          ORDER BY DisplaySortOrder, DisplayTitle";

          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@id", 0);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                int programStatusId = dr.GetInt("ProgramStatusId");
                string displayTitle = dr.GetString("DisplayTitle");
                var ps = new ProgramStatusInfo(programStatusId, displayTitle);
                programStatusList.Add(ps);
                programStatusById.Add(programStatusId, ps);

                if (ps.DisplayTitle.ToLower().Contains("setup")) {
                  if (_status_Setup == null) {
                    _status_Setup = ps;
                  } else {
                    throw new ApplicationException("There is more than one ProgramStatus containing string 'Setup'");
                  }
                } else if (ps.DisplayTitle == "Complete") {
                  if (_status_Complete == null) {
                    _status_Complete = ps;
                  } else {
                    throw new ApplicationException("There is more than one 'Complete' ProgramStatus.");
                  }
                } else if (ps.DisplayTitle == "To Be Closed") {
                  if (_status_ToBeClosed == null) {
                    _status_ToBeClosed = ps;
                  } else {
                    throw new ApplicationException("There is more than one 'To Be Closed' ProgramStatus.");
                  }
                } else if (ps.DisplayTitle == "Closed") {
                  if (_status_Closed == null) {
                    _status_Closed = ps;
                  } else {
                    throw new ApplicationException("There is more than one 'Closed' ProgramStatus.");
                  }
                } else if (ps.DisplayTitle.ToLower().Contains("active")) {
                  if (_status_Active == null) {
                    _status_Active = ps;
                  } else {
                    throw new ApplicationException("There is more than one ProgramStatus containing string 'Active'");
                  }
                }
              }
            }
          }
          if (_status_Setup == null) throw new ApplicationException("ProgramStatus 'Setup' not found.");
          else if (_status_Active == null) throw new ApplicationException("ProgramStatus 'Active' not found.");
          else if (_status_Complete == null) throw new ApplicationException("ProgramStatus 'Complete' not found.");
          else if (_status_ToBeClosed == null) throw new ApplicationException("ProgramStatus 'To Be Closed' not found.");
          else if (_status_Closed == null) throw new ApplicationException("ProgramStatus 'Closed' not found.");
        }
      }

      public static List<ProgramStatusInfo> GetProgramStatusList() {
        return programStatusList;
      }

      public static ProgramStatusInfo GetProgramStatusByIdOrNull(int? programStatusId) {
        if (programStatusId == null || !programStatusById.ContainsKey((int)programStatusId)) return null;
        return programStatusById[(int)programStatusId];
      }
      public static ProgramStatusInfo GetProgramStatusByIdOrZeroes(int? programStatusId) {
        var ps = GetProgramStatusByIdOrNull(programStatusId);
        if (ps == null) return new ProgramStatusInfo(0, "");
        return ps;
      }
      public static ProgramStatusInfo GetProgramStatusById(int programStatusId) {
        var ps = GetProgramStatusByIdOrNull(programStatusId);
        if (ps == null) throw new ArgumentException("Invalid ProgramStatusId: " + programStatusId);
        return ps;
      }

      public static string GetDisplayTitleOrNull(int? programStatusId) {
        if (programStatusId == null) return null;
        if (!programStatusById.ContainsKey((int)programStatusId)) throw new ArgumentException("programStatusId not found: " + programStatusId);
        return programStatusById[(int)programStatusId].DisplayTitle;
      }

      public class ProgramStatusInfo {
        public int ProgramStatusId { get; private set; }
        public string DisplayTitle { get; private set; }
        public ProgramStatusInfo(int programStatusId, string displayTitle) {
          this.ProgramStatusId = programStatusId;
          this.DisplayTitle = displayTitle;
        }
      }

    }
  }
}

