using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Subscriptions {

      public class User {

        private static string GetUserSubscriptionInfoSelectionSQL(string tableAlias) {
          return $@"{tableAlias}.UserSubscriptionId, {tableAlias}.UserId, {tableAlias}.CreatedUTC, {tableAlias}.SubscriptionStartUtc, {tableAlias}.SubscriptionEndUtc,
          {tableAlias}.AutoRenew, {tableAlias}.HasAICoachEnabled";
        }

        internal static string GetSubscriptionOuterApplySelectionSQL => $@"
          {GetUserSubscriptionInfoSelectionSQL("us")},
          {GetSubscriptionInfoSelectionSQL("us")},
          us.QuoteItemId";

        private static string GetUserSubscriptionSelectSql(
          string topSql,
          string whereClause,
          string extraJoins,
          string sqlOrderByColumns) {
          return $@"
          SELECT {topSql}
              {GetUserSubscriptionInfoSelectionSQL("us")},
              {GetSubscriptionInfoSelectionSQL("s")},
              comp.QuoteItemId
            FROM al_UserSubscription us
            INNER JOIN al_Subscription s ON s.SubscriptionId = us.SubscriptionId
            LEFT JOIN al_Component comp ON comp.UserSubscriptionId = us.UserSubscriptionId
            {extraJoins.EmptyIfNull()}
            {whereClause.EnsureStartsWith(" WHERE ", true).EmptyIfNull()}
            ORDER BY us.UserSubscriptionId DESC {sqlOrderByColumns.EnsureStartsWith(", ", true).EmptyIfNull()}";
        }

        internal static string GetUserSubscriptionOuterApplySQL(string userIdTable) {
          return $@"
          OUTER APPLY (
            {(GetUserSubscriptionSelectSql(
                  topSql: "TOP 1",
                  whereClause: $@"us.UserId = {userIdTable}.UserId AND CAST(us.SubscriptionEndUtc AS date) >= GETUTCDATE()",
                  extraJoins: "",
                  sqlOrderByColumns: ""))}
          ) AS us";
        }

        public static void CreateDefaultSubscriptionForCoachee(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo, bool updateIfExists) {

          if (!coacheeInfo.HasSubscription) {
            coacheeInfo.UserSubscription = new DbHelper.Subscriptions.User.UserSubscriptionInfo();
          }

          coacheeInfo.UserSubscription.SubscriptionId = ConfigHelper.SubscriptionId.FoundationFree;
          UpdateCoacheeSubscription(null, coacheeInfo, null, updateIfExists); // It will validate if the user has an on-going sub, if not, it will create a dafault one.
        }

        public static int CreateUserSubscription(SqlTransaction trans, DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

          return Common.GetScalarQueryInt(trans, @"
          INSERT INTO al_UserSubscription (UserId, SubscriptionId, CreatedUTC, SubscriptionStartUtc, SubscriptionEndUtc, AutoRenew)
          OUTPUT INSERTED.UserSubscriptionId
          VALUES (@UserId, @SubscriptionId, @CreatedUTC, @SubscriptionStartUtc, @SubscriptionEndUtc, @AutoRenew)",
            Common.NewSqlParameter("@UserId", coacheeInfo.UserId),
            Common.NewSqlParameter("@SubscriptionId", coacheeInfo.UserSubscription.SubscriptionId),
            Common.NewSqlParameter("@CreatedUTC", DateTime.UtcNow),
            Common.NewSqlParameter("@SubscriptionStartUtc", DateTime.UtcNow),
            Common.NewSqlParameter("@SubscriptionEndUtc", DateTime.UtcNow.AddYears(1)),
            Common.NewSqlParameter("@AutoRenew", 1)
          );
        }

        public static bool UpdateUserSubscription(SqlTransaction trans, DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

          string sql = @"
          UPDATE al_UserSubscription SET";

          if (coacheeInfo.UserSubscription.SubscriptionId == null) {
            // If Subscription is null, cancels current one and sets End date to Today
            sql += " SubscriptionEndUtc = @SubscriptionEndUtc";
          } else {
            // If Subscription is not null, updates SubscriptionId in database.
            sql += " SubscriptionId = @SubscriptionId";
          }

          sql += " WHERE UserId = @UserId";

          return Common.GetNonQueryInt(trans, sql,
            Common.NewSqlParameter("@UserId", coacheeInfo.UserId),
            Common.NewSqlParameter("@SubscriptionId", coacheeInfo.UserSubscription.SubscriptionId ?? 0),
            Common.NewSqlParameter("@SubscriptionEndUtc", DateTime.UtcNow) // Set to today date if subscription is cancelled
          ) == 1;
        }

        public static bool CancelSubscription(SqlTransaction trans, UserSubscriptionInfo userSubscription) {
          return UpdateSubscriptionEndDate(trans, userSubscription, DateTime.UtcNow);
        }

        public static bool UpdateSubscriptionEndDate(SqlTransaction trans, UserSubscriptionInfo userSubscription, DateTime endDateUtc) {

          if (userSubscription == null) return false;

          bool success = Common.GetNonQueryInt(trans, @"
          UPDATE al_UserSubscription SET
          SubscriptionEndUtc = @SubscriptionEndUtc
          WHERE UserSubscriptionId = @UserSubscriptionId",
            Common.NewSqlParameter("@UserSubscriptionId", userSubscription.UserSubscriptionId),
            Common.NewSqlParameter("@SubscriptionEndUtc", endDateUtc) // Set to today date if subscription is cancelled
          ) == 1;
          if (success) {
            userSubscription.SubscriptionEndUtc = endDateUtc;
          }
          return success;
        }

        public static bool UpdateHasAICoachEnabled(SqlTransaction trans, UserSubscriptionInfo userSubscription) {

          if (userSubscription == null) return false;

          return Common.GetNonQueryInt(trans, @"
          UPDATE al_UserSubscription
          SET HasAICoachEnabled = @HasAICoachEnabled
          WHERE UserSubscriptionId = @UserSubscriptionId",
            Common.NewSqlParameter("@UserSubscriptionId", userSubscription.UserSubscriptionId),
            Common.NewSqlParameter("@HasAICoachEnabled", userSubscription.UserHasAICoachEnabled)
          ) == 1;
        }

        private static List<UserSubscriptionInfo> GetUserSubscriptionList(
          SqlTransaction trans,
          string topSql,
          string extraJoins,
          string sqlWhereConditions,
          string sqlOrderByColumns,
          params SqlParameter[] sqlWhereParams) {

          var userSubscription = new List<UserSubscriptionInfo>();

          Common.Query(
            trans,
            GetUserSubscriptionSelectSql(topSql, sqlWhereConditions.EmptyIfNull(), extraJoins.EmptyIfNull(), sqlOrderByColumns.EnsureStartsWith(", ", true).EmptyIfNull()),
            dr => {
              userSubscription.Add(new UserSubscriptionInfo(dr));
            },
            sqlWhereParams
          );

          return userSubscription;
        }

        public static UserSubscriptionInfo GetUserSubscriptionByUserId(SqlTransaction trans, int userId, bool getOnlyActiveSub = false) {

          var existingSub = GetUserSubscriptionList(
            trans: trans,
            topSql: "TOP 1",
            extraJoins: null,
            sqlWhereConditions: $"us.UserId = @UserId {(getOnlyActiveSub ? " AND CAST(us.SubscriptionEndUtc AS date) > @NowUtc AND CAST(us.SubscriptionEndUtc AS date) <> @NowUtc" : "")}",
            null,
            Common.NewSqlParameter("UserId", userId),
            Common.NewSqlParameter("NowUtc", DateTime.UtcNow.Date));

          if (existingSub == null || existingSub.Count == 0) {
            return null; // No active subscription found.
          }

          return existingSub[0]; // Return the first active subscription.
        }

        public static UserSubscriptionInfo GetUserSubscriptionInfoByEmail(SqlTransaction trans, string userEmail) {

          var existingSub = GetUserSubscriptionList(
            trans: trans,
            topSql: "TOP 1",
            extraJoins: "INNER JOIN sv_User u ON u.UserId = us.UserId",
            sqlWhereConditions: "us.SubscriptionEndUtc > GETUTCDATE() AND u.Email = @UserEmail",
          null,
            Common.NewSqlParameter("UserEmail", userEmail));

          if (existingSub == null || existingSub.Count == 0) {
            return null; // No active subscription found.
          }

          return existingSub[0]; // Return the first active subscription.
        }

        public static void UpdateCoacheeSubscription(SqlTransaction trans,
          DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
          DbHelper.AbleQuotes.QuoteItemForSubscription quoteItemInfo = null,
          bool updateIfExists = true,
          bool cancelActiveSubscriptionIfExists = false) {

          if (coacheeInfo == null) {
            throw new ArgumentException("coacheeInfo is null");
          }

          if (coacheeInfo.UserId == null) {
            // Create user for Coachee is it currently doesn't have one.
            coacheeInfo.UserId = AbleUser.CreateUserFromCoachee(trans, coacheeInfo);
            AlbertCoachees.UpdateCoacheeUserId(trans, coacheeInfo);
          }

          // If QuoteItemInfo and UserSubscription are not null, check that SubscriptionsId corresponds
          if (quoteItemInfo != null && coacheeInfo.UserSubscription != null) {
            if (quoteItemInfo.SubscriptionId != coacheeInfo.UserSubscription.SubscriptionId) {
              throw new ArgumentException("SubscriptionId doesn't match QuoteItemId");
            }
          }

          if (coacheeInfo.TenantOrgId == ConfigHelper.IntegralTenantOrgId) {
            // Integral rule - Validate selected subscription
            if (coacheeInfo.HasSubscription) {

              if (coacheeInfo.UserSubscription.HasQuoteAssigned) {

                throw new ArgumentException("Subscription cannot be updated, QuoteItemId is already assigned.");

              } else if (coacheeInfo.UserSubscription.SubscriptionId != ConfigHelper.SubscriptionId.FoundationFree) {

                // If SubscriptionId is not Foundation Free, check that selected Quote has available Subscriptions to allocate
                if (quoteItemInfo == null || quoteItemInfo.AvailableSubscriptions <= 0) {
                  throw new ArgumentException("quoteItemInfo must have a value");
                }
              }
            }
          }

          // Check if the coachee already has an active subscription.
          var existingActiveSub = GetUserSubscriptionByUserId(trans, coacheeInfo.UserId.Value, true);

          if (existingActiveSub != null && cancelActiveSubscriptionIfExists) {
            CancelSubscription(trans, existingActiveSub);
            existingActiveSub = null;
          }

          if (quoteItemInfo != null && !coacheeInfo.UserSubscription.HasQuoteAssigned) {
            // If doesn't have a quote assigned and received QuoteItemInfo

            bool isCreatingNewSub = existingActiveSub == null || existingActiveSub.SubscriptionId != coacheeInfo.UserSubscription.SubscriptionId;

            if (isCreatingNewSub) {

              if (existingActiveSub != null) {
                CancelSubscription(trans, existingActiveSub);
              }

              coacheeInfo.UserSubscription.UserSubscriptionId = CreateUserSubscription(trans, coacheeInfo);
            }

            DateTime componentCompletedUtc = isCreatingNewSub ? DateTime.UtcNow.AddYears(1) : existingActiveSub.SubscriptionEndUtc;
            decimal? defaultComponentPrice = 0; // Temporal solution for cost items, avoid allocating quote items component twice.
            ProgramComponents.UpsertUserSubscription(trans, coacheeInfo.UserSubscription.UserSubscriptionId, coacheeInfo.ProgramJobId.Value, defaultComponentPrice, componentCompletedUtc, quoteItemInfo.QuoteItemId);
            coacheeInfo.UserSubscription.QuoteItemId = quoteItemInfo.QuoteItemId; // Assign the QuoteItemId to the UserSubscription.

          } else if (existingActiveSub == null) {

            if (coacheeInfo.UserSubscription.SubscriptionId == null) return; // No subscription to create.

            // Create a new subscription.
            try {
              CreateUserSubscription(trans, coacheeInfo);
            } catch (Exception ex) {
              var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
              telemetry?.Exception(ex)
                .WithOperation(nameof(UpdateCoacheeSubscription))
                .WithOperationContext("CreateNew")
                .WithProperty(DalApplicationInsightsConstants.CoacheeId, coacheeInfo.UserId)
                .WithProperty(DalApplicationInsightsConstants.SubscriptionId, coacheeInfo.UserSubscription?.SubscriptionId)
                .Track();

              throw new ArgumentException($"Error creating user Subscription - {ex.Message}");
            }

          } else {

            if (!updateIfExists) return; // No need to update

            // Update enabled AI Coach if it's different.
            if (existingActiveSub.HasAICoaching && existingActiveSub.UserHasAICoachEnabled != coacheeInfo.UserSubscription.UserHasAICoachEnabled) {
              try {
                UpdateHasAICoachEnabled(trans, coacheeInfo.UserSubscription);
              } catch (Exception ex) {
                var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
                telemetry?.Exception(ex)
                  .WithOperation(nameof(UpdateCoacheeSubscription))
                  .WithOperationContext("UpdateAICoachEnabled")
                  .WithProperty(DalApplicationInsightsConstants.CoacheeId, coacheeInfo.UserId)
                  .WithProperty(DalApplicationInsightsConstants.UserSubscriptionId, coacheeInfo.UserSubscription?.UserSubscriptionId)
                  .WithProperty(DalApplicationInsightsConstants.HasAICoachEnabled, coacheeInfo.UserSubscription?.UserHasAICoachEnabled)
                  .Track();

                throw new ArgumentException($"Error updating enabled flag - {ex}.");
              }
            }

            // Just return if the SubscriptionId is the same as the existing one.
            if (existingActiveSub.SubscriptionId == coacheeInfo.UserSubscription.SubscriptionId) return;

            // Otherwise, update the existing subscription.
            try {
              UpdateUserSubscription(trans, coacheeInfo);
            } catch (Exception ex) {
              var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
              telemetry?.Exception(ex)
                .WithOperation(nameof(UpdateCoacheeSubscription))
                .WithOperationContext("UpdateExisting")
                .WithProperty(DalApplicationInsightsConstants.CoacheeId, coacheeInfo.UserId)
                .WithProperty(DalApplicationInsightsConstants.ExistingSubscriptionId, existingActiveSub?.SubscriptionId)
                .WithProperty(DalApplicationInsightsConstants.NewSubscriptionId, coacheeInfo.UserSubscription?.SubscriptionId)
                .Track();

              throw new ArgumentException($"Error updating user Subscription - {ex.Message}");
            }
          }
        }

        internal static UserSubscriptionInfo GetUserSubscriptionInfo(SqlDataReader dr) {
          return dr.IsDBNull("SubscriptionId") ? null : new UserSubscriptionInfo(dr);
        }

        public class UserSubscriptionInfo : SubscriptionInfo {

          public int UserSubscriptionId { get; set; }
          public int UserId { get; private set; }
          public DateTime CreatedUTC { get; private set; }
          public DateTime SubscriptionStartUtc { get; private set; }
          public DateTime SubscriptionEndUtc { get; internal set; }
          public bool AutoRenew { get; private set; }
          public bool UserHasAICoachEnabled { get; set; }
          public int? QuoteItemId { get; set; }
          public bool HasQuoteAssigned { get; private set; }

          public UserSubscriptionInfo() { }

          public UserSubscriptionInfo(SqlDataReader dr) {
            this.UserSubscriptionId = dr.GetInt("UserSubscriptionId");
            this.UserId = dr.GetInt("UserId");
            this.CreatedUTC = dr.GetDateTime("CreatedUTC");
            this.SubscriptionStartUtc = dr.GetDateTime("SubscriptionStartUtc");
            this.SubscriptionEndUtc = dr.GetDateTime("SubscriptionEndUtc");
            this.AutoRenew = dr.GetBoolFromInt("AutoRenew");
            this.UserHasAICoachEnabled = dr.GetBoolFromInt("HasAICoachEnabled");
            this.QuoteItemId = dr.GetIntOrNull("QuoteItemId");
            this.HasQuoteAssigned = this.QuoteItemId != null;
            AssignSubscriptionInfo(dr);
          }
        }
      }
    }
  }
}
