using API.Areas.Admin.Models.LegalQA;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace API.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class LegalQAController : Controller
    {
        private readonly string controllerName = "LegalQAController";
        private readonly string controllerSecret;

        public LegalQAController(IConfiguration config)
        {
            controllerSecret = config["Security:SecretId"] + controllerName;
        }

        public IActionResult Index([FromQuery] SearchLegalQA dto)
        {
            int totalItems = 0;
            dto.IdCoQuan = int.Parse(HttpContext.Request.Headers["IdCoQuan"]);
            LegalQAModel data = new LegalQAModel { SearchData = dto };
            data.ListItems = LegalQAService.GetListPagination(data.SearchData, controllerSecret + HttpContext.Request.Headers["UserName"]);
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

        public IActionResult SaveItem(string Id = null)
        {
            LegalQAModel data = new LegalQAModel();
            int idCoQuan = int.Parse(HttpContext.Request.Headers["IdCoQuan"]);
            int idDC = int.Parse(MyModels.Decode(Id, controllerSecret + HttpContext.Request.Headers["UserName"]).ToString());
            data.SearchData = new SearchLegalQA
            {
                CurrentPage = 0,
                ItemsPerPage = 10,
                Keyword = "",
                IdCoQuan = idCoQuan
            };

            if (idDC == 0)
            {
                data.Item = new LegalQA();
            }
            else
            {
                data.Item = LegalQAService.GetItem(idDC, idCoQuan, controllerSecret + HttpContext.Request.Headers["UserName"]);
            }

            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveItem(LegalQA model)
        {
            int idDC = int.Parse(MyModels.Decode(model.Ids, controllerSecret + HttpContext.Request.Headers["UserName"]).ToString());
            LegalQAModel data = new LegalQAModel { Item = model };
            if (ModelState.IsValid)
            {
                if (model.Id == idDC)
                {
                    model.CreatedBy = int.Parse(HttpContext.Request.Headers["Id"]);
                    model.ModifiedBy = int.Parse(HttpContext.Request.Headers["Id"]);
                    model.IdCoQuan = int.Parse(HttpContext.Request.Headers["IdCoQuan"]);
                    LegalQAService.SaveItem(model);
                    if (model.Id > 0)
                    {
                        TempData["MessageSuccess"] = "Cập nhật thành công";
                    }
                    else
                    {
                        TempData["MessageSuccess"] = "Thêm mới thành công";
                    }
                    return RedirectToAction("Index");
                }
            }

            return View(data);
        }

        [ValidateAntiForgeryToken]
        public ActionResult DeleteItem(string Id)
        {
            LegalQA model = new LegalQA
            {
                Id = int.Parse(MyModels.Decode(Id, controllerSecret + HttpContext.Request.Headers["UserName"]).ToString())
            };

            try
            {
                if (model.Id > 0)
                {
                    model.CreatedBy = int.Parse(HttpContext.Request.Headers["Id"]);
                    model.ModifiedBy = int.Parse(HttpContext.Request.Headers["Id"]);
                    LegalQAService.DeleteItem(model);
                    TempData["MessageSuccess"] = "Xóa thành công";
                    return Json(new MsgSuccess());
                }

                TempData["MessageError"] = "Xóa Không thành công";
                return Json(new MsgError());
            }
            catch
            {
                TempData["MessageSuccess"] = "Xóa không thành công";
                return Json(new MsgError());
            }
        }

        [ValidateAntiForgeryToken]
        public ActionResult UpdateStatus([FromQuery] string Ids, bool Status)
        {
            LegalQA item = new LegalQA
            {
                Id = int.Parse(MyModels.Decode(Ids, controllerSecret + HttpContext.Request.Headers["UserName"]).ToString()),
                Status = Status
            };

            try
            {
                if (item.Id > 0)
                {
                    item.CreatedBy = int.Parse(HttpContext.Request.Headers["Id"]);
                    item.ModifiedBy = int.Parse(HttpContext.Request.Headers["Id"]);
                    LegalQAService.UpdateStatus(item);
                    TempData["MessageSuccess"] = "Cập nhật Trạng Thái thành công";
                    return Json(new MsgSuccess());
                }

                TempData["MessageError"] = "Cập nhật Trạng Thái Không thành công";
                return Json(new MsgError());
            }
            catch
            {
                TempData["MessageSuccess"] = "Cập nhật Trạng Thái không thành công";
                return Json(new MsgError());
            }
        }
    }
}
