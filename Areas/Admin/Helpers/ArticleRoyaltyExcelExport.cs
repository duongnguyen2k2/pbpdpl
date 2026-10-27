using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using API.Areas.Admin.Models.Articles;
using API.Areas.Admin.Models.DMCoQuan;
using API.Models.MyHelper;
using ClosedXML.Excel;
using Newtonsoft.Json.Linq;

#nullable enable

namespace API.Areas.Admin.Helpers
{
    /// <summary>
    /// Xuất danh sách bài viết / nhuận bút ra Excel.
    /// Header trái + cột giữa (F–H) theo từng DMCoQuan; quốc hiệu cố định cột I–M.
    /// Cột F–H: Metadata.RoyaltyExcelHeaderBesideMotto, hoặc dòng 2+ của CompanyName, hoặc Title/Slogan.
    /// </summary>
    public static class ArticleRoyaltyExcelExport
    {
        private const int ColCount = 13;
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        public static void SaveReport(
            IReadOnlyList<Articles> articles,
            SearchArticles dto,
            DMCoQuan itemCoQuan,
            string outputPath)
        {
            ArgumentNullException.ThrowIfNull(dto);
            ArgumentNullException.ThrowIfNull(itemCoQuan);

            var rows = BuildRows(articles);
            decimal grandTotal = rows.Sum(r => r.LineSumMoney);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Danh sách");
            ws.Columns(1, ColCount).Width = 12;
            ws.Column(2).Width = 42;
            ws.Column(1).Width = 5;
            ws.Column(6).Width = 14;
            ws.Column(7).Width = 14;
            ws.Column(8).Width = 14;

            int r = 1;
            var (leftBlock, besideMottoBlock) = ResolveHeaderBlocks(itemCoQuan);

            const int colLeftEnd = 6;
            //const int colBesideStart = 6;
            //const int colBesideEnd = 8;
            const int colMottoStart = 7;

            //ws.Range(r, 1, r + 1, colLeftEnd).Merge();
            //ws.Cell(r, 1).Value = leftBlock;
            //ws.Cell(r, 1).Style.Alignment.WrapText = true;
            //ws.Cell(r, 1).Style.Font.Bold = true;
            //ws.Cell(r, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            //ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            ws.Range(r, 1, r + 1, colLeftEnd).Merge();
            ws.Cell(r, 1).Value = string.IsNullOrWhiteSpace(besideMottoBlock)
                ? string.Empty
                : besideMottoBlock;
            ws.Cell(r, 1).Style.Alignment.WrapText = true;
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(r, colMottoStart, r, ColCount).Merge();
            ws.Cell(r, colMottoStart).Value = "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM";
            ws.Cell(r, colMottoStart).Style.Font.Bold = true;
            ws.Cell(r, colMottoStart).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            r++;
            ws.Range(r, colMottoStart, r, ColCount).Merge();
            ws.Cell(r, colMottoStart).Value = "Độc lập - Tự do - Hạnh phúc";
            ws.Cell(r, colMottoStart).Style.Font.Bold = true;
            ws.Cell(r, colMottoStart).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            r += 2;
            string mainTitle = BuildMainTitle(dto);
            ws.Range(r, 1, r, ColCount).Merge();
            ws.Cell(r, 1).Value = mainTitle;
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Font.FontSize = 13;
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            r++;
            ws.Range(r, 1, r, ColCount).Merge();
            ws.Cell(r, 1).Value = $"(Từ ngày {dto.ShowStartDate} đến ngày {dto.ShowEndDate})";
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            r += 2;
            string[] headers =
            {
                "Stt", "Tên bài viết", "Ngày đăng", "Tác giả",
                "Hệ số tin, bài", "Loại tin, bài", "Số hình ảnh", "Loại hình ảnh", "Hệ số ảnh",
                "Nhuận bút tin, bài", "Nhuận bút ảnh", "Tổng cộng", "Ký nhận"
            };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(r, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int headerRow = r;
            r++;

            foreach (var row in rows)
            {
                WriteDataRow(ws, r, row);
                r++;
            }

            int dataEndRow = r - 1;
            if (dataEndRow < headerRow + 1)
                dataEndRow = headerRow;

            ws.Range(headerRow, 1, dataEndRow, ColCount).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, dataEndRow, ColCount).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            r++;
            ws.Range(r, 1, r, 11).Merge();
            ws.Cell(r, 1).Value = "Tổng cộng";
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 12).Value = grandTotal;
            ws.Cell(r, 12).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 12).Style.Font.Bold = true;
            ws.Range(r, 1, r, ColCount).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            r++;
            ws.Range(r, 1, r, ColCount).Merge();
            var bangChu = StringHelper.NumberToTextVN(grandTotal).Trim();
            if (string.IsNullOrEmpty(bangChu))
                bangChu = "Không";
            ws.Cell(r, 1).Value = $"Bằng chữ: ({bangChu} đồng chẵn)";
            ws.Cell(r, 1).Style.Alignment.WrapText = true;

