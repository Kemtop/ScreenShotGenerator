using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Models
{
    public class CashImagesModel
    {
        /// <summary>
        /// Поле поиска.
        /// </summary>
        public string searchUrl { get; set; }

        /// <summary>
        /// Список файлов.
        /// </summary>
        public List<mImageList> Files { get; set; }

    }
}
