using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VkNetExtend
{
    using System.Threading;

    using VkNet;
    using VkNet.Model;
    using VkNet.Exception;
    using VkNet.Model.RequestParams;

    public delegate void PostsListDelegate(List<Post> posts);
    public delegate void FixedPostChangedDelegate(long ownerId, long pid);

    public class WallWatcher
    {
        private VkApi API { get; set; }

        public long OwnerId { get; private set; }
        public bool Active { get; private set; }
        public long LastPostId { get; private set; }
        public long FixedPostId { get; private set; }

        /// <summary>
        /// Вызывает событие <see cref="NewPosts"/> как только получает массив постов, а не когда загрузит запрашиваемые записи
        /// </summary>
        public bool? InformImmediately { get; private set; }

        #region Управление слежением
        private Timer _watchTimer = null;

        public byte MaxSleepSteps = 3;
        public int SteepSleepTime = 5000;
        private byte _currentSleepSteps = 1;
        #endregion

        /// <summary>
        /// Оповещает о новых записях на стене сообщества
        /// </summary>
        public event PostsListDelegate NewPosts;
        /// <summary>
        /// Оповещает об изменении закрепленного поста, значение -1 указывает на отсутствие закрепленного поста
        /// </summary>
        public event FixedPostChangedDelegate FixedPostChanged;

        private WallGetObject getWallPosts(uint offset, uint count)
        {
            var res = API.Wall.Get(new WallGetParams() { OwnerId = OwnerId, Offset = offset, Count = count });
            if (offset == 0 && res.WallPosts.Count > 0)
            {
                long fpid = res.WallPosts[0].Id.Value;
                if (fpid != FixedPostId)
                {
                    FixedPostId = res.WallPosts[0].IsPinned.Value ? res.WallPosts[0].Id.Value : -1;
                    if (FixedPostChanged != null)
                        FixedPostChanged(OwnerId, FixedPostId);
                }
                LastPostId = fpid != FixedPostId ? fpid : res.WallPosts.Count > 1 ? res.WallPosts[1].Id.Value : LastPostId;
            }
            return res;
        }
        private WallGetObject getWall(uint offset, uint count, long lastId, DateTime? lastDateToLoad = null, long lastPostToLoad = long.MaxValue)
        {
            WallGetObject tmp = null;

            byte brokeToken = 0;
            string ErrorLog = "";
            while (tmp == null && brokeToken < 5)
            {
                brokeToken++;
                try
                {
                    tmp = getWallPosts(offset, count);
                }
                catch (TooManyRequestsException)
                {
                    Task.Delay(150);
                    continue;
                }
                catch (Exception Ex)
                {
                    ErrorLog += string.Format("{0} - {1}{2}", brokeToken, Ex.Message, Environment.NewLine);
                }
            }

            if (tmp == null)
                throw new NotImplementedException("Ошибка при загрузке записей со стены\r\n" + ErrorLog);

            return tmp;
        }

        public List<Post> LoadWallPosts(DateTime? lastDateToLoad = null, long lastPostToLoad = long.MaxValue, bool? informImmediately = true)
        {
            List<Post> posts = new List<Post>();
            WallGetObject tmp;

            uint offset = 0;
            uint count = 100;
            long lastId = long.MaxValue;    // Последний из загруженных (верхняя граница след. итерации)

            do
            {
                try
                {
                    tmp = getWall(offset, count, lastId, lastDateToLoad, lastPostToLoad);
                }
                catch (Exception Ex)
                { throw new NotImplementedException(Ex.Message); }

                if (tmp.WallPosts.Count > 0)
                {
                    var tposts = FilterPosts(tmp, lastId, lastDateToLoad, lastPostToLoad);

                    if (informImmediately == true && NewPosts != null)
                        NewPosts(tposts.ToList());

                    posts.AddRange(tposts);
                    lastId = tmp.WallPosts[tmp.WallPosts.Count - 1].Id.Value;
                    offset += count;
                }
            }
            while (tmp.WallPosts.Count == count && lastId > lastPostToLoad && tmp.WallPosts.LastOrDefault()?.Date > lastDateToLoad);
            return posts;
        }
        /// <summary>
        /// Загружает список постов со стены сообщества с первого и до указанного и\или указанной даты
        /// </summary>
        /// <param name="lastDateToLoad">Крайник строк публикации постов (включительно)</param>
        /// <param name="lastPostToLoad">Последний пост до которого необходимо загружать</param>
        /// <param name="informImmediately">Оповещать о полученых постах через <see cref="NewPosts"/> как только они загрузяться (null и false выключат оповещение)</param>
        /// <returns></returns>
        public Task<List<Post>> LoadWallPostsAsync(DateTime? lastDateToLoad = null, long lastPostToLoad = long.MaxValue, bool? informImmediately = true)
        {
            return Task.Run(() => { return LoadWallPosts(lastDateToLoad, lastPostToLoad, informImmediately); });
        }

        private async void _watchAsync(object o)
        {
            List<Post> p = await LoadWallPostsAsync(null, LastPostId + 1, InformImmediately);

            if (p.Count() > 0)
            {
                _currentSleepSteps = 1;
                if (InformImmediately == false && NewPosts != null)
                    NewPosts(p);
            }
            else if (_currentSleepSteps < MaxSleepSteps)
                _currentSleepSteps++;

            _watchTimer.Change(_currentSleepSteps * SteepSleepTime, Timeout.Infinite);
        }

        public WallWatcher(VkApi api, long gid)
        {
            API = api;
            OwnerId = gid;

            Active = false;
            LastPostId = -1;
            FixedPostId = -1;
        }
        public void Start(bool informImmediately = true)
        {
            if (Active)
                throw new NotImplementedException("Group watcher already started!");

            Active = true;
            InformImmediately = informImmediately;

            _watchTimer = new Timer(new TimerCallback(_watchAsync), null, 0, Timeout.Infinite);
        }
        public void Stop()
        {
            if (_watchTimer != null)
                _watchTimer.Dispose();

            Active = false;
            _watchTimer = null;
            InformImmediately = null;
        }

        /// <summary>
        /// Выделить из полученных записей те, что удовлетворяют условиям
        /// </summary>
        /// <param name="wall">Стена с записями</param>
        /// <param name="thresholdId">Верхняя граница, до которой записи уже были загружены</param>
        /// <param name="lastDateToLoad">Дата и время, до которого нужно выбрать посты</param>
        /// <param name="lastPostToLoad">Последний пост, который нужно выбрать</param>
        /// <returns></returns>
        public static List<Post> FilterPosts(WallGetObject wall, long thresholdId = long.MaxValue, DateTime? lastDateToLoad = null, long lastPostToLoad = long.MaxValue)
        {
            var posts = wall.WallPosts;
            if (posts.Count > 0)
            {
                int startIndex = 0;
                int endIndex = posts.Count - 1;

                if (posts[0].IsPinned.Value)
                    startIndex++;

                while (startIndex <= endIndex && posts[startIndex].Id >= thresholdId)
                    startIndex++;

                if (lastDateToLoad.HasValue)
                    while (endIndex >= startIndex && posts[endIndex].Date < lastDateToLoad.Value)
                        endIndex--;
                if (lastPostToLoad > 0)
                    while (endIndex >= startIndex && posts[endIndex].Id < lastPostToLoad)
                        endIndex--;

                if (startIndex <= endIndex)
                    return posts.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();
            }
            return new List<Post>();
        }
        public static Task<List<Post>> FilterPostsAsync(WallGetObject wall, long thresholdId = long.MaxValue, DateTime? lastDateToLoad = null, long lastPostToLoad = long.MaxValue)
        {
            return Task.Run(() => { return FilterPosts(wall, thresholdId, lastDateToLoad, lastPostToLoad); });
        }
    }
}
