using System;
using System.Collections.Generic;
using System.Text;

namespace Integral.Web {

  public partial class WebHelper {

    public class Form {

      public enum RowTopMargin { Normal, None, Smaller1, Smaller2, Larger1, Larger2 }
      public enum LabelPosition { LeftLegacy, LeftNormal, LeftWide, LeftFitContent, Above }
      public enum ContentAlign { Left, Center, Right }
      public enum ControlWidthPreset { None, SmallNumber, JobNumber, DatePicker, Half, Full }
      public enum ImageType { Unset, ProfileImage, CompanyLogo }
      public enum CheckboxLabelPosition { Before, After }
      public enum AjaxDataType { Json, Html }

      /// <summary>
      /// Returns a css class to set a specific width for a form control.
      /// </summary>
      private static string GetControlWidthPresetClass(ControlWidthPreset controlWidthPreset) {
        if (controlWidthPreset == ControlWidthPreset.None) {
          return string.Empty;
        } else if (controlWidthPreset == ControlWidthPreset.DatePicker) {
          return "control-width-datepicker";
        } else if (controlWidthPreset == ControlWidthPreset.SmallNumber) {
          return "control-width-smallnumber";
        } else if (controlWidthPreset == ControlWidthPreset.JobNumber) {
          return "control-width-jobnumber";
        } else if (controlWidthPreset == ControlWidthPreset.Half) {
          return "control-width-half";
        } else if (controlWidthPreset == ControlWidthPreset.Full) {
          return "control-width-full";
        } else {
          throw new InvalidOperationException($"Unhandled {nameof(controlWidthPreset)} = {controlWidthPreset}");
        }
      }

      /// <summary>
      /// Standard form row, outputs label and content (usually a form control).
      /// </summary>
      public class FormRow {

        public RowTopMargin RowTopMargin { get; set; } = RowTopMargin.Normal;
        public LabelPosition LabelPosition { get; set; } = LabelPosition.LeftNormal;
        public bool Hidden { get; set; } = false;                   // Initially hidden
        public string Classes { get; set; } = string.Empty;         // Extra classes that can be added to the container
        public string LabelText { get; set; } = string.Empty;       // Label Text
        public string LabelHelpText { get; set; } = string.Empty;   // Helper Text
        public string LabelHelpUrl { get; set; } = string.Empty;    // URL for the 'Learn more ->' link in the helper textx
        public string ContentHtml { get; set; } = string.Empty;     // Content html that is passed to the container (usually a form control)
        public ContentAlign ContentAlign { get; set; } = ContentAlign.Left;

        // Using this in aspx allows autoformatting to work better. VS quirk.
        public void WriteHtml() => SystemWeb.ResponseWrite(ToHtml());

