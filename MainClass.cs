using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkNetExtend
{
    using VkNet;

    public class VKController
    {
        private VkApi _api;

        public VKController(VkApi api, int applicationId)
        {
            _api = api;
        }
    }
}
