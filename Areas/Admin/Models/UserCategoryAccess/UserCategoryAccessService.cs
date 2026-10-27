using API.Areas.Admin.Models.USUsers;
using API.Models;
using Microsoft.Extensions.Configuration;  // Thêm cho IConfiguration
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace API.Areas.Admin.Models.UserCategoryAccess
{
    /// <summary>
    /// Interface định nghĩa các phương thức cho UserCategoryAccessService.
    /// </summary>
    public interface IUserCategoryAccessService
    {
        /// <summary>
        /// Tạo mới một bản ghi UserCategoryAccess.
        /// Trả về Id của bản ghi mới được tạo, hoặc -1 nếu lỗi.
        /// </summary>
        int Create(int userId, string categoryList);

        /// <summary>
        /// Lấy thông tin UserCategoryAccess theo UserId (chỉ bản ghi chưa xóa).
        /// Trả về DataTable với cột: Id, UserId, CategoryList, CreatedDate, ModifiedDate.
        /// </summary>
        DataTable GetByUserId(int userId);

        /// <summary>
        /// Lấy thông tin UserCategoryAccess theo Id (chỉ bản ghi chưa xóa).
        /// Trả về DataTable với tất cả cột.
        /// </summary>
        DataTable GetById(int id);

        /// <summary>
        /// Lấy tất cả UserCategoryAccess chưa xóa.
        /// Trả về DataTable với cột: Id, UserId, CategoryList, CreatedDate, ModifiedDate.
        /// </summary>
        DataTable GetAll();

        /// <summary>
        /// Cập nhật CategoryList cho bản ghi theo Id (nếu chưa xóa).
        /// Trả về số hàng bị ảnh hưởng (1 nếu thành công, 0 nếu không tìm thấy).
        /// </summary>
        int Update(int id, string categoryList);

        /// <summary>
        /// Xóa mềm bản ghi theo Id (set Deleted = 1).
        /// Trả về số hàng bị ảnh hưởng (1 nếu thành công, 0 nếu không tìm thấy).
        /// </summary>
        int Delete(int id);

        /// <summary>
        /// Kiểm tra quyền đăng bài cho user trong chuyên mục cụ thể.
        /// Trả về true nếu role >=1.
        /// </summary>
        bool CanPostArticle(int catId, int userId, int coQuanId);

        /// <summary>
        /// Kiểm tra quyền chỉnh sửa bài viết cho user trong chuyên mục cụ thể.
        /// Trả về true nếu role >=2.
        /// </summary>
        bool CanEditArticle(int catId, int userId, int coQuanId);

        /// <summary>
        /// Kiểm tra quyền duyệt bài viết cho user trong chuyên mục cụ thể.
        /// Trả về true nếu role =3.
        /// </summary>
        bool CanApproveArticle(int catId, int userId, int coQuanId);

        /// <summary>
        /// Lấy danh sách chuyên mục mà user có quyền đăng bài (role >=1).
        /// </summary>
        List<int> GetPostableCategories(int userId, int coQuanId);

        /// <summary>
        /// Lấy danh sách chuyên mục mà user có quyền chỉnh sửa bài viết (role >=2).
        /// </summary>
        List<int> GetEditableCategories(int userId, int coQuanId);

        /// <summary>
        /// Lấy danh sách chuyên mục mà user có quyền duyệt bài viết (role =3).
        /// </summary>
        List<int> GetApprovableCategories(int userId, int coQuanId);
    }

    /// <summary>
    /// Implementation của IUserCategoryAccessService để xử lý CRUD cho UserCategoryAccess.
    /// </summary>
    public class UserCategoryAccessService : IUserCategoryAccessService
    {
        private readonly string _connectionString;

        // Inject IConfiguration thay vì string trực tiếp để lấy connection string từ config
        public UserCategoryAccessService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        /// <summary>
        /// Tạo mới một bản ghi UserCategoryAccess.
        /// Trả về Id của bản ghi mới được tạo, hoặc -1 nếu lỗi.
        /// </summary>
        public int Create(int userId, string categoryList)
        {
            var _user = USUsersService.GetItem(userId);
            int _idCoQuan = _user != null ? _user.IdCoQuan : 0;
            // Đảm bảo categoryList luôn là string hợp lệ, fallback "{}" nếu null/empty
            categoryList = string.IsNullOrWhiteSpace(categoryList) ? "{}" : categoryList.Trim();
            string sql = @"
                INSERT INTO [dbo].[UserCategoryAccess] ([UserId], [IdCoQuan], [CategoryList], [CreatedDate], [ModifiedDate], [Deleted])
                VALUES (@UserId, @IdCoQuan, @CategoryList, GETDATE(), GETDATE(), 0);
                SELECT SCOPE_IDENTITY() AS Id;";

            string[] paramNames = { "@UserId", "@CategoryList", "@IdCoQuan" };
            object[] values = { userId, categoryList ?? (object)DBNull.Value, _idCoQuan };

            DataTable dt = ConnectDb.ExecuteQuerySQLWithParamsTask(_connectionString, sql, paramNames, values);
            if (dt.Rows.Count > 0 && dt.Columns.Contains("Id"))
            {
                return Convert.ToInt32(dt.Rows[0]["Id"]);
            }
            return -1;  // Lỗi insert
        }

        /// <summary>
        /// Lấy thông tin UserCategoryAccess theo UserId (chỉ bản ghi chưa xóa).
        /// Trả về DataTable với cột: Id, UserId, CategoryList, CreatedDate, ModifiedDate.
        /// </summary>
        public DataTable GetByUserId(int userId)
        {
            string sql = @"
                SELECT [Id], [UserId], [CategoryList], [CreatedDate], [ModifiedDate]
                FROM [dbo].[UserCategoryAccess]
                WHERE [UserId] = @UserId AND [Deleted] = 0;";

            string[] paramNames = { "@UserId" };
            object[] values = { userId };

            return ConnectDb.ExecuteQuerySQLWithParamsTask(_connectionString, sql, paramNames, values);
        }

        /// <summary>
        /// Lấy thông tin UserCategoryAccess theo Id (chỉ bản ghi chưa xóa).
        /// Trả về DataTable với tất cả cột.
        /// </summary>
        public DataTable GetById(int id)
        {
            string sql = @"
                SELECT [Id], [UserId], [CategoryList], [CreatedDate], [ModifiedDate], [Deleted]
                FROM [dbo].[UserCategoryAccess]
                WHERE [Id] = @Id AND [Deleted] = 0;";

            string[] paramNames = { "@Id" };
            object[] values = { id };

            return ConnectDb.ExecuteQuerySQLWithParamsTask(_connectionString, sql, paramNames, values);
        }

        /// <summary>
        /// Lấy tất cả UserCategoryAccess chưa xóa.
        /// Trả về DataTable với cột: Id, UserId, CategoryList, CreatedDate, ModifiedDate.
        /// </summary>
        public DataTable GetAll()
        {
            string sql = @"
                SELECT [Id], [UserId], [CategoryList], [CreatedDate], [ModifiedDate]
                FROM [dbo].[UserCategoryAccess]
                WHERE [Deleted] = 0
                ORDER BY [CreatedDate] DESC;";

            // Không cần params
            return ConnectDb.ExecuteQuerySQLTask(_connectionString, sql);
        }

        /// <summary>
        /// Cập nhật CategoryList cho bản ghi theo Id (nếu chưa xóa).
        /// Trả về số hàng bị ảnh hưởng (1 nếu thành công, 0 nếu không tìm thấy).
        /// </summary>
        public int Update(int id, string categoryList)
        {
            // Cải tiến: Đảm bảo categoryList luôn là string hợp lệ, fallback "{}" nếu null/empty
            categoryList = string.IsNullOrWhiteSpace(categoryList) ? "{}" : categoryList.Trim();
            string sql = @"
                UPDATE [dbo].[UserCategoryAccess]
                SET [CategoryList] = @CategoryList, [ModifiedDate] = GETDATE()
                WHERE [Id] = @Id AND [Deleted] = 0;";

            string[] paramNames = { "@Id", "@CategoryList" };
            object[] values = { id, categoryList ?? (object)DBNull.Value };

            return ConnectDb.ExecuteNonQueryWithParamsTask(_connectionString, sql, paramNames, values);
        }

        /// <summary>
        /// Xóa mềm bản ghi theo Id (set Deleted = 1).
        /// Trả về số hàng bị ảnh hưởng (1 nếu thành công, 0 nếu không tìm thấy).
        /// </summary>
        public int Delete(int id)
        {
            string sql = @"
                UPDATE [dbo].[UserCategoryAccess]
                SET [Deleted] = 1, [ModifiedDate] = GETDATE()
                WHERE [Id] = @Id AND [Deleted] = 0;";

            string[] paramNames = { "@Id" };
            object[] values = { id };

            return ConnectDb.ExecuteNonQueryWithParamsTask(_connectionString, sql, paramNames, values);
        }

        /// <summary>
        /// Kiểm tra quyền đăng bài cho user trong chuyên mục cụ thể.
        /// Trả về true nếu role >=1.
        /// </summary>
        public bool CanPostArticle(int catId, int userId, int coQuanId)
        {
            return HasPermission(catId, userId, coQuanId, 1);
        }

        /// <summary>
        /// Kiểm tra quyền chỉnh sửa bài viết cho user trong chuyên mục cụ thể.
        /// Trả về true nếu role >=2.
        /// </summary>
        public bool CanEditArticle(int catId, int userId, int coQuanId)
        {
            return HasPermission(catId, userId, coQuanId, 2);
        }

        /// <summary>
        /// Kiểm tra quyền duyệt bài viết cho user trong chuyên mục cụ thể.
        /// Trả về true nếu role =3.
        /// </summary>
        public bool CanApproveArticle(int catId, int userId, int coQuanId)
        {
            return HasPermission(catId, userId, coQuanId, 3);
        }

        /// <summary>
        /// Lấy danh sách chuyên mục mà user có quyền đăng bài (role >=1).
        /// </summary>
        public List<int> GetPostableCategories(int userId, int coQuanId)
        {
            return GetCategoriesByMinRole(userId, coQuanId, 1);
        }

        /// <summary>
        /// Lấy danh sách chuyên mục mà user có quyền chỉnh sửa bài viết (role >=2).
        /// </summary>
        public List<int> GetEditableCategories(int userId, int coQuanId)
        {
            return GetCategoriesByMinRole(userId, coQuanId, 2);
        }

        /// <summary>
        /// Lấy danh sách chuyên mục mà user có quyền duyệt bài viết (role =3).
        /// </summary>
        public List<int> GetApprovableCategories(int userId, int coQuanId)
        {
            return GetCategoriesByMinRole(userId, coQuanId, 3);
        }

        /// <summary>
        /// Helper method để kiểm tra role >= threshold cho catId, userId, coQuanId.
        /// Parse CategoryList và kiểm tra dictionary.
        /// </summary>
        private bool HasPermission(int catId, int userId, int coQuanId, int minRole)
        {
            var permissions = GetPermissionsDictionary(userId, coQuanId);
            return permissions.TryGetValue(catId, out int role) && role >= minRole;
        }

        /// <summary>
        /// Lấy dictionary {catId: role} cho user và cơ quan.
        /// Cache nếu cần, nhưng giữ đơn giản.
        /// </summary>
        private Dictionary<int, int> GetPermissionsDictionary(int userId, int coQuanId)
        {
            var dict = new Dictionary<int, int>();

            // Get CategoryList cho user và cơ quan
            string sql = @"
                SELECT [CategoryList]
                FROM [dbo].[UserCategoryAccess]
                WHERE [UserId] = @UserId AND [IdCoQuan] = @CoQuanId AND [Deleted] = 0;";

            string[] paramNames = { "@UserId", "@CoQuanId" };
            object[] values = { userId, coQuanId };

            DataTable dt = ConnectDb.ExecuteQuerySQLWithParamsTask(_connectionString, sql, paramNames, values);
            if (dt.Rows.Count == 0)
            {
                return dict;  // Empty dict
            }

            string categoryListJson = dt.Rows[0]["CategoryList"]?.ToString() ?? "{}";
            if (string.IsNullOrWhiteSpace(categoryListJson) || categoryListJson == "{}" || categoryListJson == "{ }")
            {
                return dict;  // Empty
            }

            // Fix JSON: Add quotes around numeric keys (e.g., { 1:3 } → { "1":3 })
            string fixedJson = Regex.Replace(categoryListJson.Trim(), @"\s*(\d+)\s*:", "\"$1\":");

            // Parse thành Dictionary<string, int>
            try
            {
                var tempDict = JsonSerializer.Deserialize<Dictionary<string, int>>(fixedJson) ?? new Dictionary<string, int>();
                foreach (var kvp in tempDict)
                {
                    if (int.TryParse(kvp.Key, out int key))
                    {
                        dict[key] = kvp.Value;
                    }
                }
            }
            catch (JsonException)
            {
                // Invalid JSON, return empty
            }

            return dict;
        }

        /// <summary>
        /// Lấy danh sách CatId nơi role >= minRole cho user và cơ quan.
        /// </summary>
        private List<int> GetCategoriesByMinRole(int userId, int coQuanId, int minRole)
        {
            var permissions = GetPermissionsDictionary(userId, coQuanId);
            return permissions.Where(kvp => kvp.Value >= minRole).Select(kvp => kvp.Key).ToList();
        }
    }
}