using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class SendingDomain {

      private const string TblPfx = "dom";

      public static SendingDomainInfo GetDomainByName(string domainName) {

        SendingDomainInfo domainInfo = null;

        Common.Query($@"
          SELECT SendingDomainId, DomainName, DomainVerified
          FROM al_SendingDomains
          WHERE DomainName = @DomainName",
          dr => {
            domainInfo = new SendingDomainInfo(
              dr.GetInt("SendingDomainId"),
              dr.GetString("DomainName"),
              dr.GetBoolFromInt("DomainVerified")
            );
          },
          Common.NewSqlParameter("DomainName", domainName)
        );
        return domainInfo;
      }

      public class SendingDomainInfo {

        public int SendingDomainId { get; internal set; }
        public string DomainName { get; set; }
        public bool DomainVerified { get; set; }

        public SendingDomainInfo(
          int sendingDomainId,
          string domainName,
          bool domainVerified
        ) {
          this.SendingDomainId = sendingDomainId;
          this.DomainName = domainName;
          this.DomainVerified = domainVerified;
        }
      }

    }
  }
}
