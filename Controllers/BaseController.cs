using Microsoft.AspNetCore.Mvc;

namespace DentalLab.Controllers
{
    public class BaseController : Controller
    {
        private readonly IWebHostEnvironment _host;

        public BaseController(IWebHostEnvironment host)
        {
            _host = host;
        }

        public string? UploadFile(string folder, IFormFile? file, string? fileUrl, string? isThereFile)
        {
            if (isThereFile == null)
            {
                DeleteOldFile(fileUrl);
                return null;
            }

            if (file != null)
            {
                DeleteOldFile(fileUrl);

                string folderPath = Path.Combine(_host.WebRootPath, "upload", folder);
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string fileName = Guid.NewGuid() + "_" + Path.GetFileName(file.FileName);
                string newImageUrl = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(newImageUrl, FileMode.Create))
                    file.CopyTo(stream);

                return Path.Combine(folder, fileName).Replace("\\", "/");
            }

            return fileUrl;
        }

        public void DeleteOldFile(string? fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
                return;

            string relativePath = fileUrl.Replace("/", Path.DirectorySeparatorChar.ToString());
            string fullPath = Path.Combine(_host.WebRootPath, "upload", relativePath);

            if (System.IO.File.Exists(fullPath))
            {
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    System.IO.File.Delete(fullPath);
                }
                catch
                {
                }
            }
        }

        public bool CheckImgExtension(IFormFile? img)
        {
            if (img == null)
                return true;

            string fileExtension = Path.GetExtension(img.FileName.ToLower());
            string[] validExtensions = [".jpeg", ".jpg", ".bmp", ".gif", ".png", ".tiff", ".ico"];
            return validExtensions.Contains(fileExtension);
        }
    }
}
