using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wbooru.Kernel.Updater;
using Wbooru.PluginExt;
using Wbooru.Utils;

namespace WbooruPlugin.Danbooru
{
    [Export(typeof(PluginInfo))]
    public class DanbooruPluginInfo : PluginInfo , IPluginUpdatable
    {
        public override string PluginName => "Danbooru";

        public override string PluginProjectWebsite => "https://github.com/Wbooru/WbooruPlugin.Danbooru";

        public override string PluginAuthor => "MikiraSora";

        public override string PluginDescription => "Provide images source named Danbooru";

        public Version CurrentPluginVersion => GetType().Assembly.GetName().Version;

        public IEnumerable<ReleaseInfo> GetReleaseInfoList()
        {
            return UpdaterHelper.GetGithubAllReleaseInfoList("Wbooru", "WbooruPlugin.Danbooru");
        }
    }
}
