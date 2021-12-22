using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using Wbooru;
using Wbooru.Galleries;
using Wbooru.Galleries.SupportFeatures;
using Wbooru.Models;
using Wbooru.Models.Gallery;
using Wbooru.Network;
using Wbooru.Settings;
using Wbooru.UI.Controls;
using Wbooru.UI.Dialogs;
using Wbooru.UI.Pages;
using Wbooru.Utils;

namespace WbooruPlugin.Danbooru
{
    [Export(typeof(Gallery))]
    public class DanbooruGallery : Gallery, IGallerySearchImage, IGalleryItemIteratorFastSkipable, IGalleryNSFWFilter
    {
        public override string GalleryName => "Danbooru";

        #region Base

        public override async Task<GalleryItem> GetImage(string id)
        {
            try
            {
                return BuildItem(RequestHelper.GetJsonContainer<JObject>(await RequestHelper.CreateDeafultAsync($"https://danbooru.donmai.us/posts/{id}.json")));
            }
            catch (Exception e)
            {
                ExceptionHelper.DebugThrow(e);
                return null;
            }
        }

        public override async Task<GalleryImageDetail> GetImageDetial(GalleryItem item)
        {
            if (item is DanbooruImageInfo info)
                return info.ImageDetail;

            if (item.GalleryName != GalleryName)
                throw new Exception($"Can't get image detail with different {item.GalleryName} gallery item.");

            return (await GetImage(item.GalleryItemID) as DanbooruImageInfo)?.ImageDetail;
        }

        public override IAsyncEnumerable<GalleryItem> GetMainPostedImages()
        {
            return GetImagesInternal();
        }

        private async IAsyncEnumerable<GalleryItem> GetImagesInternal(IEnumerable<string> keywords = null, int page = 1)
        {
            var limit = Setting<GlobalSetting>.Current.GetPictureCountPerLoad;
            var base_url = $"https://danbooru.donmai.us/posts.json?limit=200&";

            if (keywords?.Any() ?? false)
            {
                if (keywords.Count() > 2)
                {
                    await App.Current.Dispatcher.InvokeAsync(() => Dialog.ShowDialog("不支持超过2个标签的搜索.", "Danbooru标签搜索"));
                    yield break;
                }

                base_url = base_url + $"tags={string.Join("+", keywords)}&";
            }

            while (true)
            {
                JArray json = null;

                try
                {
                    var actual_url = $"{base_url}page={page}";

                    var response = await RequestHelper.CreateDeafultAsync(actual_url);
                    using var reader = new StreamReader(response.GetResponseStream());

                    json = JsonConvert.DeserializeObject(await reader.ReadToEndAsync()) as JArray;

                    if (json.Count == 0)
                        break;

                    page++;
                }
                catch (Exception e)
                {
                    Log.Error($"Get image failed {e.Message}, but It still continue to fetch..");
                    json = null;
                }

                if (json != null)
                    foreach (var item in json.Select(x => BuildItem(x)).OfType<GalleryItem>())
                    {
                        yield return item;
                    }
            }
        }

        private string generateUnstandardId(JToken x)
        {
            string a(JToken x) => x?.ToString() ?? "moemoe";
            var buildStr = a(x["approver_id"]) + a(x["tag_string"]) + a(x["created_at"]);
            return buildStr.CalculateMD5();
        }

        private GalleryItem BuildItem(JToken x)
        {
            var image_info = new DanbooruImageInfo();

            var id = x["id"]?.ToString() ?? generateUnstandardId(x);

            image_info.GalleryItemID = id;
            image_info.GalleryName = GalleryName;

            //var size = new Size(, );
            var width = x["image_width"].ToObject<int>();
            var height = x["image_height"].ToObject<int>();
            var size = new Size(width, height);
            /*
             * Danbooru实际网站钦定网格150px正方形限制，可以快速推算
             */
            var preview_size = width > height ? new ImageSize(150, 150 * height / width) : new ImageSize(150 * width / height, 150);

            image_info.PreviewImageSize = preview_size;
            image_info.PreviewImageDownloadLink = x["preview_file_url"]?.ToString();

            image_info.GalleryItemID = id;

            image_info.ImageDetail = new DanbooruGalleryImageDetail()
            {
                ID = image_info.GalleryItemID,
                CreateDate = x["created_at"].ToObject<DateTime>(),
                Updater = x["uploader_name"]?.ToString(),
                Author = x["tag_string_artist"]?.ToString(),
                Rate = x["rating"].ToString(),
                Resolution = size,
                Score = x["score"].ToString(),
                PixivId = x["pixiv_id"]?.ToString(),
                Source = x["source"]?.ToString(),
                Tags = x["tag_string"].ToString().Split(' '),
                DownloadableImageLinks = (new[]
                {
                    new DownloadableImageLink()
                    {
                        Description = "File",
                        DownloadLink = x["file_url"]?.ToString(),
                        FileLength=x["file_size"]?.ToObject<long>()??0,
                        FullFileName = WebUtility.UrlDecode(Path.GetFileName(x["file_url"]?.ToString()??string.Empty)),
                        Size = size
                    },
                    new DownloadableImageLink()
                    {
                        Description = "Large File",
                        DownloadLink = x["large_file_url"]?.ToString(),
                        FileLength=0,
                        FullFileName = WebUtility.UrlDecode(Path.GetFileName(x["large_file_url"]?.ToString()??string.Empty)),
                        Size = size
                    }
                }).Where(x => !string.IsNullOrWhiteSpace(x.DownloadLink)).ToArray()
            };

            image_info.DownloadFileName = $"{image_info.GalleryItemID} {string.Join(" ", image_info.ImageDetail.Tags)}";

            if (string.IsNullOrWhiteSpace(image_info.PreviewImageDownloadLink) || !image_info.ImageDetail.DownloadableImageLinks.Any())
            {
                Log.Warn($"Can't get info for image id {image_info.GalleryItemID},maybe it's require account login or higher account permission. SKIPED", "Danbooru.BuildItem");
                return null;
            }

            return image_info;
        }

        #endregion

        #region IGallerySearchImage

        public IAsyncEnumerable<GalleryItem> SearchImagesAsync(IEnumerable<string> keywords)
        {
            return GetImagesInternal(keywords);
        }

        #endregion

        #region IGalleryItemIteratorFastSkipable

        public IAsyncEnumerable<GalleryItem> IteratorSkipAsync(int skip_count)
        {
            var limit_count = Setting<GlobalSetting>.Current.GetPictureCountPerLoad;

            var page = skip_count / limit_count + 1;
            skip_count = skip_count % Setting<GlobalSetting>.Current.GetPictureCountPerLoad;

            return GetImagesInternal(null, page).Skip(skip_count);
        }

        #endregion

        #region IGalleryNSFWFilter

        public IAsyncEnumerable<GalleryItem> NSFWFilter(IAsyncEnumerable<GalleryItem> items) => items.Where(x => NSFWFilter(x));

        public bool NSFWFilter(GalleryItem item)
        {
            if (!Setting<GlobalSetting>.Current.EnableNSFWFileterMode)
                return true;

            if (item is DanbooruImageInfo pi)
            {
                if (pi.ImageDetail.RatingInternal == DanbooruGalleryImageDetail.Rating.Safe)
                    return true;

                if (pi.ImageDetail.RatingInternal == DanbooruGalleryImageDetail.Rating.Questionable && !Setting<DanbooruSetting>.Current.QuestionableIsNSFW)
                    return true;

                return false;
            }

            return false;
        }

        #endregion
    }
}
