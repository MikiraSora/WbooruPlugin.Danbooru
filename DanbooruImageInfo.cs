using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wbooru.Models.Gallery;

namespace WbooruPlugin.Danbooru
{
    public class DanbooruImageInfo: GalleryItem
    {
        public GalleryImageDetail ImageDetail { get; set; }
    }
}
