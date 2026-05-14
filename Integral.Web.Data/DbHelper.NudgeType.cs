using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AlbertNudgeType {


      private static List<NudgeTypeInfo> nudgeTypeList;
      private static Dictionary<int, NudgeTypeInfo> nudgeTypeById;
      public static NudgeTypeInfo NudgeType_Coaching { get; private set; }
      public static NudgeTypeInfo NudgeType_Workshop { get; private set; }
      public static NudgeTypeInfo NudgeType_Program { get; private set; }
      const string NudgeType_Name_Coaching = "coaching";
      const string NudgeType_Name_Workshop = "workshop";
      const string NudgeType_Name_Program = "program";

      static AlbertNudgeType() {
        ReadRows();
      }

      static void ReadRows() {

        // TODO: Thread safety lock in case we need to call this while running to refresh the list.

        nudgeTypeList = new List<NudgeTypeInfo>();
        nudgeTypeById = new Dictionary<int, NudgeTypeInfo>();
        NudgeType_Coaching = null;
        NudgeType_Workshop = null;
        NudgeType_Program = null;

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {

          string sql = "SELECT NudgeTypeId, NudgeTypeName FROM al_NudgeType";

          using (var cmd = new SqlCommand(sql, conn)) {
            conn.Open();
            try {
              using (SqlDataReader dr = cmd.ExecuteReader()) {
                while (dr.Read()) {
                  var nt = new NudgeTypeInfo(
                    dr.GetInt("NudgeTypeId"),
                    dr.GetString("NudgeTypeName")
                  );
                  if (nt.NudgeTypeName.ToLower().Contains(NudgeType_Name_Coaching)) {
                    if (NudgeType_Coaching != null) throw new ApplicationException($"NudgeType Name '{NudgeType_Name_Coaching}' found twice.");
                    NudgeType_Coaching = nt;
                  } else if (nt.NudgeTypeName.ToLower().Contains(NudgeType_Name_Workshop)) {
                    if (NudgeType_Workshop != null) throw new ApplicationException($"NudgeType Name '{NudgeType_Name_Workshop}' found twice.");
                    NudgeType_Workshop = nt;
                  } else if (nt.NudgeTypeName.ToLower().Contains(NudgeType_Name_Program)) {
                    if (NudgeType_Program != null) throw new ApplicationException($"NudgeType Name '{NudgeType_Name_Program}' found twice.");
                    NudgeType_Program = nt;
                  }
                  nudgeTypeList.Add(nt);
                  nudgeTypeById.Add(nt.NudgeTypeId, nt);
                }
                if (NudgeType_Coaching == null) throw new ApplicationException($"NudgeType '{NudgeType_Name_Coaching}' not found.");
                if (NudgeType_Workshop == null) throw new ApplicationException($"NudgeType '{NudgeType_Name_Workshop}' not found.");
                if (NudgeType_Program == null) throw new ApplicationException($"NudgeType '{NudgeType_Name_Program}' not found.");
              }
            } catch (Exception ex) {
              var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
              telemetry?.Exception(ex)
                .WithOperation(nameof(ReadRows))
                .WithOperationContext("AlbertNudgeType")
                .Track();

              throw ex;
            }
          }
        }
      }

      public static List<NudgeTypeInfo> GetNudgeTypeList() {
        return nudgeTypeList;
      }

      public static NudgeTypeInfo GetNudgeTypeByIdOrNull(int? nudgeTypeId) {
        if (nudgeTypeId == null || !nudgeTypeById.ContainsKey((int)nudgeTypeId)) return null;
        return nudgeTypeById[(int)nudgeTypeId];
      }

      public class NudgeTypeInfo {

        public int NudgeTypeId { get; private set; }
        public string NudgeTypeName { get; private set; }

        public NudgeTypeInfo (
          int nudgeTypeId,
          string nudgeTypeName
        ) {
          this.NudgeTypeId = nudgeTypeId;
          this.NudgeTypeName = nudgeTypeName;
        }
      }

    }
  }
}

