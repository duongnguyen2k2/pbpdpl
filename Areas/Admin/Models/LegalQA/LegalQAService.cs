using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using API.Models;

namespace API.Areas.Admin.Models.LegalQA
{
    public class LegalQAService
    {
        public static List<LegalQA> GetListPagination(SearchLegalQA dto, string secretId)
        {
            if (dto.CurrentPage <= 0)
            {
                dto.CurrentPage = 1;
            }

            if (dto.ItemsPerPage <= 0)
            {
                dto.ItemsPerPage = 10;
            }

            if (dto.Keyword == null)
            {
                dto.Keyword = "";
            }

            string flag = "GetListPagination_Status";
            bool status = true;
            if (dto.Status == -1)
            {
                flag = "GetListPagination";
            }
            else if (dto.Status == 0)
            {
                status = false;
            }

            var tabl = ConnectDb.ExecuteDataTableTask(
                Startup.ConnectionString,
                "SP_LegalQA",
                new[] { "@flag", "@CurrentPage", "@ItemsPerPage", "@Keyword", "@IdCoQuan", "@Status" },
                new object[] { flag, dto.CurrentPage, dto.ItemsPerPage, dto.Keyword, dto.IdCoQuan, status });

            if (tabl == null)
            {
                return new List<LegalQA>();
            }

            return (from r in tabl.AsEnumerable()
                    select new LegalQA
                    {
                        Id = (int)r["Id"],
                        Title = (string)((r["Title"] == DBNull.Value) ? null : r["Title"]),
                        Status = (bool)r["Status"],
                        Fullname = (string)((r["Fullname"] == DBNull.Value) ? null : r["Fullname"]),
                        Phone = (string)((r["Phone"] == DBNull.Value) ? null : r["Phone"]),
                        Email = (string)((r["Email"] == DBNull.Value) ? null : r["Email"]),
                        Introtext = (string)((r["Introtext"] == DBNull.Value) ? null : r["Introtext"]),
                        Description = (string)((r["Description"] == DBNull.Value) ? null : r["Description"]),
                        Address = (string)((r["Address"] == DBNull.Value) ? null : r["Address"]),
                        Ids = MyModels.Encode((int)r["Id"], secretId),
                        TotalRows = (int)r["TotalRows"],
                        PublishUp = (DateTime)((r["PublishUp"] == DBNull.Value) ? null : r["PublishUp"]),
                        PublishUpShow = (string)((r["PublishUp"] == DBNull.Value) ? DateTime.Now.ToString("dd/MM/yyyy") : ((DateTime)r["PublishUp"]).ToString("dd/MM/yyyy")),
                        IdCoQuan = (int)r["IdCoQuan"],
                    }).ToList();
        }

        public static List<LegalQA> GetList(bool selected = true, int idCoQuan = 1)
        {
            DataTable tabl = ConnectDb.ExecuteDataTableTask(
                Startup.ConnectionString,
                "SP_LegalQA",
                new[] { "@flag", "@Selected", "@IdCoQuan" },
                new object[] { "GetList", Convert.ToDecimal(selected), idCoQuan });

            if (tabl == null)
            {
                return new List<LegalQA>();
            }

            return (from r in tabl.AsEnumerable()
                    select new LegalQA
                    {
                        Id = (int)r["Id"],
                        Title = (string)((r["Title"] == DBNull.Value) ? null : r["Title"]),
                        Status = (bool)r["Status"],
                        Fullname = (string)((r["Fullname"] == DBNull.Value) ? null : r["Fullname"]),
                        Phone = (string)((r["Phone"] == DBNull.Value) ? null : r["Phone"]),
                        Email = (string)((r["Email"] == DBNull.Value) ? null : r["Email"]),
                        Introtext = (string)((r["Introtext"] == DBNull.Value) ? null : r["Introtext"]),
                        Description = (string)((r["Description"] == DBNull.Value) ? null : r["Description"]),
                        Address = (string)((r["Address"] == DBNull.Value) ? null : r["Address"]),
                        PublishUp = (DateTime)((r["PublishUp"] == DBNull.Value) ? null : r["PublishUp"]),
                        PublishUpShow = (string)((r["PublishUp"] == DBNull.Value) ? DateTime.Now.ToString("dd/MM/yyyy") : ((DateTime)r["PublishUp"]).ToString("dd/MM/yyyy")),
                        IdCoQuan = (int)r["IdCoQuan"],
                    }).ToList();
        }