            r += 2;
            int sigRow = r;
            int[] sigStarts = { 1, 4, 7, 10 };
            int[] sigWidths = { 3, 3, 3, 4 };
            string[] roles = { "NGƯỜI LẬP", "P. TRƯỞNG BAN BIÊN TẬP", "KẾ TOÁN", "TRƯỞNG BAN BIÊN TẬP" };
            for (int i = 0; i < 4; i++)
            {
                int c0 = sigStarts[i];
                int w = sigWidths[i];
                var block = ws.Range(sigRow, c0, sigRow + 4, c0 + w - 1);
                block.Merge();
                block.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Cell(sigRow, c0).Value = roles[i];
                ws.Cell(sigRow, c0).Style.Font.Bold = true;
                ws.Cell(sigRow, c0).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(sigRow, c0).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                ws.Cell(sigRow, c0).Style.Alignment.WrapText = true;
            }

            for (int rr = sigRow; rr <= sigRow + 4; rr++)
                ws.Row(rr).Height = 22;

            wb.SaveAs(outputPath);
        }

        private static void WriteDataRow(IXLWorksheet ws, int r, RoyaltyRow row)
        {
            ws.Cell(r, 1).Value = row.Stt;
            ws.Cell(r, 2).Value = row.Title ?? string.Empty;
            ws.Cell(r, 3).Value = row.PublishDate ?? string.Empty;
            ws.Cell(r, 4).Value = row.Author ?? string.Empty;

            SetNullableDouble(ws.Cell(r, 5), row.HeSoTinBai);
            ws.Cell(r, 6).Value = row.LoaiTinBai ?? string.Empty;

            ws.Cell(r, 7).Value = row.SoHinhAnh;

            ws.Cell(r, 8).Value = row.LoaiHinhAnh ?? string.Empty;
            SetNullableDouble(ws.Cell(r, 9), row.HeSoAnh);
            SetNullableInt(ws.Cell(r, 10), row.NhuanButTinBai);
            SetNullableInt(ws.Cell(r, 11), row.NhuanButAnh);
            SetNullableInt(ws.Cell(r, 12), row.TongCong);
            ws.Cell(r, 13).Value = string.Empty;

            ws.Cell(r, 5).Style.NumberFormat.Format = "#,##0.0##";
            ws.Cell(r, 9).Style.NumberFormat.Format = "#,##0.0##";
            ws.Cell(r, 10).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 11).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 12).Style.NumberFormat.Format = "#,##0";

            for (int c = 1; c <= ColCount; c++)
                ws.Cell(r, c).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            ws.Cell(r, 2).Style.Alignment.WrapText = true;
        }

        private static void SetNullableDouble(IXLCell cell, double? value)
        {
            if (value.HasValue)
                cell.Value = value.Value;
        }

        private static void SetNullableInt(IXLCell cell, int? value)
        {
            if (value.HasValue)
                cell.Value = value.Value;
        }

        /// <summary>
        /// Khối trái (A–E): dòng đầu tên đơn vị. Khối giữa (F–H): nội dung riêng cạnh quốc hiệu.
        /// </summary>
        private static (string Left, string Beside) ResolveHeaderBlocks(DMCoQuan q)
        {
            string company = (q.CompanyName ?? string.Empty).Trim();
            var companyLines = company
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();

            string configuredAside = q.MetadataCV?.RoyaltyExcelHeaderBesideMotto?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(configuredAside))
            {
                string left = companyLines.Length > 0
                    ? companyLines[0]
                    : (q.Title ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(left))
                    left = (q.Title ?? string.Empty).Trim();
                return (left, configuredAside);
            }

            if (companyLines.Length > 1)
            {
                return (
                    companyLines[0],
                    string.Join(Environment.NewLine, companyLines.Skip(1))
                );
            }

            string leftOnly = companyLines.Length == 1 ? companyLines[0] : (q.Title ?? string.Empty).Trim();
            string title = (q.Title ?? string.Empty).Trim();
            string beside = string.Empty;
            if (!string.IsNullOrEmpty(title) && !title.Equals(leftOnly, StringComparison.OrdinalIgnoreCase))
                beside = title;
            else
            {
                var slogan = (q.Slogan ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(slogan))
                    beside = slogan;
            }

            return (leftOnly, beside);
        }

        private static string BuildMainTitle(SearchArticles dto)
        {
            if (DateTime.TryParseExact(dto.ShowEndDate, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var end))
            {
                int q = (end.Month - 1) / 3 + 1;
                return $"DANH SÁCH Chi nhuận bút Trang Thông tin điện tử Quý {q} năm {end.Year}";
            }

            return "DANH SÁCH Chi nhuận bút Trang Thông tin điện tử";
        }

        private static List<RoyaltyRow> BuildRows(IReadOnlyList<Articles>? articles)
        {
            var list = new List<RoyaltyRow>();
            if (articles == null || articles.Count == 0)
                return list;

            int stt = 0;
            foreach (var a in articles)
            {
                stt++;
                var meta = ParseMetaMoney(a.MetaMoney);
                string author = !string.IsNullOrWhiteSpace(a.Author)
                    ? a.Author.Trim()
                    : (a.AuthorName ?? string.Empty).Trim();

                string loaiTin = string.IsNullOrWhiteSpace(a.CategoryTypeTitle) ? string.Empty : a.CategoryTypeTitle.Trim();

                double? heSoTin = GetJsonDouble(meta, "HeSoTinBai");
                double? heSoAnh = GetJsonDouble(meta, "HeSoHinhAnh");
                int? nbTin = GetJsonInt(meta, "NhuanButTinBai");
                int? nbAnh = GetJsonInt(meta, "NhuanButAnh");
                string? loaiAnh = GetJsonString(meta, "LoaiHinhAnh");

                int? tong;
                decimal lineSum;
                if (nbTin.HasValue || nbAnh.HasValue)
                {
                    tong = (nbTin ?? 0) + (nbAnh ?? 0);
                    lineSum = tong.Value;
                }
                else if (Math.Abs(a.Money) > double.Epsilon)
                {
                    tong = (int)Math.Round(a.Money, MidpointRounding.AwayFromZero);
                    lineSum = tong.Value;
                }
                else
                {
                    tong = null;
                    lineSum = 0m;
                }

                list.Add(new RoyaltyRow
                {
                    Stt = stt,
                    Title = a.Title,
                    PublishDate = a.PublishUpShow ?? a.PublishUp.ToString("dd/MM/yyyy", Vi),
                    Author = author,
                    HeSoTinBai = heSoTin,
                    LoaiTinBai = string.IsNullOrEmpty(loaiTin) ? null : loaiTin,
                    SoHinhAnh = a.QuantityImage,
                    LoaiHinhAnh = loaiAnh,
                    HeSoAnh = heSoAnh,
                    NhuanButTinBai = nbTin,
                    NhuanButAnh = nbAnh,
                    TongCong = tong,
                    LineSumMoney = lineSum
                });
            }

            return list;
        }

        private static JObject? ParseMetaMoney(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                return JObject.Parse(json);
            }
            catch
            {
                return null;
            }
        }

        private static double? GetJsonDouble(JObject? o, string name)
        {
            if (o == null || !o.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var t) || t == null || t.Type == JTokenType.Null)
                return null;
            if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer)
                return t.Value<double>();
            if (t.Type == JTokenType.String && double.TryParse(t.Value<string>(), NumberStyles.Any, Vi, out var d))
                return d;
            return null;
        }

        private static int? GetJsonInt(JObject? o, string name)
        {
            if (o == null || !o.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var t) || t == null || t.Type == JTokenType.Null)
                return null;
            if (t.Type == JTokenType.Integer)
                return t.Value<int>();
            if (t.Type == JTokenType.Float)
                return (int)Math.Round(t.Value<double>(), MidpointRounding.AwayFromZero);
            if (t.Type == JTokenType.String && int.TryParse(t.Value<string>(), NumberStyles.Any, Vi, out var i))
                return i;
            return null;
        }

        private static string? GetJsonString(JObject? o, string name)
        {
            if (o == null || !o.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var t) || t == null || t.Type == JTokenType.Null)
                return null;
            var s = t.Type == JTokenType.String ? t.Value<string>() : t.ToString();
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private sealed class RoyaltyRow
        {
            public int Stt { get; init; }
            public string? Title { get; init; }
            public string? PublishDate { get; init; }
            public string? Author { get; init; }
            public double? HeSoTinBai { get; init; }
            public string? LoaiTinBai { get; init; }
            public int SoHinhAnh { get; init; }
            public string? LoaiHinhAnh { get; init; }
            public double? HeSoAnh { get; init; }
            public int? NhuanButTinBai { get; init; }
            public int? NhuanButAnh { get; init; }
            public int? TongCong { get; init; }
            public decimal LineSumMoney { get; init; }
        }
    }
}
