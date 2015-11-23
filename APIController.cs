namespace VkNetExtend
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;

    using VkNet;
    using VkNet.Model;
    using VkNet.Model.RequestParams.Wall;
    using VkNet.Enums.Filters;

    public static class APIController
    {
        private static Hashtable _lockers = new Hashtable();

        private static void _checkLocker(VkApi api)
        {
            if (!string.IsNullOrEmpty(api.AccessToken))
            {
                if (!_lockers.ContainsKey(api.AccessToken))
                    _lockers.Add(api.AccessToken, new object());
            }
            else
                throw new NotImplementedException("Не авторизовано");
        }

        public static bool Authorize(VkApi api, int appId, string login, string password)
        {
            api.Authorize(appId, login, password, Settings.All);
            if (!string.IsNullOrEmpty(api.AccessToken))
            {
                api.OnTokenExpires += onTokenExpires;
                return true;
            }
            return false;
        }
        public static Task<bool> AuthorizeAsync(VkApi api, int appId, string login, string password)
        {
            return Task.Run(() =>
            {
                return Authorize(api, appId, login, password);
            });
        }
        private static void onTokenExpires(VkApi api)
        {
            if (!string.IsNullOrEmpty(api.AccessToken))
            {
                _lockers.Remove(api.AccessToken);
                api.RefreshToken();
                _checkLocker(api);
            }
        }

        public static User GetUser(VkApi api, long id, ProfileFields fields = null)
        {
            _checkLocker(api);

            if (fields == null)
                fields = ProfileFields.AllUndocumented;
            User res = null;

            lock (_lockers[api.AccessToken])
                res = api.Users.Get(id, fields);
            return res;
        }
        public static Task<User> GetUserAsync(VkApi api, long id, ProfileFields fields = null)
        {
            return Task.Run(() => { return GetUser(api, id, fields); });
        }

        public static IEnumerable<Group> GetGroups(VkApi api, IEnumerable<long> gids, GroupsFields fields = null)
        {
            _checkLocker(api);

            if (fields == null)
                fields = GroupsFields.AllUndocumented;
            ReadOnlyCollection<Group> res = null;

            lock (_lockers[api.AccessToken])
                res = api.Groups.GetById(gids, fields);
            return res;
        }
        public static Task<IEnumerable<Group>> GetGroupsAsync(VkApi api, IEnumerable<long> gids, GroupsFields fields = null)
        {
            return Task.Run(() => { return GetGroups(api, gids, fields); });
        }

        public static List<Message> LoadDialogs(VkApi api, uint offset = 0, uint count = 20)
        {
            _checkLocker(api);
            ReadOnlyCollection<Message> res = null;
            int total;
            lock (_lockers[api.AccessToken])
            {
                res = api.Messages.GetDialogs(out total, out total, (int)count, (int)offset);
            }
            return res.ToList();
        }
        public static Task<List<Message>> LoadDialogsAsync(VkApi api, uint offset = 0, uint count = 20)
        {
            return Task.Run(() => { return LoadDialogs(api, offset, count); });
        }

        public static long GetFixedPost(VkApi api, long gid)
        {
            IEnumerable<Group> gs = GetGroups(api, new long[] { gid }, GroupsFields.FixedPostId);
            var g = (gs as ReadOnlyCollection<Group>).FirstOrDefault();
            return g.FixedPostId.HasValue ? g.FixedPostId.Value : -1;
        }
        public static async Task<long> GetFixedPostAsync(VkApi api, long gid)
        {
            var gs = await GetGroupsAsync(api, new long[] { gid }, GroupsFields.FixedPostId);
            var g = (gs as ReadOnlyCollection<Group>).FirstOrDefault();
            return g.FixedPostId.HasValue ? g.FixedPostId.Value : -1;
        }

        public static WallGetObject GetWallPosts(VkApi api, long ownerId, uint offset, uint count = 20)
        {
            _checkLocker(api);
            WallGetObject res = null;

            WallGetParams p = new WallGetParams();
            p.OwnerId = ownerId;
            p.Count = count;
            p.Offset = offset;

            lock (_lockers[api.AccessToken])
                res = api.Wall.Get(p);
            return res;

        }
        public static Task<WallGetObject> GetWallPostsAsync(VkApi api, long ownerId, uint offset, uint count = 20)
        {
            return Task.Run(() =>
            {
                return GetWallPosts(api, ownerId, count, offset);
            });
        }

    }
}
