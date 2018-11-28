using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using VkNet;
using VkNet.Model;
using VkNet.Exception;
using VkNet.Model.RequestParams;

namespace VkNetExtend.Old
{
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
        private CancellationTokenSource cts;
        private Timer _watchTimer = null;

        public byte MaxSleepSteps = 3;
        public int SteepSleepTime = 5000;
        private byte _currentSleepSteps = 1;
        #endregion
        #region Контроль загрузки
        /// <summary>
        /// Пауза в миллисекундах в случае превышения количества запросов в секунду у этого API
        /// </summary>
        public int TooManyRequestsDelay { get; set; }

        private uint _maxPostsPerLoad = 100;
        private uint _minPostsPerLoad = 20;
        public uint MaxPostsPerLoad
        {
            get { return _maxPostsPerLoad; }
            set { _maxPostsPerLoad = value > 100 ? 100 : value; }
        }
        public uint MinPostsPerLoad
        {
            get { return _minPostsPerLoad; }
            set { _minPostsPerLoad = value > _maxPostsPerLoad ? _maxPostsPerLoad : value; }
        }
        #endregion

        /// <summary>
        /// Оповещает о новых записях на стене сообщества
        /// </summary>
        public event PostsListDelegate NewPosts;
        /// <summary>
        /// Оповещает об изменении закрепленного поста, значение -1 указывает на отсутствие закрепленного поста
        /// </summary>
        public event FixedPostChangedDelegate FixedPostChanged;
        /// <summary>
        /// Оповещение о записях, загруженных по запросу (с указанием параметра informImmediately = true)
        /// </summary>
        public event PostsListDelegate CustomLoad;

