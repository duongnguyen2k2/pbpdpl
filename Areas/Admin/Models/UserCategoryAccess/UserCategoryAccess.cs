using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Areas.Admin.Models.UserCategoryAccess
{
    public class UserCategoryAccess
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public string? CategoryList { get; set; }  // Lưu JSON string, ví dụ: "{ \"1\":1, \"2\":1 }"


        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;


        public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;


        public bool Deleted { get; set; } = false;
    }

    // Request model cho POST
    public class SavePermissionsRequest
    {
        public int UserId { get; set; }
        public string CategoryList { get; set; } = "{}";
    }
}
