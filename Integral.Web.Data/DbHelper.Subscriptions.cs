using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Subscriptions {

      private static string GetSubscriptionInfoSelectionSQL(string tableAlias) {
        return $@"{tableAlias}.SubscriptionId, {tableAlias}.SubscriptionGuid, {tableAlias}.SubscriptionName,
          {tableAlias}.SubscriptionDescription, {tableAlias}.SubscriptionDisplayOrder,
          {tableAlias}.HasNudges, {tableAlias}.HasMicrolearnings, {tableAlias}.HasAICoaching,
          {tableAlias}.HasPulse, {tableAlias}.HasDevelopmentPlan, {tableAlias}.HasSurveyBasic,
          {tableAlias}.HasSurveySimple, {tableAlias}.HasSurveyAll, {tableAlias}.HasSurveyAdvancedNorms,
          {tableAlias}.IsDisabled, {tableAlias}.PricePerUserPerMonth, {tableAlias}.FeatureListText,
          {tableAlias}.StripeProductPriceId{TestColumnSuffix}";
      }

      // Gets a list of all subscriptions.
      public static List<SubscriptionInfo> GetAllSubscriptions() {
        return GetSubscriptionList(null);
      }

      // Gets a single subscription.
      public static SubscriptionInfo GetSubscriptionInfo(int subscriptionId) {
        var list = GetSubscriptionList(subscriptionId);
        if (list.IsNullOrEmpty()) return null;
        return list[0];
      }

      private static List<SubscriptionInfo> GetSubscriptionList(int? subscriptionIdOrNullforAll) {

        var subscriptions = new List<SubscriptionInfo>();

        Common.Query($@"
          SELECT
            {GetSubscriptionInfoSelectionSQL("sb")}
          FROM al_Subscription sb
          WHERE (@SubscriptionId IS NULL OR sb.SubscriptionId = @SubscriptionId)
            AND IsDisabled = 0
          ORDER BY sb.SubscriptionDisplayOrder",
          dr => {
            subscriptions.Add(new SubscriptionInfo(dr));
          },
          Common.NewSqlParameter("SubscriptionId", subscriptionIdOrNullforAll)
        );
        return subscriptions;
      }

      public static SubscriptionInfo GetSubscriptionInfo(Guid subscriptionGuid) {

        SubscriptionInfo subscriptionInfo = null;

        Common.Query($@"
          SELECT
            {GetSubscriptionInfoSelectionSQL("sb")}
          FROM al_Subscription sb
          WHERE sb.SubscriptionGuid = @SubscriptionGuid",
          dr => {
            subscriptionInfo = new SubscriptionInfo(dr);
          },
          Common.NewSqlParameter("SubscriptionGuid", subscriptionGuid)
        );
        return subscriptionInfo;
      }

      public class SubscriptionBase {
        public int? SubscriptionId { get; set; } // TODO fix. Instead of assigning id, new up the object by id from cached subscriptions at app start.
        public Guid SubscriptionGuid { get; protected set; }
        public string SubscriptionName { get; protected set; }
        public bool HasNudges { get; protected set; }
        public bool HasAICoaching { get; protected set; }
        public bool HasPulse { get; protected set; }
        public decimal PricePerUserPerMonth { get; protected set; }
        public int DisplayOrder { get; protected set; }
        public string StripeProductPriceId { get; protected set; }
      }

      public class SubscriptionInfo : SubscriptionBase {

        public string SubscriptionDescription { get; private set; }
        public bool HasMicrolearnings { get; private set; }
        public bool HasSurveyBasic { get; private set; }
        public bool HasSurveySimple { get; private set; }
        public bool HasSurveyAll { get; private set; }
        public bool HasSurveyAdvancedNorms { get; private set; }
        public bool HasDevelopmentPlan { get; private set; }
        public bool IsDisabled { get; private set; }
        public string FeatureListText { get; private set; }

        public SubscriptionInfo() { }

        public SubscriptionInfo(SqlDataReader dr) {
          AssignSubscriptionInfo(dr);
        }

        internal void AssignSubscriptionInfo(SqlDataReader dr) {

          this.SubscriptionId = dr.GetIntOrNull("SubscriptionId");
          this.SubscriptionGuid = dr.GetGuid("SubscriptionGuid");
          this.SubscriptionName = dr.GetString("SubscriptionName", true);
          this.SubscriptionDescription = dr.GetString("SubscriptionDescription", true);
          this.DisplayOrder = dr.GetInt("SubscriptionDisplayOrder");
          this.HasNudges = dr.GetBoolFromInt("HasNudges");
          this.HasPulse = dr.GetBoolFromInt("HasPulse");
          this.HasAICoaching = dr.GetBoolFromInt("HasAICoaching");
          this.HasMicrolearnings = dr.GetBoolFromInt("HasMicrolearnings");
          this.HasSurveyBasic = dr.GetBoolFromInt("HasSurveyBasic");
          this.HasSurveySimple = dr.GetBoolFromInt("HasSurveySimple");
          this.HasSurveyAll = dr.GetBoolFromInt("HasSurveyAll");
          this.HasSurveyAdvancedNorms = dr.GetBoolFromInt("HasSurveyAdvancedNorms");
          this.HasDevelopmentPlan = dr.GetBoolFromInt("HasDevelopmentPlan");
          this.PricePerUserPerMonth = dr.GetDecimal("PricePerUserPerMonth");
          this.IsDisabled = dr.GetBoolFromInt("IsDisabled");
          this.StripeProductPriceId = dr.GetString($"StripeProductPriceId{TestColumnSuffix}");
          this.FeatureListText = dr.GetString("FeatureListText");
        }
      }
    }
  }
}
