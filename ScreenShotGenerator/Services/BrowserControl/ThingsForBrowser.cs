using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
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

        /// <summary>
        /// Изменить размер изображения и сохранить его в файл.
        /// </summary>
        public static void reduceImage(byte[] screen,ImageSize sz, string filePathFull,ref UInt32 outSize)
        {

            using (var image = Image.Load(screen))
            {
                image.Mutate(x => x
                        //.AutoOrient() // this is the important thing that needed adding
                        .Resize(new ResizeOptions
                        {
                            Mode = ResizeMode.Stretch,
                            Position = AnchorPositionMode.Center,
                            Size = new SixLabors.ImageSharp.Size(sz.width, sz.height)
                        }));
                // .BackgroundColor(SixLabors.ImageSharp.Color.White));

                IImageEncoder imageEncoder = new JpegEncoder() { Quality = 85 };

                image.Save(filePathFull, imageEncoder);
                using (var ms = new MemoryStream())
                {
                    image.Save(ms, imageEncoder);
                    long len=ms.Length;//bytes; 
                    outSize = (UInt32)len / 1024;
                }
                 
            }


        }

    }
}