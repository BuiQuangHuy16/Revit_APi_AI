using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;

namespace Aplication.Commands.LegendAssociate.Services
{
    /// Cung cấp ảnh preview của 1 View bằng cách export PNG tạm rồi load thành BitmapImage.
    /// Trong Revit 2023-2027 không có View.GetPreviewImage — phải đi qua ImageExportOptions
    /// (đòi hỏi valid API context, nên Render phải được gọi từ ExternalEventHandler).
    /// Cache theo ElementId để click qua lại không phải export lại.
    public class PreviewImageProvider
    {
        private readonly Dictionary<ElementId, BitmapSource> _cache =
            new Dictionary<ElementId, BitmapSource>();
        private readonly string _tempFolder;

        public PreviewImageProvider()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "LegendAssociate_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_tempFolder);
        }

        public BitmapSource GetCached(ElementId id)
        {
            if (id == null || id == ElementId.InvalidElementId) return null;
            return _cache.TryGetValue(id, out var b) ? b : null;
        }

        /// CHỈ được gọi trong Revit API context (từ ExternalEventHandler.Execute).
        public BitmapSource Render(Document doc, ElementId viewId, int sizePx)
        {
            if (viewId == null || viewId == ElementId.InvalidElementId) return null;
            if (_cache.TryGetValue(viewId, out var cached)) return cached;

            var view = doc.GetElement(viewId) as View;
            if (view == null) return null;

            string filePath = Path.Combine(_tempFolder, "v_" + viewId + ".png");

            var options = new ImageExportOptions
            {
                ZoomType = ZoomFitType.FitToPage,
                PixelSize = sizePx,
                ImageResolution = ImageResolution.DPI_72,
                FitDirection = FitDirectionType.Horizontal,
                ExportRange = ExportRange.SetOfViews,
                HLRandWFViewsFileType = ImageFileType.PNG,
                ShadowViewsFileType = ImageFileType.PNG,
                FilePath = filePath
            };
            options.SetViewsAndSheets(new List<ElementId> { viewId });

            try
            {
                doc.ExportImage(options);
            }
            catch
            {
                return null;
            }

            // Revit tự thêm hậu tố (tên view) vào tên file → tìm file thực sự sinh ra.
            var dir = Path.GetDirectoryName(filePath) ?? _tempFolder;
            var stem = Path.GetFileNameWithoutExtension(filePath);
            string actualFile = null;
            foreach (var f in Directory.GetFiles(dir, stem + "*.png"))
            {
                actualFile = f;
                break;
            }
            if (actualFile == null || !File.Exists(actualFile)) return null;

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(actualFile, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                _cache[viewId] = bmp;
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public void Clear()
        {
            _cache.Clear();
            try
            {
                if (Directory.Exists(_tempFolder))
                {
                    foreach (var f in Directory.GetFiles(_tempFolder)) { try { File.Delete(f); } catch { } }
                }
            }
            catch { }
        }
    }
}
