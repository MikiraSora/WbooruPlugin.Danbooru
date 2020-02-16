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
using Wbooru.Models.Gallery;
using Wbooru.Network;
using Wbooru.Utils;

namespace WbooruPlugin.Danbooru
{
    [Export(typeof(Gallery))]
    public class DanbooruGallery : Gallery
    {
        public override string GalleryName => "Danbooru";

        public override GalleryItem GetImage(string id)
        {
            try
            {
                return BuildItem(RequestHelper.GetJsonContainer<JObject>(RequestHelper.CreateDeafult($"https://danbooru.donmai.us/posts/{id}.json")));
            }
            catch (Exception e)
            {
                ExceptionHelper.DebugThrow(e);
                return null;
            }
        }

        public override GalleryImageDetail GetImageDetial(GalleryItem item)
        {
            if (item is DanbooruImageInfo info)
                return info.ImageDetail;

            if (item.GalleryName != GalleryName)
                throw new Exception($"Can't get image detail with different {item.GalleryName} gallery item.");

            return (GetImage(item.GalleryItemID) as DanbooruImageInfo)?.ImageDetail;
        }

        public override IEnumerable<GalleryItem> GetMainPostedImages()
        {
            return GetImagesInternal();
        }

        private IEnumerable<GalleryItem> GetImagesInternal(IEnumerable<string> keywords=null,int page=1)
        {
            var base_url = $"https://danbooru.donmai.us/posts.json?limit=200";

            if (keywords?.Any() ?? false)
                base_url = base_url + $"&tags={string.Join("+",keywords)}";

            while (true)
            {
                JArray json;

                try
                {
                    var actual_url = $"{base_url}page={page}";

                    var response = RequestHelper.CreateDeafult(actual_url);
                    using var reader = new StreamReader(response.GetResponseStream());

                    json = JsonConvert.DeserializeObject(reader.ReadToEnd()) as JArray;

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

        private GalleryItem BuildItem(JToken x)
        {
            var image_info = new DanbooruImageInfo();

            image_info.GalleryItemID = x["id"].ToString();
            image_info.GalleryName = GalleryName;

            //var size = new Size(, );
            var width = x["image_width"].ToObject<int>();
            var height = x["image_height"].ToObject<int>();
            var size = new Size(width, height);
            /*
             * Danbooru实际网站钦定网格150px正方形限制，可以快速推算
             */
            var preview_size = width > height ? new Size(150, 150 * height / width) : new Size(150 * width / height, 150);

            image_info.PreviewImageSize = preview_size;
            image_info.PreviewImageDownloadLink = x["preview_file_url"]?.ToString();

            image_info.GalleryItemID = x["id"].ToString();

            image_info.ImageDetail = new DanbooruGalleryImageDetail()
            {
                ID = image_info.GalleryItemID,
                CreateDate = x["created_at"].ToObject<DateTime>(),
                Updater = x["uploader_name"].ToString(),
                Author = x["tag_string_artist"].ToString(),
                Rate = x["rating"].ToString(),
                Resolution = size,
                Score = x["score"].ToString(),
                PixivId = x["pixiv_id"].ToString(),
                Source = x["source"].ToString(),
                Tags = x["tag_string"].ToString().Split(' '),
                DownloadableImageLinks = (new []
                {
                    new DownloadableImageLink()
                    {
                        Description = "File",
                        DownloadLink = x["file_url"]?.ToString(),
                        FileLength=0,
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
                }).Where(x=>!string.IsNullOrWhiteSpace(x.DownloadLink)).ToArray()
            };

            image_info.DownloadFileName = $"{image_info.GalleryItemID} {string.Join(" ", image_info.ImageDetail.Tags)}";

            if (string.IsNullOrWhiteSpace(image_info.PreviewImageDownloadLink) || !image_info.ImageDetail.DownloadableImageLinks.Any())
            {
                Log.Warn($"Can't get info for image id {image_info.GalleryItemID},maybe it's require account login or higher account permission. SKIPED", "Danbooru.BuildItem");
                return null;
            }

            return image_info;
        }
    }
}
