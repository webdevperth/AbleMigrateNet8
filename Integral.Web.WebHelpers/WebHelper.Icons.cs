// Specifically Icon-related html.

using System.Collections.Generic;

namespace Integral.Web {

  public partial class WebHelper {

    public class Icon {

      public enum IconHeight { Normal = 22, Larger = 32 }
      public enum IconType { None, GM, Ion, FA, FA5Solid, FA5Regular, FA5Light, FA5Duotone }
      public const string FontAwesomePrefixClass = "fontawesome";

      private static Dictionary<IconType, string> IconPrefixClasses = new Dictionary<IconType, string>() {
        { IconType.FA, $"{FontAwesomePrefixClass} fa fa-" },
        { IconType.FA5Solid, $"{FontAwesomePrefixClass} fas fa-" },
        { IconType.FA5Regular, $"{FontAwesomePrefixClass} far fa-" },
        { IconType.FA5Light, $"{FontAwesomePrefixClass} fal fa-" },
        { IconType.FA5Duotone, $"{FontAwesomePrefixClass} fad fa-" }
      };

      public class ActionCardIconClass {
        public const string None = "";
        public const string Pulse = "pulse";
        public const string Intake = "intake";
        public const string CoachingEval = "coaching-eval";
        public const string WorkshopEval = "workshop-eval";
        public const string Profile = "profile";
        public const string AICoach = "aicoach";
        public const string DevPlan = "devplan";
        public const string ShareSurvey = "sharesurvey";
        public const string Module = "module";
      }

      #region "Custom Enum"
      private IconType _iconType;
      private string _iconClasses = "", _extraClasses = "", _extraAttrHtml = "";
      private IconHeight _height = IconHeight.Normal;

      private Icon(IconType iconType, string iconClass) {
        SetProps(iconType, iconClass, "", "", IconHeight.Normal);
      }
      private Icon(Icon i) {
        SetProps(i._iconType, i._iconClasses, i._extraClasses, i._extraAttrHtml, i._height);
      }
      private void SetProps(IconType iconType, string iconClass, string extraClasses, string extraAttrsHtml, IconHeight height) {
        _iconType = iconType; _iconClasses = iconClass; _extraClasses = extraClasses; _extraAttrHtml = extraAttrsHtml; _height = height;
        if (IconPrefixClasses.ContainsKey(_iconType)) _iconClasses = _iconClasses.EnsureStartsWith(IconPrefixClasses[_iconType]);
      }
      private string GetHTML() {
        if (_iconType == IconType.None) {
          return "";
        } else if (_iconType == IconType.GM) {
          return $"<span class=\"material-icons {_extraClasses.HTMLEncode()}\" {_extraAttrHtml}>{_iconClasses.HTMLEncode()}</span>";
        } else if (_iconType == IconType.Ion) {
          return $@"<ion-icon name=""{_iconClasses}"" role=""img""></ion-icon>";
        } else {
          return GetFontAwesomeHtml($"{_iconClasses} {_extraClasses}", _extraAttrHtml);
        }
      }
      public static string GetFontAwesomeHtml(string faClasses, string extraAttrHtml = "") {
        return $"<i class=\"{faClasses.HTMLEncode()}\" {extraAttrHtml}></i>";
      }

      public string ClassName => _iconClasses;

      public override string ToString() => GetHTML();
      public string HTML => GetHTML();

      public Icon AddClass(string classes) => new Icon(this) { _extraClasses = _extraClasses + " " + classes };
      public Icon AddTooltip(string tipText) => new Icon(this) { _extraAttrHtml = _extraAttrHtml + $" data-tooltip=\"{tipText.HTMLEncode().Replace("\n", "<br>")}\"" };
      public Icon AddAttribute(string attrName, string value) => new Icon(this) { _extraAttrHtml = _extraAttrHtml + $" {attrName.HTMLEncode()}=\"{value.HTMLEncode()}\"" };
      #endregion

