using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Subscriptions {

      public class Org {

        public static List<OrgSubscriptionItem> GetOrgSubscriptionItems(int orgId) {

          var orgSubscriptionItems = new List<OrgSubscriptionItem>();

          Common.Query($@"
          SELECT
            {GetSubscriptionInfoSelectionSQL("sub")},
            osub.Quantity AS TotalSeats,
            usub.AssignedSeats
          FROM al_Subscription sub
          LEFT OUTER JOIN al_OrgSubscription osub ON osub.SubscriptionId = sub.SubscriptionId AND osub.OrgId = @OrgId
          CROSS APPLY (
            SELECT COUNT(*) AS AssignedSeats
            FROM al_UserSubscription usub
            INNER JOIN sv_User su ON su.UserId = usub.UserId
            WHERE su.OrgId = @OrgId
              AND usub.SubscriptionId = sub.SubscriptionId
              AND usub.SubscriptionEndUtc > SYSUTCDATETIME()
          ) usub
          WHERE sub.IsDisabled = 0
          ORDER BY sub.SubscriptionDisplayOrder",
            dr => {
              orgSubscriptionItems.Add(new OrgSubscriptionItem(
                subscriptionId: dr.GetInt("SubscriptionId"),
                subscriptionGuid: dr.GetGuid("SubscriptionGuid"),
                subscriptionName: dr.GetString("SubscriptionName"),
                pricePerUserPerMonth: dr.GetDecimal("PricePerUserPerMonth"),
                stripeProductPriceId: dr.GetString($"StripeProductPriceId{TestColumnSuffix}"),
                displayOrder: dr.GetInt("SubscriptionDisplayOrder"),
                hasNudges: dr.GetBoolFromInt("HasNudges"),
                hasPulse: dr.GetBoolFromInt("HasPulse"),
                hasAICoaching: dr.GetBoolFromInt("HasAICoaching"),
                totalSeats: dr.GetIntOrNull("TotalSeats") ?? 0,
                assignedSeats: dr.GetInt("AssignedSeats")
              ));
            },
            Common.NewSqlParameter("OrgId", orgId)
          );

          return orgSubscriptionItems;
        }

        // Upsert to either update the quantity of an existing item or add a new item.
        public static void UpdateOrgSubscriptionQuantity(
          SqlTransaction trans,
          TenantOrg.TenantOrgInfo tenantOrgInfo,
          OrgSubscriptionItem selectedSubscription,
          int quantity) {

          Common.GetNonQueryInt(trans, $@"

            {(trans == null ? "BEGIN TRANSACTION;" : "")}

            IF EXISTS (
              SELECT 1
              FROM al_OrgSubscription WITH (UPDLOCK, HOLDLOCK)
              WHERE SubscriptionId = @SubscriptionId
                AND OrgId = @OrgId
            )
            BEGIN
              UPDATE al_OrgSubscription
              SET Quantity = @Quantity
              WHERE SubscriptionId = @SubscriptionId
                AND OrgId = @OrgId;
            END
            ELSE
            BEGIN
              INSERT INTO al_OrgSubscription (OrgId, SubscriptionId, Quantity)
              VALUES (@OrgId, @SubscriptionId, @Quantity);
            END

            {(trans == null ? "COMMIT TRANSACTION;" : "")}",

            Common.NewSqlParameter("OrgId", tenantOrgInfo.OrgId),
            Common.NewSqlParameter("SubscriptionId", selectedSubscription.SubscriptionId),
            Common.NewSqlParameter("Quantity", quantity)
          );
        }

        public static void UpdateOrgSubscriptionsForAssignedSeats(SqlTransaction trans, int orgId) {

          Common.GetNonQueryInt(trans, $@"

            {(trans == null ? "BEGIN TRANSACTION;" : "")}

            -- Table var to store counts of currently active
            -- seats in the org (by user.orgid at the moment).
            DECLARE @usub TABLE (
              SubscriptionId INT,
              OrgId INT,
              ActiveSeats INT,
              OrgSeats INT
            );

            -- Populate table var with latest active user subscription counts.
            INSERT INTO @usub (SubscriptionId, OrgId, ActiveSeats, OrgSeats)
              SELECT sub.SubscriptionId, @OrgId, usub.ActiveSeats, ISNULL(osub.Quantity, 0)
              FROM al_Subscription sub
              CROSS APPLY (
                SELECT COUNT(usub.UserSubscriptionId) AS ActiveSeats
                FROM al_UserSubscription usub WITH (UPDLOCK, SERIALIZABLE)
                INNER JOIN sv_User su ON su.UserId = usub.UserId
                WHERE su.OrgId = @OrgId
                  AND usub.SubscriptionId = sub.SubscriptionId
                  AND usub.SubscriptionEndUtc > SYSUTCDATETIME()
              ) AS usub
              OUTER APPLY (
                SELECT osub.Quantity
                FROM al_OrgSubscription osub WITH (UPDLOCK, SERIALIZABLE)
                WHERE osub.SubscriptionId = sub.SubscriptionId
                AND osub.OrgId = @OrgId
              ) AS osub
              WHERE sub.IsDisabled = 0
                AND sub.PricePerUserPerMonth > 0;

            -- Update existing records in OrgSubscription if Active subs > Org total subs.
            UPDATE osub WITH (UPDLOCK, SERIALIZABLE)
            SET osub.Quantity = usub.ActiveSeats
            FROM al_OrgSubscription osub
            INNER JOIN @usub usub ON usub.SubscriptionId = osub.SubscriptionId
            WHERE usub.ActiveSeats > osub.Quantity;

            -- Insert any missing org sub rows.
            INSERT INTO al_OrgSubscription (OrgId, SubscriptionId, Quantity)
            SELECT OrgId, SubscriptionId, ActiveSeats
            FROM @usub usub
            WHERE NOT EXISTS (
              SELECT 1
              FROM al_OrgSubscription osub
              WHERE osub.OrgId = usub.OrgId
                AND osub.SubscriptionId = usub.SubscriptionId);

            {(trans == null ? "COMMIT TRANSACTION;" : "")}",

            Common.NewSqlParameter("OrgId", orgId)
          );
        }

        public class OrgSubscriptionItem : SubscriptionBase {

          public int TotalSeats { get; private set; }
          public int AssignedSeats { get; private set; }
          public int AvailableSeats => TotalSeats - AssignedSeats;

          public OrgSubscriptionItem(
            int subscriptionId,
            Guid subscriptionGuid,
            string subscriptionName,
            decimal pricePerUserPerMonth,
            string stripeProductPriceId,
            int displayOrder,
            bool hasNudges,
            bool hasPulse,
            bool hasAICoaching,
            int totalSeats,
            int assignedSeats) {

            SubscriptionId = subscriptionId;
            SubscriptionGuid = subscriptionGuid;
            SubscriptionName = subscriptionName;
            PricePerUserPerMonth = pricePerUserPerMonth;
            StripeProductPriceId = stripeProductPriceId;
            DisplayOrder = displayOrder;
            HasNudges = hasNudges;
            HasPulse = hasPulse;
            HasAICoaching = hasAICoaching;
            TotalSeats = totalSeats;
            AssignedSeats = assignedSeats;
          }
        }
      }
    }
  }
}