        public string ToHtml() {

          var labelHelpHtml = string.Empty;
          if (!LabelHelpText.IsNullOrEmpty()) {
            labelHelpHtml += $@"<span class=""form-label-help-text"">{LabelHelpText.HTMLEncode()}</span>";
          }
          if (!LabelHelpUrl.IsNullOrEmpty()) {
            if (!labelHelpHtml.IsNullOrEmpty()) labelHelpHtml += " ";
            labelHelpHtml += $@"<a class=""form-label-help-link"" href=""{LabelHelpUrl.HTMLEncode()}"" target=""_blank"">{DefultHelpLinkHtml}</a>";
          }
          if (!labelHelpHtml.IsNullOrEmpty()) {
            labelHelpHtml = $@"<div class=""form-label-help mb5"">{labelHelpHtml}</div>";
          }

          int label_col_md, label_col_lg;
          int content_col_md, content_col_lg;

          string rowClasses = Classes.HTMLEncode();

          if (Hidden) rowClasses += " hidden";

          if (RowTopMargin == RowTopMargin.None) {
            rowClasses += " mt0";
          } else if (RowTopMargin == RowTopMargin.Smaller1) {
            rowClasses += " formrow-topmargin-smaller1";
          } else if (RowTopMargin == RowTopMargin.Smaller2) {
            rowClasses += " formrow-topmargin-smaller2";
          } else if (RowTopMargin == RowTopMargin.Larger1) {
            rowClasses += " formrow-topmargin-larger1";
          } else if (RowTopMargin == RowTopMargin.Larger2) {
            rowClasses += " formrow-topmargin-larger2";
          }

          string contentClass = string.Empty;
          if (ContentAlign == ContentAlign.Center) {
            contentClass = "form-col-align-center";
          } else if (ContentAlign == ContentAlign.Right) {
            contentClass = "form-col-align-right";
          }

          if (LabelPosition == LabelPosition.Above) {
            // Content is below label.
            label_col_md = BootstrapCols.Total;
            content_col_md = BootstrapCols.Total;
            label_col_lg = BootstrapCols.Total;
            content_col_lg = BootstrapCols.Total;
            rowClasses += " formrow-labelpos-above";
          } else if (LabelPosition == LabelPosition.LeftLegacy) {
            // Wide label, narrow content.
            label_col_md = 2;
            content_col_md = 10;
            label_col_lg = 2;
            content_col_lg = 10;
            rowClasses += " formrow-labelpos-left-legacy";
          } else if (LabelPosition == LabelPosition.LeftWide) {
            // Wide label, narrow content.
            label_col_md = 10;
            content_col_md = 2;
            label_col_lg = 6;
            content_col_lg = 2;
            rowClasses += " formrow-labelpos-left-wide";
          } else if (LabelPosition == LabelPosition.LeftFitContent) {
            // Wide label, narrow content.
            label_col_md = 0;
            content_col_md = 0;
            label_col_lg = 0;
            content_col_lg = 0;
            rowClasses += " formrow-labelpos-left-fitcontent";
          } else {
            // Normal - narrow label, wide content.
            label_col_md = BootstrapCols.FormLabel_md;
            content_col_md = BootstrapCols.FormContent_md;
            label_col_lg = BootstrapCols.FormLabel_lg;
            content_col_lg = BootstrapCols.FormContent_lg;
            rowClasses += " formrow-labelpos-left-normal";
          }

          string colLabelClass = LabelPosition == LabelPosition.LeftLegacy ? "control-label" : "";

          return $@"
            <div class=""row form-group formrow ajaxSubmit-field {rowClasses}"">
              <div class=""formrow-col-label {colLabelClass} col-lg-{label_col_lg} col-md-{label_col_md} col-sm-12 col-xs-12"">
                <label class=""control-label"">{LabelText.HTMLEncode()}</label>
                {labelHelpHtml}
              </div>
              <div class=""formrow-col-content col-lg-{content_col_lg} col-md-{content_col_md} col-sm-12 col-xs-12 {contentClass.HTMLEncode()}"">
                {ContentHtml}
              </div>
            </div>";
        }
      }

      private static string ReadOnlyAttr(bool isReadOnly) => isReadOnly ? "readonly tabindex=\"-1\"" : string.Empty;

      public class TextInput {

