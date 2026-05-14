using NanoidDotNet;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class PartnerReferrals {

      static PartnerReferrals() {
      }

      public static List<PartnerReferral> GetAllForUserId(int inviterUserId) {
        return GetReferralList(null, "",
          "pr.InviterUserId = @InviterUserId",
          "pr.InviteeFirstName, pr.InviteeLastName, pr.PartnerReferralId",
          Common.NewSqlParameter("InviterUserId", inviterUserId));
      }

      public static PartnerReferral GetReferralByGuid(Guid referralGuid) {
        var referralInfo = GetReferralList(1, "", "pr.PartnerReferralGUID = @ReferralGuid", "", Common.NewSqlParameter("ReferralGuid", referralGuid));
        if (referralInfo.IsNullOrEmpty()) return null;
        return referralInfo[0];
      }

      private static List<PartnerReferral> GetReferralList(
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        string sqlOrderBy,
        params SqlParameter[] sqlWhereParams) {

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);

        var list = new List<PartnerReferral>();

        string sql = $@"
          SELECT {sqlTop}
            pr.PartnerReferralId, pr.PartnerReferralGUID, pr.InviterUserId,
            pr.InviteeFirstName, pr.InviteeLastName, pr.InviteeEmail,
            pr.CreatedUtc, pr.InviteEmailSentUtc, pr.InviteAcceptedUtc,
            pr.ReferralCode
          FROM al_PartnerReferrals pr
          {sqlExtraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        Common.Query(sql,
          dr => {
            var referral = new PartnerReferral(
              dr.GetInt("PartnerReferralId"),
              dr.GetGuid("PartnerReferralGUID"),
              dr.GetInt("InviterUserId"),
              dr.GetString("InviteeFirstName"),
              dr.GetString("InviteeLastName"),
              dr.GetString("InviteeEmail"),
              dr.GetDateTime("CreatedUtc"),
              dr.GetDateTimeOrNull("InviteEmailSentUtc"),
              dr.GetDateTimeOrNull("InviteAcceptedUtc"),
              dr.GetString("ReferralCode"));
            list.Add(referral);
          },
          sqlWhereParams
        );
        return list;
      }

      public static Guid CreateReferralBasic(
        int inviterUserId, string inviteeFirstName, string inviteeLastName, string inviteeEmail) {

        // If insert fails on ReferralCode unique key, keep making a new random code until successful.

        Guid newReferralGuid = Guid.Empty;
        while (newReferralGuid == Guid.Empty) {
          try {
            newReferralGuid = (Guid)Common.GetScalarQuery(@"
            INSERT INTO al_PartnerReferrals
              (InviterUserId, InviteeFirstName, InviteeLastName, InviteeEmail, ReferralCode)
            OUTPUT INSERTED.PartnerReferralGuid
            VALUES
            (@InviterUserId, @InviteeFirstName, @InviteeLastName, @InviteeEmail, @ReferralCode)",
              Common.NewSqlParameter("InviterUserId", inviterUserId),
              Common.NewSqlParameter("InviteeFirstName", inviteeFirstName),
              Common.NewSqlParameter("InviteeLastName", inviteeLastName),
              Common.NewSqlParameter("InviteeEmail", inviteeEmail),
              Common.NewSqlParameter("ReferralCode", GenerateReferralCode()));
          } catch (SqlException ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation(nameof(CreateReferralBasic))
              .WithOperationContext("GenerateUniqueReferralCode")
              .WithProperty("InviterUserId", inviterUserId)
              .WithProperty(DalApplicationInsightsConstants.InviteeEmail, inviteeEmail)
              .WithProperty(DalApplicationInsightsConstants.InviteeFirstName, inviteeFirstName)
              .WithProperty(DalApplicationInsightsConstants.InviteeLastName, inviteeLastName)
              .WithProperty(DalApplicationInsightsConstants.IsDuplicateKey, IsDuplicateKeyError(ex))
              .Track();

            if (!IsDuplicateKeyError(ex)) throw ex; // Anything other than unique key violation.
          }
        }
        return newReferralGuid;
      }

      public static bool SetAccepted(int partnerReferralId, DateTime? acceptedUtc) {
        int affected = Common.GetScalarQueryInt(@"
          UPDATE al_PartnerReferrals
          SET InviteAcceptedUtc = @InviteAcceptedUtc
          WHERE PartnerReferralId = @PartnerReferralId",
          Common.NewSqlParameter("InviteAcceptedUtc", acceptedUtc),
          Common.NewSqlParameter("PartnerReferralId", partnerReferralId));
        return affected > 0;
      }

      private static string GenerateReferralCode() {
        return Nanoid.Generate(ConfigHelper.PartnerReferralCodeAllowedCharacters, ConfigHelper.PartnerReferralCodeMaxLength);
      }

      public class PartnerReferral {

        public int PartnerReferralId { get; private set; }
        public Guid PartnerReferralGUID { get; private set; }
        public int InviterUserId { get; private set; }
        public string InviteeFirstName { get; private set; }
        public string InviteeLastName { get; private set; }
        public string InviteeEmail { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public DateTime? InviteEmailSentUtc { get; private set; }
        public DateTime? InviteAcceptedUtc { get; private set; }
        public string ReferralCode { get; private set; }

        public PartnerReferral(
          int partnerReferralId,
          Guid partnerReferralGUID,
          int inviterUserId,
          string inviteeFirstName,
          string inviteeLastName,
          string inviteeEmail,
          DateTime createdUtc,
          DateTime? inviteEmailSentUtc,
          DateTime? inviteAcceptedUtc,
          string referalCode

        ) {
          PartnerReferralId = partnerReferralId;
          PartnerReferralGUID = partnerReferralGUID;
          InviterUserId = inviterUserId;
          InviteeFirstName = inviteeFirstName;
          InviteeLastName = inviteeLastName;
          InviteeEmail = inviteeEmail;
          CreatedUtc = createdUtc;
          InviteEmailSentUtc = inviteEmailSentUtc;
          InviteAcceptedUtc = inviteAcceptedUtc;
          ReferralCode = referalCode;
        }
      }

    }
  }
}
