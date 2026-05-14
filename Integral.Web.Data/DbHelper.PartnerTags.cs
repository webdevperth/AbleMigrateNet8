using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class PartnerTags {

      public class GetAllTagsParams {
        public bool OnlyAssignedTags { get; set; } = false;
        public bool IncludeUIHiddenTags { get; set; } = false;
        public bool OnlyPartnerTags { get; set; } = false;
        public int? UserId { get; set; } = null;
      }

      // Gets the complete list of categories and all their tags.
      public static AllTagInfo GetAllTags(GetAllTagsParams @params) {

        if (@params == null) @params = new GetAllTagsParams();

        var allTagInfo = new AllTagInfo();
        TagCategoryInfo thisCatInfo = null;

        Common.Query($@"
          SELECT
            ptc.PartnerTagCategoryId, ptc.CategoryName, ptc.IsPartners,
            pt.PartnerTagId, pt.TagName, pt.PartnerCanEdit, pt.PartnerUIHidden
          FROM al_PartnerTagCategory ptc
          INNER JOIN al_PartnerTag pt ON ptc.PartnerTagCategoryId = pt.PartnerTagCategoryId
          WHERE (@IncludeUIHidden = 1 OR pt.PartnerUIHidden = 0)
          {(@params.OnlyAssignedTags ? "AND EXISTS(SELECT NULL FROM al_PartnerTagToUser tu WHERE tu.PartnerTagId = pt.PartnerTagId)" : "")}
          {(@params.OnlyPartnerTags ? "AND ptc.IsPartners = 1" : "")}
          ORDER BY ptc.CategoryName, pt.PartnerTagCategoryId, pt.TagName, pt.PartnerTagId",
          dr => {
            int thisCatId = dr.GetInt("PartnerTagCategoryId");
            int thisTagId = dr.GetInt("PartnerTagId");
            if (thisCatInfo == null || thisCatInfo.CategoryId != thisCatId) {
              // Add new category to the list.
              thisCatInfo = new TagCategoryInfo(thisCatId, dr.GetString("CategoryName"));
              allTagInfo.CategoryList.Add(thisCatInfo);
            }
            // Add tag to the current category.
            thisCatInfo.AddTag(new TagInfo(
              thisCatInfo,
              thisTagId,
              dr.GetString("TagName"),
              dr.GetBoolFromInt("PartnerCanEdit"),
              dr.GetBoolFromInt("PartnerUIHidden")
            ));
            allTagInfo.CategoryByTagId.Add(thisTagId, thisCatInfo);
          },
          Common.NewSqlParameter("IncludeUIHidden", @params.IncludeUIHiddenTags ? 1 : 0));
        return allTagInfo;
      }

      public static List<TagCategoryInfo> GetPartnerTags(GetAllTagsParams @params) {

        if (@params == null) @params = new GetAllTagsParams();
        var partnerTags = new List<TagCategoryInfo>();

        Common.Query($@"
          SELECT
            ptc.PartnerTagCategoryId, ptc.CategoryName, ptc.IsContent, ptc.IsPartners,
            pt.PartnerTagId, pt.TagName, pt.PartnerCanEdit, pt.PartnerUIHidden,
            {(@params.UserId.HasValue ? "IIF(ptu.PartnerTagId IS NULL, 0, 1)" : "0")} AS IsSelected
          FROM al_PartnerTagCategory ptc
          LEFT JOIN al_PartnerTag pt ON pt.PartnerTagCategoryId = ptc.PartnerTagCategoryId
          {(@params.UserId.HasValue ? $@"LEFT JOIN al_PartnerTagToUser ptu ON ptu.PartnerTagId = pt.PartnerTagId {(@params.OnlyAssignedTags ? "" : "AND ptu.UserId = @UserId")}" : "")}
          WHERE (@IncludeUIHidden = 1 OR pt.PartnerUIHidden = 0)
          {(@params.OnlyPartnerTags ? "AND ptc.IsPartners = 1" : "")}
          {(@params.OnlyAssignedTags ? "AND ptu.UserId = @UserId" : "")}
          ORDER BY ptc.CategoryName, pt.PartnerTagCategoryId, pt.TagName, pt.PartnerTagId",
          dr => {
            int categoryId = dr.GetInt("PartnerTagCategoryId");
            var category = partnerTags.FirstOrDefault(c => c.CategoryId == categoryId);
            if (category == null) {
              category = new TagCategoryInfo(dr);
              partnerTags.Add(category);
            }
            var tag = new TagInfo(dr);
            category.TagInfoList.Add(tag);
          },
          Common.NewSqlParameter("IncludeUIHidden", @params.IncludeUIHiddenTags ? 1 : 0),
          Common.NewSqlParameter("UserId", @params.UserId)
        );

        return partnerTags;
      }

      public static void UpdatePartnerTags(AlbertCoaches.AlbertCoachInfo coachInfo, List<int> partnerTagIds) {

        if (coachInfo == null) return;

        Common.UsingTransaction(trans => {

          Common.GetNonQueryInt(trans, @"
            DELETE FROM al_PartnerTagToUser
            WHERE UserId = @UserId",
            Common.NewSqlParameter("UserId", coachInfo.UserId)
          );

          foreach (var tagId in partnerTagIds) {
            Common.GetNonQueryInt(trans, @"
              INSERT INTO al_PartnerTagToUser (UserId, PartnerTagId)
              VALUES (@UserId, @PartnerTagId)",
              Common.NewSqlParameter("UserId", coachInfo.UserId),
              Common.NewSqlParameter("PartnerTagId", tagId)
            );
          }
          return true;
        });

        AlbertCoaches.UpdateIsPartnerActive(null, coachInfo);
      }

      public class AllTagInfo {
        public Dictionary<int, TagCategoryInfo> CategoryByTagId = new Dictionary<int, TagCategoryInfo>();
        public List<TagCategoryInfo> CategoryList = new List<TagCategoryInfo>();
      }

      public class TagCategoryInfo {
        public int CategoryId { get; private set; }
        public string CategoryName { get; private set; }
        public bool IsContent { get; private set; }
        public bool IsPartners { get; private set; }
        public List<TagInfo> TagInfoList { get; private set; } = new List<TagInfo>();
        public Dictionary<int, TagInfo> TagsById { get; private set; } = new Dictionary<int, TagInfo>();
        public TagCategoryInfo(int categoryId, string categoryName) {
          CategoryId = categoryId;
          CategoryName = categoryName;
        }
        public TagCategoryInfo(SqlDataReader dr) {
          this.CategoryId = dr.GetInt("PartnerTagCategoryId");
          this.CategoryName = dr.GetString("CategoryName");
          this.IsContent = dr.GetBoolFromInt("IsContent");
          this.IsPartners = dr.GetBoolFromInt("IsPartners");
          this.TagInfoList = new List<TagInfo>();
        }
        public void AddTag(TagInfo tagInfo) {
          TagInfoList.Add(tagInfo);
        }
      }

      public class TagInfo {
        public int CategoryId { get; private set; }
        public int TagId { get; private set; }
        public string TagName { get; private set; }
        public bool PartnerCanEdit { get; private set; }
        public bool PartnerUIHidden { get; private set; }
        public bool IsSelected { get; private set; }
        public TagInfo(TagCategoryInfo category, int tagId, string tagName, bool partnerCanEdit, bool partnerUIHidden) {
          CategoryId = category.CategoryId;
          TagId = tagId;
          TagName = tagName;
          PartnerCanEdit = partnerCanEdit;
          PartnerUIHidden = partnerUIHidden;
          category.TagsById.Add(tagId, this);
        }

        public TagInfo(SqlDataReader dr) {
          this.CategoryId = dr.GetInt("PartnerTagCategoryId");
          this.TagId = dr.GetInt("PartnerTagId");
          this.TagName = dr.GetString("TagName");
          this.PartnerCanEdit = dr.GetBoolFromInt("PartnerCanEdit");
          this.PartnerUIHidden = dr.GetBoolFromInt("PartnerUIHidden");
          this.IsSelected = dr.GetBoolFromInt("IsSelected");
        }
      }

    }
  }
}