        public string InputName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Placeholder { get; set; } = string.Empty;
        public string Classes { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public string Attributes { get; set; } = string.Empty;
        public string RightHtml = string.Empty;
        public bool IsReadOnly { get; set; } = false;
        public bool AutoComplete { get; set; } = false;
        public ControlWidthPreset WidthPreset { get; set; } = ControlWidthPreset.None;
        public InputMaxLength MaxLength { get; set; } = InputMaxLength.NoLimit;

        // For Dynamic URL Input (e.g Calendly Url on Profile Page)
        public string LeftSideLabelText { get; set; } = string.Empty;
        public string RightSideLabelText { get; set; } = string.Empty;

        public string ToHtml() {

          string widthPresetClass = GetControlWidthPresetClass(WidthPreset);

          var html = new StringBuilder();
          bool hasSideLabels = !LeftSideLabelText.IsNullOrEmpty() || !RightSideLabelText.IsNullOrEmpty();

          html.Append($@"<div class=""control-container control-with-righthtml"">");

          if (hasSideLabels) {
            html.Append($@"<div class=""control-sidelabels-container"">");
          }
          if (!LeftSideLabelText.IsNullOrEmpty()) {
            html.Append($@"<label class=""control-sidelabel control-sidelabel-left"">{LeftSideLabelText.HTMLEncode()}</label>");
          }

          html.Append($@"<input {GetReadOnlyAttrs(IsReadOnly)} type=""{Type.HTMLEncode()}"" {GetMaxLengthAttr(MaxLength)}
            class=""form-control control-textinput {widthPresetClass} {Classes.HTMLEncode()}"" {Attributes.HTMLEncode()} name=""{InputName.HTMLEncode()}""
            value=""{Value.HTMLEncode()}"" placeholder=""{Placeholder.HTMLEncode()}"" {GetAutocompleteAttr(AutoComplete)} />");

          if (!RightSideLabelText.IsNullOrEmpty()) {
            html.Append($@"<label class=""control-sidelabel control-sidelabel-right"">{RightSideLabelText.HTMLEncode()}</label>");
          }
          if (hasSideLabels) {
            html.Append("</div>");
          }

          if (!RightHtml.IsNullOrEmpty()) {
            html.Append($@"<div class=""control-righthtml"">{RightHtml}</div>");
          }

          html.Append("</div>");

          return html.ToString();
        }
      }

      public class TextInputDual {

        public string Input1Name { get; set; } = string.Empty;
        public string Input1Value { get; set; } = string.Empty;
        public string Input1Placeholder { get; set; } = string.Empty;
        public string Input2Name { get; set; } = string.Empty;
        public string Input2Value { get; set; } = string.Empty;
        public string Input2Placeholder { get; set; } = string.Empty;
        public bool IsReadOnly { get; set; } = false;
        public bool Autofocus { get; set; } = false;
        public InputMaxLength MaxLength { get; set; } = InputMaxLength.NoLimit;

        public string ToHtml() {
          return $@"
            <div class=""input-text-dual"">
              <input {GetReadOnlyAttrs(IsReadOnly)} type=""text"" {GetMaxLengthAttr(MaxLength)} class=""form-control"""
                  + $@" name=""{Input1Name.HTMLEncode()}"" value=""{Input1Value.HTMLEncode()}"""
                  + $@" placeholder=""{Input1Placeholder.HTMLEncode()}"" {GetAutocompleteAttr(false)} {Autofocus.ToValue("autofocus")} />
              <input {GetReadOnlyAttrs(IsReadOnly)} type=""text"" {GetMaxLengthAttr(MaxLength)} class=""form-control"""
                  + $@" name=""{Input2Name.HTMLEncode()}"" value=""{Input2Value.HTMLEncode()}"""
                  + $@" placeholder=""{Input2Placeholder.HTMLEncode()}"" {GetAutocompleteAttr(false)} />
            </div>";
        }
      }

      public class AjaxInfo {
        public AjaxDataType DataType { get; set; } = AjaxDataType.Json;
        public string AlternateUrl { get; set; }
        public List<KeyValuePair<string, string>> FormData { get; set; } = new List<KeyValuePair<string, string>>();
      }
      public class AjaxSearch : AjaxInfo {
        public string SearchKey { get; set; }
      }

      public class Select<T> : FormElementInfo<T> {

        public int Size { get; set; } = 1;
        public bool IsMultiple { get; set; } = false;
        public bool NoSelect2 { get; set; } = false;
        public bool Select2WordWrap { get; set; } = false;
        public bool Autofocus { get; set; } = false;
        public string TopOptionsHtml { get; set; } = string.Empty;
        public string Placeholder { get; set; } = string.Empty;
        public string SearchPlaceholder { get; set; } = string.Empty;
        public AjaxSearch AjaxSearch { get; set; } = null;
        public ControlWidthPreset WidthPreset { get; set; } = ControlWidthPreset.Full;
        public List<SelectOption> Options { get; set; } = new List<SelectOption>();

        public string ToHtml() {

          var placeHolderData = new DataAttributes();
          if (!Placeholder.IsNullOrEmpty()) placeHolderData.Add("placeholder", Placeholder);
          if (!SearchPlaceholder.IsNullOrEmpty()) placeHolderData.Add("searchplaceholder", SearchPlaceholder);

          var classes = new List<string>();
          classes.Add("form-control");
          classes.Add(Class);
          classes.Add(GetControlWidthPresetClass(WidthPreset));
          if (NoSelect2) classes.Add("noselect2");
          if (Select2WordWrap) classes.Add("select2-word-wrap");

          if (AjaxSearch?.SearchKey != null) {
            DataAttributes = AddAjaxDataAttributes(AjaxSearch, DataAttributes);
            DataAttributes.Add(DataAttrName.AjaxSearchKey, AjaxSearch.SearchKey);
          }

          // Note about use of the control's Value vs Options which may have been pre-selected:
          // Value is inherited from FormElementInfo, but is only relevant for *single-selection* dropdowns.
          // For multi-select (IsMultiple == true):
          //  - Value has no effect - the individually selected options are selected.
          // For single-select:
          //  - If Value is null, the individually selected option is selected (there should only be one).
          //  - If Value is not null, then Value takes precedence, and any selected options of other values are deselected.

          if (!IsMultiple && Value != null && !Options.IsNullOrEmpty()) {

            // For any selected options, de-select any that aren't same as Value.
            bool valueAlreadySelected = false;
            Options.FindAll(o => o.Selected == true).ForEach(o => {
              if (!valueAlreadySelected && o.Value.Equals(Value)) {
                valueAlreadySelected = true; // Found a selected option with the right value.
              } else {
                o.Selected = false; // Not the same as Value, or already found one. Only one can be selected.
              }
            });
            if (!valueAlreadySelected) {
              var selectedOption = Options.Find(o => o.Value.Equals(Value));
              if (selectedOption != null) selectedOption.Selected = true;
            }
          }

          return $@"
            <select name=""{InputName.HTMLEncode()}"" size=""{Math.Max(Size, 1)}"" {ClassAttrHtml(classes)} {Autofocus.ToValue("autofocus")}
              {AttrHtml("id", ID, RenderAttr.IfHasValue)} {DataAttributes?.ToHTML()} {placeHolderData.ToHTML()}
              {AttrHtml("style", Style, RenderAttr.IfHasValue)} {ReadOnlyAttr(IsReadOnly)} {IsMultiple.ToValue("multiple")}>
                {TopOptionsHtml.EmptyIfNull()}
                {GetSelectOptionListHtml(Options)}
            </select>";
        }
      }

      // Add ajax info entries to the element's data attributes.
      // Note the result must be a returned reference to the object in case the passed object is null (pointers will differ).
      private static DataAttributes AddAjaxDataAttributes(AjaxInfo ajaxInfo, DataAttributes dataAttributes) {
        if (ajaxInfo == null) return dataAttributes;
        if (dataAttributes == null) dataAttributes = new DataAttributes();
        dataAttributes.Add(DataAttrName.AjaxAlternateUrl, ajaxInfo.AlternateUrl);
        dataAttributes.Add(DataAttrName.AjaxDataType, ajaxInfo.DataType);
        dataAttributes.Add(DataAttrName.AjaxFormData, ToFormUrlEncoded(ajaxInfo.FormData));
        return dataAttributes;
      }

      // Non-generic alias for the above for string type.
      public class Select : Select<string> { }

      public class CheckBox {

        // Note the iCheck lib will be phased out, so this new checkbox does not use iCheck.

        public string InputName;
        public string Value = DefaultCheckboxValue;
        public bool Checked = false;
        public bool Autofocus = false;
        public string Classes;
        public bool IsReadOnly = false;
        public string Label;
        public bool LabelIsHtml = false;
        public CheckboxLabelPosition LabelPosition = CheckboxLabelPosition.After;
        public string ID;
        public string Attributes;

        public string ToHtml() {

          string name = InputName.HTMLEncode();
          string value = Value.HTMLEncode();
          string id = ID ?? $"chk_{name}_{value}";

          string labelHtml = string.Empty;
          if (!Label.IsNullOrEmpty()) {
            labelHtml = $@"<div class=""checkbox-group-label user-select-none""><label tabindex=""-1"" for=""{id.HTMLEncode()}"">{(LabelIsHtml ? Label : Label.HTMLEncode())}</label></div>";
          }

          string html = $@"
            <div class=""checkbox-group {Classes.HTMLEncode()}"">
              {(!labelHtml.IsNullOrEmpty() && LabelPosition == CheckboxLabelPosition.Before).ToValue(labelHtml)}
              <div class=""checkbox-group-checkbox"">
                <div class=""checkbox-styled"">
                  <input type=""checkbox"" {Checked.ToValue("checked")} {IsReadOnly.ToValue("readonly")} {Autofocus.ToValue("autofocus")}"
                  + $@" id=""{id}"" name=""{name}"" value=""{value}"" {Attributes.HTMLEncode()} />
                </div>
              </div>
              {(!labelHtml.IsNullOrEmpty() && LabelPosition == CheckboxLabelPosition.After).ToValue(labelHtml)}
            </div>";

          return html;
        }
      }

      public class TextArea {

        public string InputName = string.Empty;
        public bool IsReadOnly = false;
        public string Value = string.Empty;
        public string Classes = string.Empty;
        public bool IsRichText = false;

        public string ToHtml() {

          return $@"<textarea id=""txt{InputName}"" {GetReadOnlyAttrs(IsReadOnly)} rows = ""3""
            class=""form-control {Classes.HTMLEncode()} {(IsRichText ? "tinymce displaynone" : "")}""
            name=""{InputName.HTMLEncode()}"">{Value.HTMLEncode()}</textarea>";
        }
      }

      private static string GetNewComponentID() {
        var rnd = new Random();
        return rnd.Next(100, 999).ToString(); // Meant to be unique for this component on the page.
      }

      public class ImageWithUpload {

        public readonly ImageType ImageType;
        public readonly string Src;
        public readonly string AjaxAction;
        public readonly bool EnableUpload;
        public string ButtonText = "Select an image";
        public string MessageUnderButton;
        public bool ButtonOnRight;
        public string AltText;

        public readonly string ImgID, InputID, InputContainerID, InputMessageID;

        private const string ImageIdPrefix = "image-upload-img-";
        private const string InputIdPrefix = "image-upload-input-";
        private const string InputMessageIdPrefix = "image-upload-input-message-";
        private const string InputContainerIdPrefix = "image-upload-input-container-";

        public ImageWithUpload(string src, ImageType imageType, string ajaxAction, bool enableUpload = false) {

          Src = src;
          ImageType = imageType;
          AjaxAction = ajaxAction;
          EnableUpload = enableUpload;

          ImgID = ImageIdPrefix + AjaxAction;
          InputID = InputIdPrefix + AjaxAction;
          InputMessageID = InputMessageIdPrefix + AjaxAction;
          InputContainerID = InputContainerIdPrefix + AjaxAction;
        }

        public string ToHtml() {

          if (Src.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Src));
          if (AjaxAction.IsNullOrEmpty()) throw new ArgumentNullException(nameof(AjaxAction));

          string imgClass = string.Empty;
          if (ImageType == ImageType.ProfileImage) {
            imgClass = CSSClasses.Images.ProfileImage;
          } else if (ImageType == ImageType.CompanyLogo) {
            imgClass = CSSClasses.Images.CompanyLogo;
          }

          string containerClass = ButtonOnRight
            ? "flex flex-align-center gap20"
            : "flex flex-column gap5";

          string html = $@"
            <div class=""control-container image-upload {containerClass}"">
              <div class=""image-upload-img"">
                <img {GetUploadImgDataAttrs(AjaxAction)} {AttrHtml("class", imgClass, RenderAttr.IfHasValue)}
                     {AttrHtml("id", ImgID, RenderAttr.Always)} {AttrHtml("src", Src, RenderAttr.Always)} {AttrHtml("alt", AltText, RenderAttr.IfHasValue)} />
              </div>";

          if (EnableUpload) {
            html += $@"
              <div class=""image-upload-input"" {AttrHtml("id", InputContainerID, RenderAttr.Always)}>
                {(!ButtonOnRight ? @"<h4>Change Image:</h4>" : string.Empty)}
                <input {AttrHtml("id", InputID, RenderAttr.Always)} type=""file"" class=""hidden"" accept=""image/*"" title=""{ButtonText.HTMLEncode()}"" />
                <label {AttrHtml("for", InputID, RenderAttr.Always)} class=""btn btn-secondary"">{ButtonText.HTMLEncode()}</label>
                <div class=""image-upload-button-message"" {AttrHtml("id", InputMessageID, RenderAttr.Always)}>{MessageUnderButton.HTMLEncode()}</div>
              </div>";
          }

          html += $@"</div> ";

          return html;
        }
      }

      public static string GetUploadImgDataAttrs(string ajaxAction) {
        return AttrHtml("data-" + DataAttrName.AjaxAction, ajaxAction, RenderAttr.Always);
      }

      public class SectionTitle {

        public string TitleText = string.Empty;
        public string HelpText = string.Empty;
        public string HelpLinkUrl = string.Empty;
        public string Classes = string.Empty;
        public bool BottomMargin = true;

        public void WriteHtml() => SystemWeb.ResponseWrite(ToHtml());

        public string ToHtml() {

          if (TitleText.IsNullOrEmpty()) return string.Empty;

          var helpHtml = string.Empty;
          if (!HelpText.IsNullOrEmpty()) {
            helpHtml = $@"<div class=""form-section-help"">{HelpText.HTMLEncode()}";
            if (!HelpLinkUrl.IsNullOrEmpty()) {
              helpHtml += $@" <a href=""{HelpLinkUrl.HTMLEncode()}"" target=""_blank"">{DefultHelpLinkHtml}</a>";
            }
            helpHtml += $@"</div>";
          }

          return $@"
            <div class=""form-section-title {Classes} {(BottomMargin ? "" : "bottom-margin-false")}"">
              <h3 class=""border-bottom"">{TitleText.HTMLEncode()}</h3>
              {helpHtml}
            </div>";
        }
      }

      public class DatePicker {

        public string InputName = string.Empty;
        public DateTime? Value;
        public string Classes = string.Empty;
        public bool IsReadOnly = false;
        public string RightHtml = string.Empty;

        string widthPresetClass = GetControlWidthPresetClass(ControlWidthPreset.DatePicker);

        public string ToHtml() {
          return $@"
            <div class=""control-container control-with-righthtml"">
              <div class=""input-group date {widthPresetClass} {Classes.HTMLEncode()}"" data-customfield=""{InputName.HTMLEncode()}"">
                <input type=""text"" class=""form-control control-datepicker"" {ReadOnlyAttr(IsReadOnly)} name=""{InputName.HTMLEncode()}"" value=""{DisplayDate(Value)}"">
                <div class=""input-group-addon""><span class=""glyphicon glyphicon-th""></span></div>
              </div>
              <div class=""control-righthtml"">{RightHtml}</div>
            </div>";
        }
      }

      public class ButtonGroup {

        public string InputName = string.Empty;
        public string Value;
        public string Classes = string.Empty;
        public bool IsReadOnly = false;
        public List<ButtonGroupButton> Buttons = null;
        public ControlWidthPreset WidthPreset = ControlWidthPreset.None;

        public ButtonGroup(string inputName, List<ButtonGroupButton> buttons, string selectedValue) {
          InputName = inputName;
          Buttons = buttons;
          Value = selectedValue;
        }

        public string ToHtml() {

          string classes = $"btn-group btn-group-toggle {Classes.HTMLEncode()} {IsReadOnly.ToValue("disabled")}"
            + $" {GetControlWidthPresetClass(WidthPreset)} {GetButtonGroupClass(InputName)}";

          return $@"
            <div class=""{classes}"" data-toggle=""buttons"">
              {GetButtonGroupButtons(InputName, Buttons, Value, IsReadOnly)}
            </div>";
        }
      }
    }
  }
}
