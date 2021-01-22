using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CourseProjectMusic.Models;
using CourseProjectMusic.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace CourseProjectMusic.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MusicController : ControllerBase
    {
        private readonly DataBaseContext db;
        private readonly IConfiguration config;
        private readonly IOptions<StorageConfiguration> storageConfig;
        private int UserId => int.Parse(User.Claims.Single(cl => cl.Type == ClaimTypes.NameIdentifier).Value);
        public MusicController(DataBaseContext db, IConfiguration config, IOptions<StorageConfiguration> sc)
        {
            this.db = db;
            this.config = config;
            storageConfig = sc;
        }

        [HttpGet("FilterMusic")]
        public async Task<List<MusicInfo>> GetFilteredList(string musicName, int genreId)
        {
            FilteredMusicList fl = new FilteredMusicList { MusicName = musicName==null?"":musicName, GenreId = genreId };
            string musicNameFilter = fl.MusicName.Length > 0 ? fl.MusicName : String.Empty;
            string musicGenreFilter = fl.GenreId > 0 ? fl.GenreId.ToString() : "%";
            List<MusicInfo> res = new List<MusicInfo>();
            try
            {
               res=await db.Musics.Where(m => EF.Functions.Like(m.MusicName, $"%{musicNameFilter}%") 
               & EF.Functions.Like(m.MusicGenreId.ToString(), $"{musicGenreFilter}")).Join(db.Users, m => m.UserId, u => u.UserId, (m, u) => new MusicInfo
               {
                   Id = m.MusicId,
                   Name = m.MusicName,
                   Url = config.GetSection("ContainerURL").Value + m.MusicFileName,
                   FileName = m.MusicFileName,
                   Img = config.GetSection("ContainerURL").Value + m.MusicImageName,
                   GenreId = m.MusicGenreId,
                   UserId = u.UserId,
                   UserLogin = u.Login
               }).ToListAsync();
                return res;
            }
            catch
            {
                Response.StatusCode = 500;
                return new List<MusicInfo>();
            }
        }

        [HttpGet("DownloadFile/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            MemoryStream ms = new MemoryStream();
            if (CloudStorageAccount.TryParse(storageConfig.Value.ConnectionString, out CloudStorageAccount storageAccount))
            {
                CloudBlobClient BlobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = BlobClient.GetContainerReference(storageConfig.Value.ContainerName);

                if (await container.ExistsAsync())
                {
                    CloudBlob file = container.GetBlobReference(fileName);

                    if (await file.ExistsAsync())
                    {
                        await file.DownloadToStreamAsync(ms);
                        Stream blobStream = file.OpenReadAsync().Result;

                        Response.Headers.Add("Accept-Ranges", "bytes");

                        return new FileStreamResult(blobStream, "audio/mp3");
                    }
                    else
                    {
                        return Content("File does not exist");
                    }
                }
                else
                {
                    return Content("Container does not exist");
                }
            }
            else
            {
                return Content("Error opening storage");
            }
        }

        [HttpPost("LikeMusic/{idMusic}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> LikeMusic(int idMusic)
        {
            try
            {
                db.UserMusicLikes.Add(new UserMusicLike { MusicId = idMusic, UserId = UserId });
                await db.SaveChangesAsync();
                return Ok();
            }
            catch { return StatusCode(500); }
        }

        [HttpDelete("DeleteLikeMusic/{idMusic}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> DeleteLikeMusic(int idMusic)
        {
            try
            {
                db.UserMusicLikes.Remove(new UserMusicLike { MusicId = idMusic, UserId = UserId });
                await db.SaveChangesAsync();
                return Ok();
            }
            catch { return StatusCode(500); }
        }

        [HttpGet("{id}")]
        public async Task<MusicInfo> GetMusicInfoById(int id)
        {
            Music music = await db.Musics.FindAsync(id);
            return new MusicInfo
            {
                Id = music.MusicId,
                Name = music.MusicName,
                Url = config.GetSection("ContainerURL").Value + music.MusicFileName,
                FileName = music.MusicFileName,
                Img = config.GetSection("ContainerURL").Value + music.MusicImageName,
                GenreId = music.MusicGenreId,
                UserId=music.UserId,
                UserLogin = await db.Users.Where(u => u.UserId == music.UserId).Select(u => u.Login).FirstOrDefaultAsync(),
                IdOfUsersLike = await db.UserMusicLikes.Where(um => um.MusicId == id).Select(um => um.UserId).ToArrayAsync()
            };
        }


        [HttpGet("list/{userid}")]
        public async Task<List<MusicInfo>> GetMusicListByUserId(int userid)
        {
            List<MusicInfo> res = new List<MusicInfo>();
            await db.Musics.Where(m => m.UserId == userid).ForEachAsync(m => res.Add(new MusicInfo {Id=m.MusicId, Name = m.MusicName, Url = config.GetSection("ContainerURL").Value + m.MusicFileName, 
                FileName=m.MusicFileName, Img=config.GetSection("ContainerURL").Value+m.MusicImageName, GenreId=m.MusicGenreId }));
            return res;
        }

        [HttpGet("listMusicGenres")]
        public async Task<List<MusicGenreInfo>> GetMusicGenresList()
        {
            List<MusicGenreInfo> res = new List<MusicGenreInfo>();
            await db.MusicGenres.ForEachAsync(g => res.Add(new MusicGenreInfo { Id = g.MusicGenreId, Name = g.GenreName, Description = g.GenreDescription }));
            return res;
        }

        [HttpPost("AddMusic")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> AddMusic([FromForm]AddMusicModel model)
        {
            User user = await db.Users.FindAsync(UserId);
            string dateTimeNow = $"{DateTime.Now.Day}.{DateTime.Now.Month}.{DateTime.Now.Year} {DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            if (await db.Musics.Where(m => m.UserId == user.UserId && m.MusicName == model.MusicName).FirstOrDefaultAsync() != null)
                return Ok(new { msg = $"У вас уже есть запись с названием {model.MusicName}" });
            try
            {
                if (CloudStorageAccount.TryParse(storageConfig.Value.ConnectionString, out CloudStorageAccount storageAccount))
                {
                    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                    CloudBlobContainer container = blobClient.GetContainerReference(storageConfig.Value.ContainerName);
                    CloudBlockBlob musicBlockBlob = container.GetBlockBlobReference($"{user.Login}_{dateTimeNow}_" + model.MusicFile.FileName);
                    if (await musicBlockBlob.ExistsAsync())
                        return Ok(new { msg = $"В вашем хранилище уже есть файл {model.MusicFile.FileName}" });
                    if (model.MusicImageFile!=null)
                    {
                        CloudBlockBlob imageBlockBlob = container.GetBlockBlobReference($"{user.Login}_music_{dateTimeNow}_" + model.MusicImageFile.FileName);
                        if (await imageBlockBlob.ExistsAsync())
                            return Ok(new { msg = $"В вашем хранилище уже есть файл {model.MusicImageFile.FileName}" });
                        await imageBlockBlob.UploadFromStreamAsync(model.MusicImageFile.OpenReadStream());
                    }
                    await musicBlockBlob.UploadFromStreamAsync(model.MusicFile.OpenReadStream());
                    db.Musics.Add(new Music
                    {
                        MusicName = model.MusicName,
                        MusicFileName = $"{user.Login}_{dateTimeNow}_"+ model.MusicFile.FileName,
                        MusicImageName =model.MusicImageFile==null?"default.png":$"{user.Login}_music_{dateTimeNow}_" + model.MusicImageFile.FileName,
                        UserId = user.UserId,
                        DateOfPublication = DateTime.Now.Date,
                        MusicGenreId = model.MusicGenreId
                    });
                    await db.SaveChangesAsync();
                    return Ok(new {msg=""});
                }
                return StatusCode(500);
            }
            catch
            {
                return StatusCode(500);
            }
        }

        [HttpPut("EditMusic")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> EditMusic([FromForm] EditMusicModel model)
        {
            string musicFileName, imageFileName;
            User user = await db.Users.FindAsync(UserId);
            Music music = await db.Musics.FindAsync(model.Id);
            if (music.MusicName!=model.MusicName && await db.Musics.Where(m => m.UserId == user.UserId && m.MusicName == model.MusicName).FirstOrDefaultAsync() != null)
                return Ok(new { msg = $"У вас уже есть запись с названием {model.MusicName}" });
            string dateTimeNow = $"{DateTime.Now.Day}.{DateTime.Now.Month}.{DateTime.Now.Year} {DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            try
            {
                if (CloudStorageAccount.TryParse(storageConfig.Value.ConnectionString, out CloudStorageAccount storageAccount))
                {
                    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                    CloudBlobContainer container = blobClient.GetContainerReference(storageConfig.Value.ContainerName);
                    if (model.MusicFile != null)
                    {
                        musicFileName = $"{user.Login}_{dateTimeNow}_" + model.MusicFile.FileName;
                        CloudBlockBlob musicBlockBlob = container.GetBlockBlobReference(music.MusicFileName);
                        await musicBlockBlob.DeleteIfExistsAsync();
                        musicBlockBlob = container.GetBlockBlobReference(musicFileName);
                        await musicBlockBlob.UploadFromStreamAsync(model.MusicFile.OpenReadStream());
                        music.MusicFileName = musicFileName;
                    }
                    if (model.MusicImageFile != null)
                    {
                        imageFileName = $"{user.Login}_music_{dateTimeNow}_" + model.MusicImageFile.FileName;
                        CloudBlockBlob imageBlockBlob = container.GetBlockBlobReference(music.MusicImageName);
                        await imageBlockBlob.DeleteIfExistsAsync();
                        imageBlockBlob = container.GetBlockBlobReference(imageFileName);
                        await imageBlockBlob.UploadFromStreamAsync(model.MusicImageFile.OpenReadStream());
                        music.MusicImageName = imageFileName;
                    }
                    music.MusicName = model.MusicName;
                    music.MusicGenreId = model.MusicGenreId;
                    await db.SaveChangesAsync();
                    return Ok(new {msg=""});
                }
            }
            catch { }
            return StatusCode(500);
        }

        [Authorize(Roles = "User")]
        [HttpDelete("Delete/{id}")]
        public async Task<List<MusicInfo>> DeleteMusic(int id)
        {
            User user = await db.Users.FindAsync(UserId);
            Music music = await db.Musics.FindAsync(id);
            List<MusicInfo> res = new List<MusicInfo>();
            if (music != null)
            {
                try
                {
                    if(CloudStorageAccount.TryParse(storageConfig.Value.ConnectionString, out CloudStorageAccount storageAccount))
                    {
                        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                        CloudBlobContainer container = blobClient.GetContainerReference(storageConfig.Value.ContainerName);
                        if(await container.ExistsAsync())
                        {
                            CloudBlob blob = container.GetBlobReference(music.MusicFileName);
                            if (await blob.ExistsAsync())
                                await blob.DeleteAsync();
                            if (music.MusicImageName != "default.png")
                            {
                                blob = container.GetBlobReference(music.MusicImageName);
                                if (await blob.ExistsAsync())
                                    await blob.DeleteAsync();
                            }  
                            db.Musics.Remove(music);
                            await db.SaveChangesAsync();
                        }
                    }
                }
                catch
                {
                }
            }
            return await GetMusicListByUserId(UserId);
        }
    }
}
