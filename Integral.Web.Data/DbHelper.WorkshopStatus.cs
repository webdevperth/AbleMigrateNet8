using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class WorkshopStatus {


      private static List<WorkshopStatusInfo> workshopStatusList;
      private static Dictionary<int, WorkshopStatusInfo> workshopStatusById;
      private static Dictionary<string, WorkshopStatusInfo> workshopStatusByName;
      public static WorkshopStatusInfo WorkshopStatus_NotPlanned { get; private set; }
      public static WorkshopStatusInfo WorkshopStatus_Estimated { get; private set; }
      public static WorkshopStatusInfo WorkshopStatus_Confirmed { get; private set; }
      public static WorkshopStatusInfo WorkshopStatus_Postponed { get; private set; }
      public static WorkshopStatusInfo WorkshopStatus_Cancelled { get; private set; }

      // Known Ids from the db.
      public static class Ids {
        public const int Confirmed = 1;
        public const int Estimated = 2;
        public const int NotPlanned = 3;
        public const int Cancelled = 4;
        public const int Postponed = 5;
      }

      static WorkshopStatus() {
        ReadRows();
      }

      static void ReadRows() {

        // TODO: Thread safety lock in case we need to call this while running to refresh the list.

        workshopStatusList = new List<WorkshopStatusInfo>();
        workshopStatusById = new Dictionary<int, WorkshopStatusInfo>();
        workshopStatusByName = new Dictionary<string, WorkshopStatusInfo>(StringComparer.OrdinalIgnoreCase);

        WorkshopStatus_NotPlanned = null;
        WorkshopStatus_Estimated = null;
        WorkshopStatus_Confirmed = null;
        WorkshopStatus_Postponed = null;
        WorkshopStatus_Cancelled = null;

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {

          string sql = @"
            SELECT WorkshopStatusId, WorkshopStatusName, IncludeInProjectInvoice, IsPaidOut
            FROM ev_WorkshopStatus";

          using (var cmd = new SqlCommand(sql, conn)) {
            conn.Open();
            try {
              using (SqlDataReader dr = cmd.ExecuteReader()) {
                while (dr.Read()) {
                  var ct = new WorkshopStatusInfo(
                    dr.GetInt("WorkshopStatusId"),
                    dr.GetString("WorkshopStatusName"),
                    dr.GetBoolFromInt("IncludeInProjectInvoice"),
                    dr.GetBoolFromInt("IsPaidOut"),
                    false, false
                  );
                  if (ct.WorkshopStatusId == Ids.NotPlanned) {
                    if (WorkshopStatus_NotPlanned != null) throw new ApplicationException("Workshop Status 'NotPlanned' found twice.");
                    ct.AllowBlankDate = true;
                    WorkshopStatus_NotPlanned = ct;
                  } else if (ct.WorkshopStatusId == Ids.Estimated) {
                    if (WorkshopStatus_Estimated != null) throw new ApplicationException("Workshop Status 'Estimated' found twice.");
                    ct.IsEstimated = true;
                    WorkshopStatus_Estimated = ct;
                  } else if (ct.WorkshopStatusId == Ids.Confirmed) {
                    if (WorkshopStatus_Confirmed != null) throw new ApplicationException("Workshop Status 'Confirmed' found twice.");
                    WorkshopStatus_Confirmed = ct;
                  } else if (ct.WorkshopStatusId == Ids.Postponed) {
                    if (WorkshopStatus_Postponed != null) throw new ApplicationException("Workshop Status 'Postponed' found twice.");
                    WorkshopStatus_Postponed = ct;
                  } else if (ct.WorkshopStatusId == Ids.Cancelled) {
                    if (WorkshopStatus_Cancelled != null) throw new ApplicationException("Workshop Status 'Cancelled' found twice.");
                    WorkshopStatus_Cancelled = ct;
                  }
                  workshopStatusList.Add(ct);
                  workshopStatusById.Add(ct.WorkshopStatusId, ct);
                  workshopStatusByName.Add(ct.WorkshopStatusName, ct);
                }
                if (WorkshopStatus_NotPlanned == null) throw new ApplicationException("Workshop status 'NotPlanned' not found.");
                if (WorkshopStatus_Estimated == null) throw new ApplicationException("Workshop status 'Estimated' not found.");
                if (WorkshopStatus_Confirmed == null) throw new ApplicationException("Workshop status 'Confirmed' not found.");
                if (WorkshopStatus_Postponed == null) throw new ApplicationException("Workshop status 'Postponed' not found.");
                if (WorkshopStatus_Cancelled == null) throw new ApplicationException("Workshop status 'Cancelled' not found.");
              }
            } catch (Exception ex) {
              var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
              telemetry?.Exception(ex)
                .WithOperation(nameof(ReadRows))
                .WithOperationContext("WorkshopStatus")
                .Track();

              throw ex;
            }
          }
        }
      }

      public static List<WorkshopStatusInfo> GetWorkshopStatusList() {
        return workshopStatusList;
      }

      public static WorkshopStatusInfo GetWorkshopStatusByIdOrNull(int? workshopStatusId) {
        if (workshopStatusId == null || !workshopStatusById.ContainsKey((int)workshopStatusId)) return null;
        return workshopStatusById[(int)workshopStatusId];
      }

      public static WorkshopStatusInfo GetWorkshopStatusByName(string workshopStatusName) {
        if (workshopStatusName == null || !workshopStatusByName.ContainsKey(workshopStatusName)) return null;
        return workshopStatusByName[workshopStatusName];
      }

      public class WorkshopStatusInfo {

        public int WorkshopStatusId { get; private set; }
        public string WorkshopStatusName { get; private set; }
        public bool IncludeInProjectInvoice { get; private set; }
        public bool IsPaidOut { get; private set; }
        public bool AllowBlankDate { get; internal set; }
        public bool IsEstimated { get; internal set; }

        public WorkshopStatusInfo(
          int workshopStatusId,
          string workshopStatusName,
          bool includeInProjectInvoice,
          bool isPaidOut,
          bool allowBlankDate,
          bool isEstimated
        ) {
          this.WorkshopStatusId = workshopStatusId;
          this.WorkshopStatusName = workshopStatusName;
          this.IncludeInProjectInvoice = includeInProjectInvoice;
          this.IsPaidOut = isPaidOut;
          this.AllowBlankDate = allowBlankDate;
          this.IsEstimated = isEstimated;
        }
      }

    }
  }
}

