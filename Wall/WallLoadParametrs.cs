using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkNetExtend.Wall
{
    public struct WallLoadParametrs
    {
        /// <summary>
        /// Сдвиг для загрузки записей
        /// </summary>
        public uint? Offset;
        /// <summary>
        /// Количество постов, которые нужно загрузить
        /// </summary>
        public uint? Count;

        /// <summary>
        /// Время последнего поста, который нужно загрузить.
        /// </summary>
        public DateTime? LastDateToLoad;
        /// <summary>
        /// Идентификатор последнего поста, который нужно загрузить
        /// </summary>
        public long? LastPostToLoad;

        public WallLoadParametrs(uint offset, uint count)
        {
            Offset = offset;
            Count = count;
            LastDateToLoad = null;
            LastPostToLoad = null;
        }
        public WallLoadParametrs(DateTime? lastDateToLoad, long? lastPostToLoad)
        {
            Offset = null;
            Count = null;
            LastDateToLoad = lastDateToLoad;
            LastPostToLoad = lastPostToLoad;
        }

        public static WallLoadParametrs All
        {
            get
            {
                return new WallLoadParametrs(null, null);
            }
        }
        public static WallLoadParametrs Today
        {
            get
            {
                return new WallLoadParametrs(DateTime.Today, long.MaxValue);
            }
        }
        public static WallLoadParametrs Last100
        {
            get
            {
                return new WallLoadParametrs(0, 100);
            }
        }
    }
}