      // Enum items - class names are for Font Awesome 5.
      public static readonly Icon InternalLink = new Icon(IconType.FA5Regular, "sign-in-alt");
      public static readonly Icon ExternalLink = new Icon(IconType.FA5Solid, "external-link-alt");
      public static readonly Icon ExternalLinkRegular = new Icon(IconType.FA5Regular, "external-link-alt");
      public static readonly Icon Survey = new Icon(IconType.FA5Solid, "align-left");
      public static readonly Icon Check = new Icon(IconType.FA5Solid, "check");
      public static readonly Icon CheckCircle = new Icon(IconType.FA5Solid, "check-circle");
      public static readonly Icon TooltipHelp = new Icon(IconType.FA5Regular, "info-circle");
      public static readonly Icon SadTear = new Icon(IconType.FA5Regular, "sad-tear");
      public static readonly Icon SadCry = new Icon(IconType.FA5Regular, "sad-cry");
      public static readonly Icon Frown = new Icon(IconType.FA5Regular, "frown");
      public static readonly Icon MailBulk = new Icon(IconType.FA5Regular, "mail-bulk");
      public static readonly Icon Edit = new Icon(IconType.FA5Regular, "edit");
      public static readonly Icon Delete = new Icon(IconType.FA5Regular, "trash");
      public static readonly Icon MinusCircle = new Icon(IconType.FA5Regular, "minus-circle");
      public static readonly Icon SurveyInfo = new Icon(IconType.FA5Solid, "info");
      public static readonly Icon SurveyReport = new Icon(IconType.FA5Regular, "align-left");
      public static readonly Icon PDF_outline = new Icon(IconType.FA5Regular, "file-pdf");
      public static readonly Icon PDF_solid = new Icon(IconType.FA5Solid, "file-pdf");
      public static readonly Icon Smile_Plus = new Icon(IconType.FA5Solid, "smile-plus");
      public static readonly Icon Circle_Plus = new Icon(IconType.FA5Regular, "plus-circle");
      public static readonly Icon User_Circle = new Icon(IconType.FA5Regular, "user-circle");
      public static readonly Icon User_Cog = new Icon(IconType.FA5Regular, "user-cog");
      public static readonly Icon User_Plus = new Icon(IconType.FA5Solid, "user-plus");
      public static readonly Icon Calendar = new Icon(IconType.FA5Regular, "calendar-alt");
      public static readonly Icon Calendar_Plus = new Icon(IconType.FA5Solid, "calendar-plus");
      public static readonly Icon File = new Icon(IconType.FA5Regular, "file-alt");
      public static readonly Icon File_Plus = new Icon(IconType.FA5Regular, "file-plus");
      public static readonly Icon Layer_Plus = new Icon(IconType.FA5Solid, "layer-plus");
      public static readonly Icon Copy = new Icon(IconType.FA5Regular, "copy");
      public static readonly Icon Trash = new Icon(IconType.FA5Regular, "trash-alt");
      public static readonly Icon QuestionCircle_Solid = new Icon(IconType.FA5Light, "question-circle");
      public static readonly Icon QuestionCircle_Regular = new Icon(IconType.FA5Regular, "question-circle");
      public static readonly Icon File_Invoice_Dollar = new Icon(IconType.FA5Regular, "file-invoice-dollar");
      public static readonly Icon Cog_Solid = new Icon(IconType.FA5Solid, "cog");
      public static readonly Icon ChalkboardTeacher = new Icon(IconType.FA5Solid, "chalkboard-teacher");
      public static readonly Icon GraduationCap = new Icon(IconType.FA5Solid, "graduation-cap");
      public static readonly Icon DollarSign = new Icon(IconType.FA5Regular, "dollar-sign");
      public static readonly Icon Cubes = new Icon(IconType.FA5Solid, "cubes");
      public static readonly Icon Team = new Icon(IconType.FA5Solid, "users");
      public static readonly Icon DraggableRow = new Icon(IconType.FA5Light, "bars");
    }

  } // HTMLHelper
}