        public static LegalQA GetItem(decimal id, int idCoQuan, string secretId = null)
        {
            DataTable tabl = ConnectDb.ExecuteDataTableTask(
                Startup.ConnectionString,
                "SP_LegalQA",
                new[] { "@flag", "@Id", "@IdCoQuan" },
                new object[] { "GetItem", id, idCoQuan });

            return (from r in tabl.AsEnumerable()
                    select new LegalQA
                    {
                        Id = (int)r["Id"],
                        Title = (string)((r["Title"] == DBNull.Value) ? null : r["Title"]),
                        Status = (bool)r["Status"],
                        Fullname = (string)((r["Fullname"] == DBNull.Value) ? null : r["Fullname"]),
                        Phone = (string)((r["Phone"] == DBNull.Value) ? null : r["Phone"]),
                        Email = (string)((r["Email"] == DBNull.Value) ? null : r["Email"]),
                        Address = (string)((r["Address"] == DBNull.Value) ? null : r["Address"]),
                        Introtext = (string)((r["Introtext"] == DBNull.Value) ? null : r["Introtext"]),
                        Description = (string)((r["Description"] == DBNull.Value) ? null : r["Description"]),
                        Deleted = (bool)r["Deleted"],
                        CreatedBy = (int)r["CreatedBy"],
                        CreatedDate = (DateTime)r["CreatedDate"],
                        ModifiedBy = (int?)((r["ModifiedBy"] == DBNull.Value) ? null : r["ModifiedBy"]),
                        ModifiedDate = (DateTime?)((r["ModifiedDate"] == DBNull.Value) ? null : r["ModifiedDate"]),
                        PublishUp = (DateTime)((r["PublishUp"] == DBNull.Value) ? null : r["PublishUp"]),
                        PublishUpShow = (string)((r["PublishUp"] == DBNull.Value) ? DateTime.Now.ToString("dd/MM/yyyy") : ((DateTime)r["PublishUp"]).ToString("dd/MM/yyyy")),
                        Ids = MyModels.Encode((int)r["Id"], secretId),
                        IdCoQuan = (int)r["IdCoQuan"],
                    }).FirstOrDefault();
        }

        public static string ConvertBodyEmail(LegalQA dto, string body)
        {
            body = body.Replace("{{Title}}", dto.Title);
            body = body.Replace("{{Fullname}}", dto.Fullname);
            body = body.Replace("{{Phone}}", dto.Phone);
            body = body.Replace("{{Address}}", dto.Address);
            body = body.Replace("{{Email}}", dto.Email);
            body = body.Replace("{{Introtext}}", dto.Introtext);
            body = body.Replace("{{Description}}", dto.Description);
            body = body.Replace("{{CreatedDate}}", DateTime.Now.Hour + ":" + DateTime.Now.Minute + " &nbsp; " + API.Models.MyHelper.StringHelper.DayweekVN(DateTime.Now) + ", Ngày " + DateTime.Now.ToString("dd") + ", Tháng " + DateTime.Now.ToString("MM") + " Năm " + DateTime.Now.ToString("yyyy"));
            return body;
        }

        public static dynamic SaveItem(LegalQA dto)
        {
            dto.Title = API.Models.MyHelper.StringHelper.RemoveTagsTitle(dto.Title);
            dto.Fullname = API.Models.MyHelper.StringHelper.RemoveTagsTitle(dto.Fullname);
            dto.Phone = API.Models.MyHelper.StringHelper.RemoveTagsTitle(dto.Phone);
            dto.Email = API.Models.MyHelper.StringHelper.RemoveTagsTitle(dto.Email);
            dto.Address = API.Models.MyHelper.StringHelper.RemoveTagsTitle(dto.Address);
            dto.Description = API.Models.MyHelper.StringHelper.RemoveTagsFullText(dto.Description);
            dto.Introtext = API.Models.MyHelper.StringHelper.RemoveTagsIntroText(dto.Introtext);

            DateTime publishUp = DateTime.Now;
            if (dto.PublishUpShow != null && dto.PublishUpShow != "")
            {
                publishUp = DateTime.ParseExact(dto.PublishUpShow, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            }

            DataTable tabl = ConnectDb.ExecuteDataTableTask(
                Startup.ConnectionString,
                "SP_LegalQA",
                new[] { "@flag", "@Id", "@Title", "@Introtext", "@Description", "@Status", "@Fullname", "@Phone", "@Email", "@Address", "@CreatedBy", "@ModifiedBy", "@IdCoQuan", "@PublishUp" },
                new object[] { "SaveItem", dto.Id, dto.Title, dto.Introtext, dto.Description, dto.Status, dto.Fullname, dto.Phone, dto.Email, dto.Address, dto.CreatedBy, dto.ModifiedBy, dto.IdCoQuan, publishUp });

            return (from r in tabl.AsEnumerable()
                    select new
                    {
                        N = (int)r["N"],
                    }).FirstOrDefault();
        }

        public static dynamic DeleteItem(LegalQA dto)
        {
            DataTable tabl = ConnectDb.ExecuteDataTableTask(
                Startup.ConnectionString,
                "SP_LegalQA",
                new[] { "@flag", "@Id", "@ModifiedBy" },
                new object[] { "DeleteItem", dto.Id, dto.ModifiedBy });

            return (from r in tabl.AsEnumerable()
                    select new
                    {
                        N = (int)r["N"],
                    }).FirstOrDefault();
        }

        public static dynamic UpdateStatus(LegalQA dto)
        {
            DataTable tabl = ConnectDb.ExecuteDataTableTask(
                Startup.ConnectionString,
                "SP_LegalQA",
                new[] { "@flag", "@Id", "@Status", "@ModifiedBy" },
                new object[] { "UpdateStatus", dto.Id, dto.Status, dto.ModifiedBy });

            return (from r in tabl.AsEnumerable()
                    select new
                    {
                        N = (int)r["N"],
                    }).FirstOrDefault();
        }
    }
}
