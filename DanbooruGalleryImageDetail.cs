using System;
using System.Diagnostics;
using Wbooru.Models.Gallery;
using Wbooru.Models.Gallery.Annotation;

namespace WbooruPlugin.Danbooru
{
    public class DanbooruGalleryImageDetail: GalleryImageDetail
    {
        [DisplayAutoIgnore]
        [DisplayName("P站ID")]
        [DisplayClickAction(nameof(OnPixivIdClicked))]
        public string PixivId { get; set; }

        //ref public delegate bool OnClickCallBack(object property_object, object state);
        private bool OnPixivIdClicked(object property_object, object state)
        {
            var id = property_object?.ToString();
            var url = $"https://www.pixiv.net/artworks/{id}";

            Process.Start(url);

            return true;
        }
    }
}