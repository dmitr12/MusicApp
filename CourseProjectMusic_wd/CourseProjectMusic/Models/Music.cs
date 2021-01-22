using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CourseProjectMusic.Models
{
    public class Music
    {
        public int MusicId { get; set; }

        [Required]
        public string MusicName { get; set; }

        [Required]
        public string MusicFileName { get; set; }

        public string MusicImageName { get; set; }

        [Required]
        public int UserId { get; set; }
        [Required]
        public DateTime DateOfPublication { get; set; }

        [Required]
        public int MusicGenreId { get; set; }

        public User User { get; set; }

        public MusicGenre MusicGenre { get; set; }
    }
}