        /// <summary>
        /// Непосредственно загружает посты по указанному смещению и количеству, проверяет корректность загрузки и следит за изменением закрепленной записи
        /// </summary>
        private WallGetObject getWallPosts(uint offset, uint count)
        {
            var res = API.Wall.Get(new WallGetParams() { OwnerId = OwnerId, Offset = offset, Count = count });
            if (offset == 0 && res.WallPosts.Count > 0)
            {
                long fpid = res.WallPosts[0].Id.Value;
                if (fpid != FixedPostId)
                {
                    FixedPostId = res.WallPosts[0].IsPinned.Value ? res.WallPosts[0].Id.Value : -1;
                    FixedPostChanged?.Invoke(OwnerId, FixedPostId);
                }
                LastPostId = fpid != FixedPostId ? fpid : res.WallPosts.Count > 1 ? res.WallPosts[1].Id.Value : LastPostId;
            }
            return res;
        }
        /// <summary>
        /// Вызывает метод загрузки постов и следит чтобы загрузка прошла успешно.
        /// </summary>
        private WallGetObject getWall(CancellationToken cancellationToken, uint offset, uint count)
        {
            WallGetObject tmp = null;

            byte brokeToken = 0;
            string ErrorLog = "";
            while (tmp == null && brokeToken < 5)
            {
                cancellationToken.ThrowIfCancellationRequested();
                brokeToken++;
                try
                {
                    tmp = getWallPosts(offset, count);
                }
                catch (TooManyRequestsException)
                {
                    Task.Delay(TooManyRequestsDelay);
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

        private List<Post> LoadWallPosts(CancellationToken cancellationToken, WallLoadParametrs @params, bool customLoad, bool? informImmediately = true)
        {
            List<Post> posts = new List<Post>();
            WallGetObject tmp;

            uint offset = @params.Offset ?? 0;
            uint count = _maxPostsPerLoad;  //TODO: Дописать чтобы находило оптимальное количество загружаемых постов

            long lastId = long.MaxValue;    // Последний из загруженных (верхняя граница след. итерации)

            do
            {
                try
                {
                    tmp = getWall(cancellationToken, offset, count);
                }
                catch (Exception Ex)
                { throw new NotImplementedException(Ex.Message); }

                if (tmp.WallPosts.Count > 0)
                {
                    var tposts = FilterPosts(tmp, @params, lastId);

                    if (informImmediately == true)
                    {
                        if (customLoad)
                        {
                            CustomLoad?.Invoke(tposts.ToList());
                        }
                        else
                        {
                            NewPosts?.Invoke(tposts.ToList());
                        }
                    }

                    posts.AddRange(tposts);
                    lastId = tmp.WallPosts[tmp.WallPosts.Count - 1].Id.Value;
                    offset += count;
                }
            }
            while (tmp.WallPosts.Count == count && /* Не дошло до конца и загружает все запрашиваемые записи */
                (@params.LastPostToLoad == null || lastId > @params.LastPostToLoad.Value) && /* Не дошли до поста, который нужно загрузить (по идентификатору)*/
                (@params.Count == null || posts.Count < @params.Count.Value) && /* Не дошли до поста, который нужно загрузить (по количеству) */
                tmp.WallPosts.LastOrDefault()?.Date > @params.LastDateToLoad /* Не дошли до поста, который нужно загрузить (по дате) */
            );
            return posts;
        }
        /// <summary>
        /// Загрузить записи со стены
        /// </summary>
        /// <param name="cancellationToken">Токен отмены загрузки</param>
        /// <param name="params">Параметры загрузки записей</param>
        /// <param name="informImmediately">Оповещать о полученых постах через <see cref="CustomLoad"/> как только они загрузяться (null и false выключат оповещение)</param>
        public List<Post> LoadWallPosts(CancellationToken cancellationToken, WallLoadParametrs @params, bool? informImmediately = true)
        {
            return LoadWallPosts(cancellationToken, @params, true, informImmediately);
        }

        private Task<List<Post>> LoadWallPostsAsync(CancellationToken cancellationToken, WallLoadParametrs @params, bool customLoad, bool? informImmediately = true)
        {
            return Task.Run(() => { return LoadWallPosts(cancellationToken, @params, customLoad, informImmediately); }, cancellationToken);
        }
        /// <summary>
        /// Загружает список постов со стены сообщества с первого и до указанного и\или указанной даты
        /// </summary>
        /// <param name="cancellationToken">Токен отмены загрузки</param>
        /// <param name="params">Параметры загрузки записей</param>
        /// <param name="informImmediately">Оповещать о полученых постах через <see cref="CustomLoad"/> как только они загрузяться (null и false выключат оповещение)</param>
        public Task<List<Post>> LoadWallPostsAsync(CancellationToken cancellationToken, WallLoadParametrs @params, bool? informImmediately = true)
        {
            return LoadWallPostsAsync(cancellationToken, @params, true, informImmediately);
        }

        private async void _watchAsync(object o)
        {
            List<Post> p = await LoadWallPostsAsync(cts.Token, new WallLoadParametrs { LastDateToLoad = null, LastPostToLoad = LastPostId + 1 }, false, InformImmediately);

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

            TooManyRequestsDelay = 150;
        }
        /// <summary>
        /// Запускает слежение за стеной
        /// </summary>
        /// <param name="informImmediately">Показывать найденные посты сразу после получания набора или после загрузки всех записей (может занять много времени)</param>
        public void Start(bool informImmediately = true)
        {
            if (Active)
                throw new NotImplementedException("Group watcher already started!");

            Active = true;
            InformImmediately = informImmediately;

            cts = new CancellationTokenSource();
            _watchTimer = new Timer(new TimerCallback(_watchAsync), null, 0, Timeout.Infinite);
        }
        public void Stop()
        {
            if (_watchTimer != null)
            {
                _watchTimer.Dispose();

                cts.Cancel();
                Active = false;
                _watchTimer = null;
                InformImmediately = null;
            }
        }

        /// <summary>
        /// Выделить из полученных записей те, что удовлетворяют условиям
        /// </summary>
        /// <param name="wall">Стена с записями</param>
        /// <param name="thresholdId">Верхняя граница, до которой записи уже были загружены</param>
        /// <param name="lastDateToLoad">Дата и время, до которого нужно выбрать посты</param>
        /// <param name="lastPostToLoad">Последний пост, который нужно выбрать</param>
        /// <returns></returns>
        public static List<Post> FilterPosts(WallGetObject wall, WallLoadParametrs p, long thresholdId = long.MaxValue)
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

                if (p.LastDateToLoad.HasValue)
                    while (endIndex >= startIndex && posts[endIndex].Date < p.LastDateToLoad.Value)
                        endIndex--;
                if (p.LastPostToLoad > 0)
                    while (endIndex >= startIndex && posts[endIndex].Id < p.LastPostToLoad)
                        endIndex--;

                if (startIndex <= endIndex)
                    return posts.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();
            }
            return new List<Post>();
        }
        public static Task<List<Post>> FilterPostsAsync(WallGetObject wall, WallLoadParametrs p, long thresholdId = long.MaxValue)
        {
            return Task.Run(() => { return FilterPosts(wall, p, thresholdId); });
        }
    }
}
