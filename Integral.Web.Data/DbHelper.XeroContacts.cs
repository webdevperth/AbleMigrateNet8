using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class XeroContacts {

      public static List<XeroContactsInfo> GetXeroContacts() {

        var xeroContacts = new List<XeroContactsInfo>();

        Common.Query($@"
          SELECT
            xc.XeroContactId, xc.ContactName, xc.EmailAddress, xc.XeroContactUID,
            xc.Firstname, xc.Lastname, xc.IsSupplier, xc.IsCustomer, xc.LastXeroSyncUtc
          FROM al_XeroContacts xc
          ORDER BY xc.ContactName",
          null,
          dr => {
            xeroContacts.Add(new XeroContactsInfo(
              dr.GetInt("XeroContactId"),
              dr.GetString("ContactName"),
              dr.GetString("EmailAddress"),
              dr.GetGuid("XeroContactUID"),
              dr.GetString("Firstname"),
              dr.GetString("Lastname"),
              dr.GetBoolFromInt("IsSupplier"),
              dr.GetBoolFromInt("IsCustomer"),
              dr.GetDateTimeOrNull("LastXeroSyncUtc")
            ));
          }
        );
        return xeroContacts;
      }

      public static bool UpdateXeroContact(int xeroContactId, string xeroContactNewName) {
        return Common.GetNonQueryInt($@"
          UPDATE al_XeroContacts SET ContactName = @ContactName
          WHERE XeroContactId = @XeroContactId",
          Common.NewSqlParameter("@XeroContactId", xeroContactId),
          Common.NewSqlParameter("@ContactName", xeroContactNewName)
        ) == 1;
      }

      public class XeroContactsInfo {
        public int XeroContactId { get; set; }
        public string ContactName { get; set; }
        public string EmailAddress { get; set; }
        public Guid XeroContactUID { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public bool IsSupplier { get; set; }
        public bool IsCustomer { get; set; }
        public DateTime? LastXeroSyncUtc { get; set; }

        public XeroContactsInfo(int xeroContactId, string contactName, string emailAddress, Guid xeroContactUID, string firstname, string lastname, bool isSupplier, bool isCustomer, DateTime? lastXeroSyncUtc) {
          XeroContactId = xeroContactId;
          ContactName = contactName;
          EmailAddress = emailAddress;
          XeroContactUID = xeroContactUID;
          Firstname = firstname;
          Lastname = lastname;
          IsSupplier = isSupplier;
          IsCustomer = isCustomer;
          LastXeroSyncUtc = lastXeroSyncUtc;
        }
      }

    }
  }
}
