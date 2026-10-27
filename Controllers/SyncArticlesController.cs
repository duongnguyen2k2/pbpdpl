using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using API.Models.WordPress;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncArticlesController : ControllerBase
    {
        private readonly WordPressService _sync;

        public SyncArticlesController(WordPressService sync)
        {
            _sync = sync;
        }

        [HttpPost("wp/articles")]
        public async Task<IActionResult> SyncFromWp()
        {
            var count = await _sync.SyncAsync();
            return Ok(new { message = "Sync WP -> Articles OK", count });
        }
        [HttpPost("wp/categories")]
        public async Task<IActionResult> SyncCatgoriesFromWp()
        {
            var count = await _sync.SyncCategoriesAsync();
            return Ok(new { message = "Sync WP Categories -> DB OK", count });
        }
        [HttpPost("wp/media")]
        public async Task<IActionResult> DownloadMedia()
        {
            string localRoot = @"D:\Images";

            var total = await _sync.DownloadAllMediaAsync(localRoot);

            return Ok(new
            {
                message = "Download completed",
                totalDownloaded = total,
                localPath = localRoot
            });
        }
    }
}
