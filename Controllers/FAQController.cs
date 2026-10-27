using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Areas.Admin.Models.FAQ;
using API.Areas.Admin.Models.DMCoQuan;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace API.Controllers
{
    public class FAQController : Controller
    {
        /// <summary>
        /// Trang hiển thị danh sách Câu hỏi thường gặp (FAQ) cho người dùng.
        /// </summary>
        /// <returns></returns>
        public IActionResult Index()
        {
            // Lấy danh sách FAQ đang được kích hoạt
            List<FAQ> items = FAQService.GetList(true);

            // Thiết lập một số thông tin SEO cơ bản nếu có cấu hình đơn vị
            try
            {
                string strThongTinCoQuan = HttpContext.Session.GetString("ThongTinCoQuan");
                if (!string.IsNullOrEmpty(strThongTinCoQuan))
                {
                    DMCoQuan itemCoQuan = JsonConvert.DeserializeObject<DMCoQuan>(strThongTinCoQuan);

                    string domain;
                    if (Request.IsHttps)
                    {
                        domain = "https://" + Request.Host;
                    }
                    else
                    {
                        domain = "http://" + Request.Host;
                    }

                    ViewData["Title"] = "Câu hỏi thường gặp - " + itemCoQuan.Title;
                    ViewData["Description"] = "Tổng hợp các câu hỏi thường gặp và giải đáp chi tiết.";
                    ViewData["Keywords"] = "FAQ, Câu hỏi thường gặp, giải đáp";
                    ViewData["Url"] = domain + Request.Path;
                    ViewData["Images"] = domain + "/avatar_hoi_dap.jpg?v=" + DateTime.Now.ToString("yyyyMMddhh");
                    ViewData["Alias"] = "Câu hỏi thường gặp";
                    ViewData["Type"] = "article";
                }
            }
            catch
            {
                // Nếu có lỗi khi đọc thông tin cơ quan thì bỏ qua, không chặn trang FAQ
            }

            return View(items);
        }
    }
}


