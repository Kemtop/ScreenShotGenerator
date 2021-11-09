using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services.BrowserControl
{
    /// <summary>
    /// Вспомогательные методы для реализаций браузеров.
    /// </summary>
    public class ThingsForBrowser
    {
        /// <summary>
        /// Читает конфигрурацию браузера.
        /// </summary>
        /// <param name="browserName"></param>
        /// <returns></returns>
        public static Dictionary<string, object> readConfigBrowser(string browserName)
        {
            //Получаю конфигурацию.
            IConfigurationRoot config_ = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                 .Build();

            List<IConfigurationSection> lines = config_.GetSection(browserName)
                    .GetChildren().ToList();

            Dictionary<string, object> Dic = new Dictionary<string, object>();
            foreach (IConfigurationSection s in lines)
            {
                Dic[s.Key] = s.Value;
            }

            return Dic;
        }

        /// <summary>
        /// Обрезает и сохраняет картинку.
        /// </summary>
        /// <param name="screen"></param>
        /// <param name="filePathFull"></param>
        public static void cutAndSave(byte[] screen, string filePathFull)
        {
            //Обрезка.
            using (var stream = new MemoryStream())
            {

                using var image = Image.Load(screen);
                image.Mutate(x => x
                    //.AutoOrient() // this is the important thing that needed adding
                    .Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Crop,
                        Position = AnchorPositionMode.Center,
                        Size = new SixLabors.ImageSharp.Size(1260, 965)
                    })
                    .BackgroundColor(SixLabors.ImageSharp.Color.White));

                image.Save(filePathFull, new JpegEncoder() { Quality = 85 });


            }
        }
    }
}