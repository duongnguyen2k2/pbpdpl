using API.Areas.Admin.Models.DocumentsField;
using API.Areas.Admin.Models.Partial;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
/*https://docs.microsoft.com/en-us/aspnet/core/tutorials/razor-pages/validation?view=aspnetcore-2.2*/
namespace API.Areas.Admin.Models.DMLinhVuc
{
    public class DMLinhVuc
    {
        public string? Ids { get; set; }
        public int TotalRows { get; set; }
        public int Id { get; set; }
        public string? Alias { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public Boolean Status { get; set; }
        public Boolean Deleted { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class DMLinhVucModel {
        public List<DMLinhVuc> ListItems { get; set; } = new List<DMLinhVuc>();
        public SearchDMLinhVuc SearchData { get; set; } = new SearchDMLinhVuc() { };
        public DMLinhVuc Item { get; set; } = new DMLinhVuc() { };
        public PartialPagination Pagination { get; set; } = new PartialPagination() { };
    }
    public class SearchDMLinhVuc {
        public int CurrentPage { get; set; }
        public int ItemsPerPage { get; set; }
        public string? Keyword { get; set; }
    }
}
