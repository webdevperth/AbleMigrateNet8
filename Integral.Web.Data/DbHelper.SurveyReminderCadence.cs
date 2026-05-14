using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    // Note: 1. This should eventually be a real lookup table, but for now it is virtual.
    //       2. The word "reminder" here also includes sending the first invite, not just sending reminders.
    //          So the "None" option does not mean "no reminders" it means no survey emails at all, including the first one.

    public class SurveyReminderCadence {

      public static readonly SurveyReminderCadence All, None, FirstOnly, FirstAndFinal;

      private static Dictionary<ConfigHelper.SurveyReminderCadenceIds, SurveyReminderCadence> cadenceByEnum;
      private static SurveyReminderCadence defaultCadence = null;

      public static bool IsValidId(int? id) => id == null ? false : Enum.IsDefined(typeof(ConfigHelper.SurveyReminderCadenceIds), id);

      public static SurveyReminderCadence GetById(ConfigHelper.SurveyReminderCadenceIds id) => cadenceByEnum[id];

      public static SurveyReminderCadence GetByIdOrDefault(int? id) => id != null && Enum.IsDefined(typeof(ConfigHelper.SurveyReminderCadenceIds), id) ? cadenceByEnum[(ConfigHelper.SurveyReminderCadenceIds)id] : defaultCadence;

      public static void ForEach(Func<SurveyReminderCadence, bool> f) { foreach (var o in cadenceByEnum) { if (!f(o.Value)) break; } }

      static SurveyReminderCadence() {

        cadenceByEnum = new Dictionary<ConfigHelper.SurveyReminderCadenceIds, SurveyReminderCadence>();
        defaultCadence = null;

        All = new SurveyReminderCadence(ConfigHelper.SurveyReminderCadenceIds.All, "All", true);
        None = new SurveyReminderCadence(ConfigHelper.SurveyReminderCadenceIds.None, "None");
        FirstOnly = new SurveyReminderCadence(ConfigHelper.SurveyReminderCadenceIds.FirstOnly, "First Invite Only");
        FirstAndFinal = new SurveyReminderCadence(ConfigHelper.SurveyReminderCadenceIds.FirstAndFinal, "First Invite and Final Reminder Only");
      }

      // Instance members

      public ConfigHelper.SurveyReminderCadenceIds CadenceType { get; private set; }
      public int Id { get; private set; }
      public string DisplayText { get; private set; }
      public bool IsDefault { get; private set; }

      private SurveyReminderCadence(ConfigHelper.SurveyReminderCadenceIds cadenceType, string displayText, bool isDefault = false) {

        this.CadenceType = cadenceType;
        this.Id = (int)cadenceType;
        this.DisplayText = displayText;
        this.IsDefault = isDefault;

        if (defaultCadence == null || isDefault) defaultCadence = this;

        cadenceByEnum.Add(cadenceType, this);
      }

      public bool IsSameAs(SurveyReminderCadence reminderCadence) {
        if (reminderCadence == null) return false;
        return reminderCadence.Id == this.Id;
      }

      // Check cadence rules.
      public bool CanSendInvite(DateTime? firstInviteSentUtc, DateTime? latestReminderSentUtc, DateTime closeDateUtc) {

        if (DateTime.UtcNow >= closeDateUtc) return false; // Self or rater close date passed.

        if (this.CadenceType == ConfigHelper.SurveyReminderCadenceIds.All) return true; // Send all invites.
        if (this.CadenceType == ConfigHelper.SurveyReminderCadenceIds.None) return false; // Don't send any invites.

        bool isFirstInvite = firstInviteSentUtc == null && latestReminderSentUtc == null;
        bool isFinalReminder = DateTime.UtcNow > closeDateUtc.AddHours(-ConfigHelper.MinimumHoursBetweenReminders * 2); // Final reminder occurs from this time.

        if (this.CadenceType == ConfigHelper.SurveyReminderCadenceIds.FirstOnly) {

          return isFirstInvite;

        } else if (this.CadenceType == ConfigHelper.SurveyReminderCadenceIds.FirstAndFinal) {

          return isFirstInvite || isFinalReminder;

        }

        // All cadence types should be handled above.
        throw new ApplicationException($"Unhandled Cadence Type: {this.CadenceType} (ID={this.Id}).");
      }
    }

  }
}

