using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dogs.Breed.Data.Generator;
using Newtonsoft.Json;
using NSoup;
using NSoup.Nodes;

namespace ML_DataGenerator
{
    class Program
    {

        private static void DogBreedMetaData(int maxRecords)
        {
            var start = DateTime.Now;
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..");
            var relPath = Path.Combine(basePath, "DogBreeds");

            // Directory.Delete(relPath, true);

            if (!Directory.Exists(relPath))
                Directory.CreateDirectory(relPath);

            int attempt = 100;

            Document doc = null;
            while (--attempt > 0 && doc == null)
            {
                try
                {
                    doc = NSoupClient.Parse(new WebClient().DownloadString(new Uri("https://dogtime.com/dog-breeds/profiles")));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }


            var entries = doc.GetElementsByAttributeValue("class", "list-item");
            var allTasks = new List<Task>();

            int currentRecord = 0;

            int counter = 0;

            foreach (var entry in entries)
            {
                if (++currentRecord >= maxRecords)
                    break;
                var dataSource = entry.GetElementsByAttributeValue("class", "list-item-title");
                var name = dataSource[0].Text();
                var curPath = Path.Combine(relPath, name);
                var jsonPath = Path.Combine(curPath, "about.json");

                var imgName = $"Main.jpg";
                var imgPath = Path.Combine(curPath, imgName);

                if (Directory.Exists(curPath) && File.Exists(imgPath) && File.Exists(jsonPath))
                {
                    Console.WriteLine($"Skipped {name}");
                    continue;
                }

                allTasks.Add(Task.Run(() =>
                {
                    var dogProfileUrl = dataSource[0].Attr("href");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        if (!Directory.Exists(curPath))
                            Directory.CreateDirectory(curPath);
                        try
                        {
                            using (var client = new WebClient())
                            {
                                var count = Interlocked.Increment(ref counter);
                                Console.WriteLine($"{count} - Downloading image of {name}");
                                var imageUrl = entry.GetElementsByAttributeValue("class", "list-item-breed-img")[1]
                                    .Attributes["src"];
                                try
                                {
                                    client.DownloadFile(new Uri(imageUrl),
                                        imgPath);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                }

                                var dogInfo = HtmlDogOpjectParser.GetDogInfo(name, dogProfileUrl);
                                for (int i = 0; i < dogInfo.ImagesUrls.Count; ++i)
                                {

                                    var tempImagename = (Regex.Replace(name, @"\s+", "") + i.ToString() + ".jpg");
                                    try
                                    {
                                        client.DownloadFile(new Uri(dogInfo.ImagesUrls[i]), Path.Combine(curPath, tempImagename));
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);
                                    }
                                    Console.WriteLine($"{count} - Downloading image of {tempImagename}");

                                }
                                dogInfo.ImagesUrls.Add(imageUrl);
                                dogInfo.ImagesUrls.Sort();
                                var json = JsonConvert.SerializeObject(dogInfo, Formatting.Indented);
                                File.WriteAllText(jsonPath, json);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                ));

                if (currentRecord % 10 == 0)
                {
                    Console.WriteLine("\n\n\n\n\n\n\n\nWaiting for 5 tasks.\n\n\n\n\n\n\n\n");
                    Task.WaitAll(allTasks.ToArray());
                    allTasks.Clear();
                    GC.Collect();
                }
            }

            Task.WaitAll(allTasks.ToArray());
            Console.WriteLine($"Total time taken to parse {entries.Count} records is {(DateTime.Now - start).TotalSeconds}");
            Console.ReadLine();

        }



        private static void StartDownloadingDogsData()
        {
            int maxRecords = -1;

            while (maxRecords < 0)
            {
                Console.WriteLine("How many records? (0- Max)");

                int temp;
                if (int.TryParse(Console.ReadLine(), out temp))
                    maxRecords = temp;
            }

            DogBreedMetaData(maxRecords == 0 ? int.MaxValue : maxRecords);
        }

        static void Main(string[] args)
        {
            // var res=HtmlDogOpjectParser.GetDogInfo("Affenhuahua", "https://dogtime.com/dog-breeds/doxle");

            StartDownloadingDogsData();


            //RenameFiles();

            //CreateMetadata();

            //Analyse();

        }

        private static void Analyse()
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..");
            var relPath = Path.Combine(basePath, "DogBreeds");

            var dirs = Directory.GetDirectories(relPath);


            var metadata = dirs.Select(dir =>
            {
                var files = Directory.GetFiles(dir, "*.jpg");
                return new
                {
                    Name = Path.GetFileName(dir),
                    HasProfileInfo = File.Exists(Path.Combine(dir, "about.json")),
                    ImagesCount = files.Length,
                    Images = files.Select(file => Path.GetFileName(file))
                };
            });

            Console.WriteLine($"Total Breeds: {metadata.Count()}");
            Console.WriteLine($"With Profile: {metadata.Where(item => item.HasProfileInfo).Count()}");
            Console.WriteLine($"Total Images: {metadata.Sum(item => item.ImagesCount)}");
            Console.ReadLine();
        }

        private static void RenameFiles()
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..");
            var relPath = Path.Combine(basePath, "DogBreeds");

            var dirs = Directory.GetDirectories(relPath, "*");


            foreach (var dir in dirs)
            {
                var files = Directory.GetFiles(dir, "*.jpg");
                Console.WriteLine(string.Join("\n", files));
                foreach (var file in files)
                {
                    var fileName = Path.Combine(Path.GetDirectoryName(file), Regex.Replace(Path.GetFileName(file), @"\s+", ""));
                    Console.WriteLine($"\n old: {file}\n\nnew: {fileName}");
                    if (file != fileName)
                        File.Move(file, fileName);
                }
            }
        }

        private static void CreateMetadata()
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..");
            var relPath = Path.Combine(basePath, "DogBreeds");

            var dirs = Directory.GetDirectories(relPath);


            var metadata = dirs.Select(dir =>
            {
                var files = Directory.GetFiles(dir, "*.jpg");
                return new
                {
                    Name = Path.GetFileName(dir),
                    HasProfileInfo = File.Exists(Path.Combine(dir, "about.json")),
                    ImagesCount = files.Length,
                    Images = files.Select(file => Path.GetFileName(file))
                };
            });

            var json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            File.WriteAllText(Path.Combine(relPath, "metadata.json"), json);


        }
    }
}
