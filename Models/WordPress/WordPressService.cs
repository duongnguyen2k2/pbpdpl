using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace API.Models.WordPress
{
    public class WordPressService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        public WordPressService(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }
        public async Task<int> SyncAsync()
        {
            var client = _httpClientFactory.CreateClient("WPApi");

            int page = 1;
            int perPage = 100;

            var allPosts = new List<WordPress>();

            // 1) Lấy tất cả bài viết từ WP (paging)
            while (true)
            {
                var url = $"posts?_embed&per_page={perPage}&page={page}";
                var res = await client.GetAsync(url);

                // WP trả 400 khi page vượt quá tổng số trang
                if (!res.IsSuccessStatusCode)
                    break;
                var json = await res.Content.ReadAsStringAsync();


                var posts = JsonSerializer.Deserialize<List<WordPress>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<WordPress>();

                if (posts.Count == 0)
                    break;

                allPosts.AddRange(posts);
                page++;
            }

            // 2) Lưu DB
            var connStr = _config.GetConnectionString("DefaultConnection");
            int count = 0;

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            foreach (var p in allPosts)
            {
                // ====== Chuẩn hoá dữ liệu ======
                var title = HtmlDecode(p.Title?.Rendered)?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                var alias = !string.IsNullOrWhiteSpace(p.Slug)
                    ? p.Slug.Trim()
                    : GenerateSlug(title);

                var intro = HtmlDecode(p.Excerpt?.Rendered);
                var full = HtmlDecode(p.Content?.Rendered);

                DateTime publishUp = DateTime.TryParse(p.Date, out var dt) ? dt : DateTime.Now;
                DateTime modified = DateTime.TryParse(p.Modified, out var mt) ? mt : DateTime.Now;

                int catId = (p.Categories != null && p.Categories.Count > 0) ? p.Categories[0] : 0;
                // ShowPublishUp INT = yyyyMMdd (phục vụ filter trong SP)
                int showPublishUp = publishUp.Year * 10000 + publishUp.Month * 100 + publishUp.Day;

                // Status theo WP
                bool status = string.Equals(p.Status, "publish", StringComparison.OrdinalIgnoreCase);

                // ====== Bắt buộc để lên GetListPagination ======
                int idCoQuan = 7836;           // vì SP inner join DM_CoQuan
                int articlesStatusId = 1;   // vì SP yêu cầu ArticlesStatusId > 0
                int deleted = 0;            // deleted = 0
                int CategoryTypeId = 0;
                int Ordering = 0;
                int AuthorId = 0;
                int Featured = 0;
                int Hit = 0;
                int QuantityImage = 0;
                int Money = 0;
                // CatId: WP category id không match hệ thống => để an toàn set 1

                // featured image => chỉ lấy uploads/...
                var fullImageUrl = p.Embedded?.FeaturedMedia?.FirstOrDefault()?.SourceUrl;
                var imagePath = ExtractUploadsPath(fullImageUrl);

                // Str_Link nên lưu JSON để hệ thống không lỗi parse
                var strLinkJson = JsonSerializer.Serialize(new[]
                {
            new { Title = title, Url = p.Link, Status = true }
        });

                // ====== Check tồn tại theo IdOld ======
                const string checkSql = "SELECT TOP 1 Id FROM Articles WHERE IdOld = @IdOld";
                int? existingId = null;

                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.Add("@IdOld", SqlDbType.Int).Value = p.Id;
                    var obj = await checkCmd.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value)
                        existingId = Convert.ToInt32(obj);
                }

                if (existingId == null)
                {
                    var insertSql = @"
                                    INSERT INTO Articles
                                    (
                                        Title, Alias, CatId, IntroText, FullText,
                                        Images,
                                        Str_Link, PublishUp, ShowPublishUp, Status,
                                        CreatedDate, ModifiedDate,
                                        IdOld, Deleted, ArticlesStatusId,
                                        IdCoQuan, CategoryTypeId, Ordering, AuthorId, Featured, Hit, QuantityImage, Money
                                    )
                                    VALUES
                                    (
                                        @Title, @Alias, @CatId, @IntroText, @FullText,
                                        @Images,
                                        @StrLink, @PublishUp, @ShowPublishUp, @Status,
                                        @CreatedDate, @ModifiedDate,
                                        @IdOld, @Deleted, @ArticlesStatusId,
                                        @IdCoQuan, @CategoryTypeId, @Ordering, @AuthorId, @Featured, @Hit, @QuantityImage, @Money
                                    )";

                    using var cmd = new SqlCommand(insertSql, conn);

                    cmd.Parameters.Add("@Title", SqlDbType.NVarChar, 500).Value = (object?)title ?? DBNull.Value;
                    cmd.Parameters.Add("@Alias", SqlDbType.NVarChar, 500).Value = (object?)alias ?? DBNull.Value;
                    cmd.Parameters.Add("@CatId", SqlDbType.Int).Value = catId;
                    cmd.Parameters.Add("@IntroText", SqlDbType.NVarChar).Value = (object?)intro ?? DBNull.Value;
                    cmd.Parameters.Add("@FullText", SqlDbType.NVarChar).Value = (object?)full ?? DBNull.Value;

                    cmd.Parameters.Add("@Images", SqlDbType.NVarChar, 500).Value = (object?)imagePath ?? DBNull.Value;
                    cmd.Parameters.Add("@StrLink", SqlDbType.NVarChar).Value = (object?)strLinkJson ?? DBNull.Value;

                    cmd.Parameters.Add("@PublishUp", SqlDbType.DateTime).Value = publishUp;
                    cmd.Parameters.Add("@ShowPublishUp", SqlDbType.Int).Value = showPublishUp;
                    cmd.Parameters.Add("@Status", SqlDbType.Bit).Value = status;

                    cmd.Parameters.Add("@CreatedDate", SqlDbType.DateTime).Value = publishUp;
                    cmd.Parameters.Add("@ModifiedDate", SqlDbType.DateTime).Value = modified;

                    cmd.Parameters.Add("@IdOld", SqlDbType.Int).Value = p.Id;

                    cmd.Parameters.Add("@Deleted", SqlDbType.Bit).Value = deleted;
                    cmd.Parameters.Add("@ArticlesStatusId", SqlDbType.Int).Value = articlesStatusId;
                    cmd.Parameters.Add("@IdCoQuan", SqlDbType.Int).Value = idCoQuan;
                    cmd.Parameters.Add("@CategoryTypeId", SqlDbType.Int).Value = CategoryTypeId;
                    cmd.Parameters.Add("@Ordering", SqlDbType.Int).Value = Ordering;
                    cmd.Parameters.Add("@AuthorId", SqlDbType.Int).Value = AuthorId;
                    cmd.Parameters.Add("@Featured", SqlDbType.Int).Value = Featured;
                    cmd.Parameters.Add("@Hit", SqlDbType.Int).Value = Hit;
                    cmd.Parameters.Add("@QuantityImage", SqlDbType.Int).Value = QuantityImage;
                    cmd.Parameters.Add("@Money", SqlDbType.Int).Value = Money;

                    var rows = await cmd.ExecuteNonQueryAsync();
                    if (rows > 0) count++;
                }
                else
                {
                    var updateSql = @"
                                    UPDATE Articles
                                    SET
                                        Title = @Title,
                                        Alias = @Alias,
                                        CatId = @CatId,
                                        IntroText = @IntroText,
                                        FullText = @FullText,
                                        Images = @Images,
                                        Str_Link = @StrLink,
                                        PublishUp = @PublishUp,
                                        ShowPublishUp = @ShowPublishUp,
                                        Status = @Status,
                                        ModifiedDate = @ModifiedDate,

                                        Deleted = @Deleted,
                                        ArticlesStatusId = @ArticlesStatusId,
                                        IdCoQuan = @IdCoQuan
                                        CategoryTypeId = @CategoryTypeId
                                        Ordering = @Ordering
                                        AuthorId = @AuthorId
                                        Featured = @Featured
                                        Hit = @Hit
                                        QuantityImage = @QuantityImage
                                        Money = @Money
                                    WHERE Id = @Id
                                    ";

                    using var cmd = new SqlCommand(updateSql, conn);

                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = existingId.Value;

                    cmd.Parameters.Add("@Title", SqlDbType.NVarChar, 500).Value = (object?)title ?? DBNull.Value;
                    cmd.Parameters.Add("@Alias", SqlDbType.NVarChar, 500).Value = (object?)alias ?? DBNull.Value;
                    cmd.Parameters.Add("@CatId", SqlDbType.Int).Value = catId;
                    cmd.Parameters.Add("@IntroText", SqlDbType.NVarChar).Value = (object?)intro ?? DBNull.Value;
                    cmd.Parameters.Add("@FullText", SqlDbType.NVarChar).Value = (object?)full ?? DBNull.Value;

                    cmd.Parameters.Add("@Images", SqlDbType.NVarChar, 500).Value = (object?)imagePath ?? DBNull.Value;
                    cmd.Parameters.Add("@StrLink", SqlDbType.NVarChar).Value = (object?)strLinkJson ?? DBNull.Value;

                    cmd.Parameters.Add("@PublishUp", SqlDbType.DateTime).Value = publishUp;
                    cmd.Parameters.Add("@ShowPublishUp", SqlDbType.Int).Value = showPublishUp;
                    cmd.Parameters.Add("@Status", SqlDbType.Bit).Value = status;

                    cmd.Parameters.Add("@ModifiedDate", SqlDbType.DateTime).Value = modified;

                    cmd.Parameters.Add("@Deleted", SqlDbType.Bit).Value = deleted;
                    cmd.Parameters.Add("@ArticlesStatusId", SqlDbType.Int).Value = articlesStatusId;
                    cmd.Parameters.Add("@IdCoQuan", SqlDbType.Int).Value = idCoQuan;
                    cmd.Parameters.Add("@CategoryTypeId", SqlDbType.Int).Value = CategoryTypeId;
                    cmd.Parameters.Add("@Ordering", SqlDbType.Int).Value = Ordering;
                    cmd.Parameters.Add("@AuthorId", SqlDbType.Int).Value = AuthorId;
                    cmd.Parameters.Add("@Featured", SqlDbType.Int).Value = Featured;
                    cmd.Parameters.Add("@Hit", SqlDbType.Int).Value = Hit;
                    cmd.Parameters.Add("@QuantityImage", SqlDbType.Int).Value = QuantityImage;
                    cmd.Parameters.Add("@Money", SqlDbType.Int).Value = Money;
                    var rows = await cmd.ExecuteNonQueryAsync();
                    if (rows > 0) count++;
                }
            }

            return count;
        }

        public async Task<int> SyncCategoriesAsync()
        {
            // 1) Lấy hết categories từ WP
            var client = _httpClientFactory.CreateClient("WPApi");

            int page = 1;
            int perPage = 100;
            var allCats = new List<WPCategory>();

            while (true)
            {
                var url = $"categories?per_page={perPage}&page={page}&_fields=id,name,slug,description,parent,count";
                var res = await client.GetAsync(url);

                // WP trả 400 khi page vượt tổng trang
                if (!res.IsSuccessStatusCode)
                    break;

                var json = await res.Content.ReadAsStringAsync();

                var cats = JsonSerializer.Deserialize<List<WPCategory>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<WPCategory>();

                if (cats.Count == 0)
                    break;

                allCats.AddRange(cats);
                page++;
            }

            // 2) Lưu DB
            var connStr = _config.GetConnectionString("DefaultConnection");
            int count = 0;

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            foreach (var c in allCats)
            {
                // Check exist theo IdOld
                var checkSql = "SELECT TOP 1 Id FROM CategoriesArticles WHERE IdOld = @IdOld";
                int? existingId = null;

                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.Add("@IdOld", SqlDbType.Int).Value = c.Id;
                    var obj = await checkCmd.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value)
                        existingId = Convert.ToInt32(obj);
                }

                // ParentId: tạm lưu WP parent id trước
                int parentWpId = c.Parent;

                if (existingId == null)
                {
                    var insertSql = @"
INSERT INTO CategoriesArticles
(
    Title, Alias, Description,
    ParentId,
    Status, Deleted,
    CreatedDate, ModifiedDate,
    Hits,
    IdOld
)
VALUES
(
    @Title, @Alias, @Description,
    @ParentId,
    @Status, @Deleted,
    @CreatedDate, @ModifiedDate,
    @Hits,
    @IdOld
)";
                    using var cmd = new SqlCommand(insertSql, conn);

                    cmd.Parameters.Add("@Title", SqlDbType.NVarChar, 250).Value = (object?)c.Name ?? DBNull.Value;
                    cmd.Parameters.Add("@Alias", SqlDbType.NVarChar, 250).Value = (object?)c.Slug ?? DBNull.Value;
                    cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 250).Value = (object?)c.Description ?? DBNull.Value;

                    cmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = parentWpId;

                    cmd.Parameters.Add("@Status", SqlDbType.Bit).Value = true;
                    cmd.Parameters.Add("@Deleted", SqlDbType.Bit).Value = false;

                    cmd.Parameters.Add("@CreatedDate", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@ModifiedDate", SqlDbType.DateTime).Value = DateTime.Now;

                    cmd.Parameters.Add("@Hits", SqlDbType.Int).Value = c.Count;
                    cmd.Parameters.Add("@IdOld", SqlDbType.Int).Value = c.Id;

                    var rows = await cmd.ExecuteNonQueryAsync();
                    if (rows > 0) count++;
                }
                else
                {
                    var updateSql = @"
UPDATE CategoriesArticles
SET
    Title = @Title,
    Alias = @Alias,
    Description = @Description,
    ParentId = @ParentId,
    Hits = @Hits,
    ModifiedDate = @ModifiedDate
WHERE Id = @Id
";
                    using var cmd = new SqlCommand(updateSql, conn);

                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = existingId.Value;

                    cmd.Parameters.Add("@Title", SqlDbType.NVarChar, 250).Value = (object?)c.Name ?? DBNull.Value;
                    cmd.Parameters.Add("@Alias", SqlDbType.NVarChar, 250).Value = (object?)c.Slug ?? DBNull.Value;
                    cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 250).Value = (object?)c.Description ?? DBNull.Value;

                    cmd.Parameters.Add("@ParentId", SqlDbType.Int).Value = parentWpId;

                    cmd.Parameters.Add("@Hits", SqlDbType.Int).Value = c.Count;
                    cmd.Parameters.Add("@ModifiedDate", SqlDbType.DateTime).Value = DateTime.Now;

                    var rows = await cmd.ExecuteNonQueryAsync();
                    if (rows > 0) count++;
                }
            }

            // 3) Fix ParentId: đổi WP parent id -> Id nội bộ
            await FixParentIdAsync(conn);

            return count;
        }

        public async Task<int> DownloadAllMediaAsync(string localRoot)
        {
            var client = _httpClientFactory.CreateClient("WPApi");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            int page = 1;
            int perPage = 100;
            int count = 0;

            while (true)
            {
                var res = await client.GetAsync($"media?per_page={perPage}&page={page}");
                if (!res.IsSuccessStatusCode)
                    break;

                var json = await res.Content.ReadAsStringAsync();
                var medias = JsonSerializer.Deserialize<List<WPMedia>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new();

                if (medias.Count == 0)
                    break;

                foreach (var media in medias)
                {
                    if (string.IsNullOrWhiteSpace(media.Source_Url))
                        continue;

                    var saved = await SaveWpImageToLocalAsync(
                        media.Source_Url,
                        localRoot,
                        client
                    );

                    if (saved != null)
                        count++;
                }

                page++;
            }

            return count;
        }

        private static async Task<string?> SaveWpImageToLocalAsync(
        string imageUrl,
        string localRoot,
        HttpClient client)
        {
            var uri = new Uri(imageUrl);
            var localPath = uri.LocalPath;

            if (!localPath.Contains("/wp-content/uploads/"))
                return null;

            var relativePath = localPath.Replace("/wp-content/", "");
            var fullPath = Path.Combine(localRoot, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            if (!File.Exists(fullPath))
            {
                var bytes = await client.GetByteArrayAsync(imageUrl);
                await File.WriteAllBytesAsync(fullPath, bytes);
            }

            return fullPath;
        }

        private async Task FixParentIdAsync(SqlConnection conn)
        {
            // Lấy các category có ParentId > 0 (đang là WP ParentId)
            var selectSql = @"
                            SELECT Id, ParentId
                            FROM CategoriesArticles
                            WHERE ParentId IS NOT NULL AND ParentId > 0
                            ";

            var list = new List<(int Id, int ParentWpId)>();

            using (var cmd = new SqlCommand(selectSql, conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    int parentWpId = reader.GetInt32(1);
                    list.Add((id, parentWpId));
                }
            }

            foreach (var item in list)
            {
                // tìm Id nội bộ của category cha theo IdOld
                var findParentSql = "SELECT TOP 1 Id FROM CategoriesArticles WHERE IdOld = @ParentWpId";
                int? parentInternalId = null;

                using (var cmd = new SqlCommand(findParentSql, conn))
                {
                    cmd.Parameters.Add("@ParentWpId", SqlDbType.Int).Value = item.ParentWpId;
                    var obj = await cmd.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value)
                        parentInternalId = Convert.ToInt32(obj);
                }

                // nếu tìm thấy thì update ParentId = Id nội bộ
                if (parentInternalId.HasValue)
                {
                    var updateSql = @"
                                    UPDATE CategoriesArticles
                                    SET ParentId = @ParentInternalId
                                    WHERE Id = @Id
                                    ";
                    using var cmd = new SqlCommand(updateSql, conn);
                    cmd.Parameters.Add("@ParentInternalId", SqlDbType.Int).Value = parentInternalId.Value;
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = item.Id;
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private static string? HtmlDecode(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return html;
            return System.Net.WebUtility.HtmlDecode(html);
        }

        private static string GenerateSlug(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Guid.NewGuid().ToString("N");

            // đơn giản hoá: bỏ tag HTML + khoảng trắng
            var s = Regex.Replace(input, "<.*?>", "");
            s = s.Trim().ToLower();

            // thay space thành "-"
            s = Regex.Replace(s, @"\s+", "-");

            // bỏ ký tự đặc biệt
            s = Regex.Replace(s, @"[^a-z0-9\-]", "");

            return s;
        }
        private static string? ExtractUploadsPath(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            var idx = url.IndexOf("uploads/", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var path = url.Substring(idx); // uploads/2025/01/abc.jpg

            // thêm uploads/ttyt-mdrak/ vào sau uploads/
            // => uploads/ttyt-mdrak/2025/01/abc.jpg
            if (path.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                path = "/uploads/ttyt-mdrak/" + path.Substring("uploads/".Length);
            }

            return path;
        }





    }

}

