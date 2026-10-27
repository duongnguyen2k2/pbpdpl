using AngleSharp.Css;
using API.Areas.Admin.Models.Articles;
using API.Areas.Admin.Models.CategoriesArticles;
using API.Models.Sitemap;
using DocumentFormat.OpenXml.Drawing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace API.Controllers
{
    public class SitemapController : Controller
    {
        public IActionResult Index()
        {
            var urls = new List<SitemapUrl>();
            int IdCoQuan = 1;
            if (HttpContext.Session.GetString("IdCoQuan") != null && HttpContext.Session.GetString("IdCoQuan") != "")
            {
                IdCoQuan = int.Parse(HttpContext.Session.GetString("IdCoQuan"));
            }

            string DomainName = HttpContext.Session.GetString("DomainName").ToString();
            DomainName = String.IsNullOrEmpty(DomainName) ? "daklak.gov.vn" : DomainName.ToLower();

            if (HttpContext.Request.IsHttps)
            {
                DomainName = "https://" + DomainName + "/";
            }
            else
            {
                DomainName = "http://" + DomainName + "/";
            }


            urls.Add(new SitemapUrl { Loc = DomainName, LastMod = DateTime.Now, ChangeFreq = "always", Priority = Convert.ToDouble(1.0) });

            // get list cat || yearly || 0.8
            List<ArticleInfo> listcat = CategoriesArticlesService.GetAllCategory(IdCoQuan);
            if (listcat.Count > 0)
            {
                for (int i = 0; i < listcat.Count; i++)
                {
                    urls.Add(new SitemapUrl { Loc = DomainName + listcat[i].Alias + "-" + listcat[i].Id + ".html", LastMod = listcat[i].CreatedDate, ChangeFreq = listcat[i].ChangeFreq, Priority = Convert.ToDouble(listcat[i].Priority) });
                }
            }

            // get list top 100 article || daily || 1.0
            List<ArticleInfo> artile = ArticlesService.GetListForSiteMap(IdCoQuan);
            if (artile.Count > 0)
            {
                for (int i = 0; i < artile.Count; i++)
                {
                    urls.Add(new SitemapUrl { Loc = DomainName + artile[i].Alias + "-" + artile[i].Id + ".html", LastMod = artile[i].CreatedDate, ChangeFreq = artile[i].ChangeFreq, Priority = Convert.ToDouble(artile[i].Priority) });
                }
            }

            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

            var xml = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(ns + "urlset",
                    urls.Select(u =>
                        new XElement(ns + "url",
                            new XElement(ns + "loc", u.Loc),
                            new XElement(ns + "lastmod", u.LastMod.ToString("yyyy-MM-dd")),
                            new XElement(ns + "changefreq", u.ChangeFreq),
                            new XElement(ns + "priority", u.Priority.ToString("F1").Replace(",", "."))
                        )
                    )
                )
            );

            return Content(xml.ToString(), "application/xml");
        }
    }
}
