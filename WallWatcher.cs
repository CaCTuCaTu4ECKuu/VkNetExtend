using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkNetExtend
{
    using System.Threading;

    using VkNet;
    using VkNet.Model;
    using VkNet.Exception;

    public delegate void PostsListDelegate(long ownerId, IEnumerable<Post> posts);
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
            var res = APIController.GetWallPosts(API, OwnerId, count, offset);
            if (res.WallPosts.Count > 0 && offset == 0)
            {
                if (res.WallPosts[0].Id != FixedPostId && res.WallPosts[0].IsPinned)
                {
                    FixedPostId = APIController.GetFixedPost(API, OwnerId);
                    if (FixedPostChanged != null)
                        FixedPostChanged(OwnerId, FixedPostId);
                }
            }
            return res;
        }
        private WallGetObject getWall(uint offset, uint count, long lastId, DateTime? lastDateToLoad = null, long lastPostToLoad = 0)
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
                    ErrorLog += string.Format("{0} - {1}{3}", brokeToken, Ex.Message, Environment.NewLine);
                }
            }

            if (tmp == null)
                throw new NotImplementedException("Ошибка при загрузке записей со стены\r\n" + ErrorLog);

            return tmp;
        }

        /// <summary>
        /// Загружает список постов со стены сообщества с первого и до указанного в lastPostId и указанной даты
        /// </summary>
        /// <param name="lastDateToLoad">Крайник строк публикации постов (включительно)</param>
        /// <param name="lastPostId">Последний пост до которого необходимо загружать</param>
        /// <param name="informImmediately">Оповещать о полученых постах через <see cref="NewPosts"/> как только они загрузяться (null и false выключат оповещение)</param>
        /// <returns></returns>
        public Task<IEnumerable<Post>> LoadWallPostsAsync(DateTime? lastDateToLoad = null, long lastPostId = 0, bool? informImmediately = true)
        {
            return Task.Run(() =>
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
                        tmp = getWall(offset, count, lastId, lastDateToLoad, lastPostId);
                    }
                    catch (Exception Ex)
                    { throw new NotImplementedException(Ex.Message); }

                    if (tmp.WallPosts.Count > 0)
                    {
                        var tposts = FilterPosts(tmp, lastId, lastDateToLoad, lastPostId);

                        if (informImmediately == true && NewPosts != null)
                            NewPosts(OwnerId, tposts);

                        posts.AddRange(tposts);
                        lastId = tmp.WallPosts[tmp.WallPosts.Count - 1].Id;
                        offset += count;
                    }
                }
                while (tmp.WallPosts.Count == count && lastId > lastPostId);
                return posts as IEnumerable<Post>;
            });
        }

        private async void _watchAsync(object o)
        {
            IEnumerable<Post> p = await LoadWallPostsAsync(null, LastPostId + 1, InformImmediately);

            if (p.Count() > 0)
            {
                _currentSleepSteps = 1;
                if (InformImmediately == false && NewPosts != null)
                    NewPosts(OwnerId, p);
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
        public static IEnumerable<Post> FilterPosts(WallGetObject wall, long thresholdId = long.MaxValue, DateTime? lastDateToLoad = null, long lastPostToLoad = 0)
        {
            var posts = wall.WallPosts;
            if (posts.Count > 0)
            {
                int startIndex = 0;
                int endIndex = posts.Count - 1;

                if (posts[0].IsPinned)
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
                    return posts.Skip(startIndex).Take(endIndex - startIndex + 1);
            }
            return new Post[0];
        }
        public static Task<IEnumerable<Post>> FilterPostsAsync(WallGetObject wall, long thresholdId = long.MaxValue, DateTime? lastDateToLoad = null, long lastPostToLoad = 0)
        {
            return Task.Run(() => { return FilterPosts(wall, thresholdId, lastDateToLoad, lastPostToLoad); });
        }
    }
}
