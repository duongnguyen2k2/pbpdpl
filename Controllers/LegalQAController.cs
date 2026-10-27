using API.Areas.Admin.Models.DMCoQuan;
using API.Areas.Admin.Models.LegalQA;
using API.Areas.Admin.Models.USUsers;
using API.Models.MyHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace API.Controllers
{
    public class LegalQAController : Controller
    {
        private readonly string controllerName = "LegalQAController";
        private readonly string controllerSecret;
        private readonly IConfiguration configuration;
        private readonly IGoogleService google;

        public LegalQAController(IConfiguration config, IGoogleService googleService)
        {
            google = googleService;
            configuration = config;
            controllerSecret = config["Security:SecretId"] + controllerName + "client";
        }

        public IActionResult Index()
        {
            LegalQAModel data = new LegalQAModel
            {
                SearchData = new SearchLegalQA { CurrentPage = 0, ItemsPerPage = 10, Keyword = "" },
                Item = new LegalQA()
            };
            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LegalQA model, string Website, long FormStartTime)
        {
            if (!string.IsNullOrEmpty(Website))
            {
                TempData["MessageError"] = "Yêu cầu không hợp lệ.";
                return View(new LegalQAModel { Item = model });
            }

            var timeElapsed = (DateTime.Now.Ticks - FormStartTime) / TimeSpan.TicksPerSecond;
            if (timeElapsed < 5)
            {
                TempData["MessageError"] = "Vui lòng nhập đầy đủ thông tin trước khi gửi.";
                return View(new LegalQAModel { Item = model });
            }

            LegalQAModel data = new LegalQAModel { Item = model };
            bool checkGoogle = google.CheckGoogle(configuration["RecaptchaSettings:SecretKey"], model.Token, int.Parse(configuration["flagCaptCha"].ToString()));

            USUsersLog userLog = new USUsersLog
            {
                Browser = "LegalQA",
                Platform = "Index",
                IdUser = 0,
                LoginInfo = checkGoogle.ToString(),
                Ip = "",
                Token = model.Token,
                Description = "",
            };

            USUsersService.SaveUserLog(userLog);

            if (!checkGoogle)
            {
                TempData["MessageError"] = "Trình duyệt máy bạn Gửi yêu cầu thất bại. Xin vui lòng gửi lại";
                return View(data);
            }

            if (ModelState.IsValid)
            {
                DMCoQuan itemCoQuan = JsonConvert.DeserializeObject<DMCoQuan>(HttpContext.Session.GetString("ThongTinCoQuan"));
                model.Id = 0;
                model.CreatedBy = 0;
                model.ModifiedBy = 0;
                model.IdCoQuan = int.Parse(HttpContext.Session.GetString("DomainId"));

                try
                {
                    LegalQAService.SaveItem(model);

                    Telegram tele = new Telegram();
                    string smsTele = itemCoQuan.Code + " - <b>" + HttpContext.Connection.RemoteIpAddress + "</b> - <b>LegalQA</b> - Hỏi Đáp Pháp Luật";
                    Telegram.Info teleInfo = new Telegram.Info { Msg = smsTele };
                    tele.SendNotification(teleInfo, configuration["flagTelegram"]);

                    TempData["MessageSuccess"] = "Câu hỏi pháp luật của bạn đã gửi thành công, đang chuyển bộ phận xử lý và trả lời trong thời gian sớm nhất!";

                    if (configuration["flagSendEmail"] == "1")
                    {
                        MailSettings settings = new MailSettings
                        {
                            Host = configuration["MailSettings:Host"],
                            Port = int.Parse(configuration["MailSettings:Port"]),
                            Password = configuration["MailSettings:Password"],
                            DisplayName = configuration["MailSettings:DisplayName"],
                            Mail = configuration["MailSettings:Mail"],
                        };

                        string linkEmail = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ReportTemplate/email_contact.html");
                        string textBody = System.IO.File.ReadAllText(linkEmail);
                        textBody = LegalQAService.ConvertBodyEmail(model, textBody);
                        MailRequest request = new MailRequest
                        {
                            ToEmail = itemCoQuan.Email,
                            Subject = model.Title,
                            Body = textBody,
                        };
                        await MyEmail.SendEmailAsync(settings, request);
                    }
                }
                catch
                {
                    TempData["MessageSuccess"] = "";
                    TempData["MessageError"] = "Gửi câu hỏi Thất bại. Xin vui lòng gửi lại";
                }

                return RedirectToAction("Index");
            }

            return View(data);
        }

        public IActionResult List([FromQuery] SearchLegalQA dto)
        {
            int totalItems = 0;
            int idCoQuan = 1;
            if (HttpContext.Session.GetString("IdCoQuan") != null && HttpContext.Session.GetString("IdCoQuan") != "")
            {
                idCoQuan = int.Parse(HttpContext.Session.GetString("IdCoQuan"));
            }

            LegalQAModel data = new LegalQAModel
            {
                SearchData = new SearchLegalQA { CurrentPage = 0, ItemsPerPage = 10, Keyword = "", IdCoQuan = idCoQuan, Status = 1 },
                Item = new LegalQA()
            };

            data.ListItems = LegalQAService.GetListPagination(data.SearchData, controllerSecret);
            if (data.ListItems != null && data.ListItems.Count > 0)
            {
                totalItems = data.ListItems[0].TotalRows;
            }
            data.Pagination = new Areas.Admin.Models.Partial.PartialPagination
            {
                CurrentPage = data.SearchData.CurrentPage,
                ItemsPerPage = data.SearchData.ItemsPerPage,
                TotalItems = totalItems,
                QueryString = Request.QueryString.ToString()
            };

            return View(data);
        }
    }
}
