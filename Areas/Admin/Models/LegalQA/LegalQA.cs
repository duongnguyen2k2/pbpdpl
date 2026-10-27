using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Areas.Admin.Models.Partial;

namespace API.Areas.Admin.Models.LegalQA
{
    public class LegalQA
    {
        public string? Ids { get; set; }
        public int TotalRows { get; set; }
        public int Id { get; set; }

        [StringLength(1024, MinimumLength = 3, ErrorMessage = "Độ dài chuỗi phải lớn hơn {2} Không quá {1} ký tự")]
        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        public string? Title { get; set; }

        public bool Status { get; set; }

        [StringLength(1024, MinimumLength = 3, ErrorMessage = "Độ dài chuỗi phải lớn hơn {2} Không quá {1} ký tự")]
        [Required(ErrorMessage = "Tên không được để trống")]
        public string? Fullname { get; set; }

        [StringLength(2000, MinimumLength = 3, ErrorMessage = "Độ dài chuỗi phải lớn hơn {2} Không quá {1} ký tự")]
        [Required(ErrorMessage = "Nội dung không được để trống")]
        public string? Introtext { get; set; }

        public string? Description { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public bool Deleted { get; set; }
        public int CreatedBy { get; set; }
        public int IdCoQuan { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime PublishUp { get; set; } = DateTime.Now;
        public string? PublishUpShow { get; set; } = DateTime.Now.ToString("dd/MM/yyyy");
        public string? Token { get; set; }
    }

    public class LegalQAModel
    {
        public List<LegalQA> ListItems { get; set; } = new List<LegalQA>();
        public SearchLegalQA SearchData { get; set; } = new SearchLegalQA();
        public LegalQA Item { get; set; } = new LegalQA();
        public PartialPagination Pagination { get; set; } = new PartialPagination();
    }

    public class SearchLegalQA
    {
        public int CurrentPage { get; set; }
        public int ItemsPerPage { get; set; }
        public int IdCoQuan { get; set; }
        public int Status { get; set; } = -1;
        public string? Keyword { get; set; }
    }
}
