using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CourseProjectMusic.Models
{
    public class MusicInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string FileName { get; set; }
        public string Img { get; set; }
        public int GenreId { get; set; }
        public int UserId { get; set; }
        public string UserLogin { get; set; }
        public int[] IdOfUsersLike { get; set; }
    }
}
