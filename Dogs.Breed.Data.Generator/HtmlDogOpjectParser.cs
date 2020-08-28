using NSoup;
using System;
using System.Collections.Generic;
using System.Linq;
using NSoup.Nodes;
using System.Text.RegularExpressions;
using System.Net;

namespace Dogs.Breed.Data.Generator
{
    public static class HtmlDogOpjectParser
    {
        private static string GetDogProfileUrl(string name) => $"https://dogtime.com/dog-breeds/{name}";
        public static Dog GetDogInfo(string name, string url)
        {
            Dog dog = new Dog();
            dog.Name = name;
            dog.ProfileUrl = url;

            Console.WriteLine($"HtmlDogOpjectParser: Parsing {url}");

            Document doc = null;
            int attempt = 0;
            while (doc == null && attempt++ < 100)
                try
                {
                    doc = NSoupClient.Parse(new WebClient().DownloadString(new Uri(url)));

                }
                catch (Exception e)
                {
                    Console.WriteLine($"NSoupClient.Connect failed for {url} \nException: {e}");
                }

            if (doc != null)
            {
                try
                {
                    ParseDescriptionAndUrl(ref doc, ref dog);
                }
                catch (Exception e)
                {
                    Console.WriteLine("ParseDescriptionAndUrl: " + e);
                }

                try
                {
                    ParseCharacteristicsList(ref doc, ref dog);
                }
                catch (Exception e)
                {
                    Console.WriteLine("ParseCharacteristicsList: " + e);
                }

                try
                {
                    ParseVitalStats(ref doc, ref dog);
                }
                catch (Exception e)
                {
                    Console.WriteLine("ParseVitalStats: " + e);
                }

                try
                {
                    ParseMoreAbout(ref doc, ref dog);
                }
                catch (Exception e)
                {
                    Console.WriteLine("ParseMoreAbout: " + e);
                }
            }

            return dog;
        }

        private static void ParseDescriptionAndUrl(ref Document doc, ref Dog dog)
        {
            var tempNodes = doc.GetElementsByAttributeValue("class", "breeds-single-intro")[0];
            var description = tempNodes.GetElementsByTag("p").Select(
                entry =>
                {
                    var text = entry.Text();
                    return text;
                }).Where(s => { return !(s.StartsWith("See below") || s.StartsWith("(Picture")); }).ToList();
            dog.Description = description;
            dog.ImagesUrls = new List<string>();
            List<string> imgs = null;

            string href = "";


            try
            {
                href = doc.GetElementsByAttributeValue("class",
                                        "pbslideshow-fullscreen js-fullscreen-button slideshow-begin-button pbslideshow-inline-cta")[0]
                                    .Attributes["href"];

                Document refDoc = null;
                int attempt = 0;
                while (refDoc == null && attempt++ < 100)
                    try
                    {
                        refDoc = NSoupClient.Parse(new WebClient().DownloadString(new Uri(href)));

                        imgs = refDoc.GetElementsByAttributeValue("class", "pbslideshow-slider-item")
                            .Select(item => item.GetElementsByTag("img"))
                            .Select(item =>
                            {
                                var it = item.FirstOrDefault(x => x.Attributes.ContainsKey("src"));
                                return it == null ? "" : it.Attributes["src"];
                            }).Where(it => !string.IsNullOrWhiteSpace(it)).ToList();

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"NSoupClient.Connect failed for {refDoc} \nException: {e}");
                    }
            }
            catch
            {
                try
                {

                    imgs = doc.GetElementsByAttributeValue("class", "wp-caption alignnone")
                        .Select(item => item.GetElementsByTag("img"))
                        .Select(item1 =>
                        {
                            var it = item1.FirstOrDefault(item2 => item2.Attributes.ContainsKey("src"));
                            return it == null ? "" : it.Attributes["src"];
                        }).Where(item3 => !string.IsNullOrWhiteSpace(item3)).ToList();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

            }

            dog.ImagesUrls = imgs?.Distinct().ToList() ?? new List<string>();
            dog.ImagesUrls.Sort();
        }

        public static bool ValidateUrl(string URL)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
            request.Timeout = 300;
            request.Method = "GET";
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (WebException)
            {
                return false;
            }
        }

        private static void ParseCharacteristicsList(ref Document doc, ref Dog dog)
        {
            var breedCharacteristics = doc.GetElementsByAttributeValue("class", "breeds-single-details")[0].GetElementsByAttributeValue("class", "breed-characteristics-ratings-wrapper paws").Select(
                entry =>
                {
                    var bc = new BreedCharacteristics();
                    var title = entry.GetElementsByAttributeValue("class", "characteristic-stars parent-characteristic");
                    bc.Title = title[0].Children[0].Text();
                    bc.Rating = ToInt(title[0].GetElementsByAttributeValue("class", "characteristic-star-block")[0].Children[0].Attr("class"));
                    bc.Survey = entry.GetElementsByAttributeValue("class", "js-list-item child-characteristic").Select(

                        item =>
                        {
                            return new
                            {
                                Key = item.GetElementsByAttributeValue("class", "characteristic-title").Text,
                                Value = ToInt(item.GetElementsByAttributeValue("class", "characteristic-star-block")[0].Children[0].Attr("class"))
                            };
                        }).ToDictionary(item => item.Key, item => item.Value);
                    return bc;
                }).ToList();
            dog.BreedCharacteristics = breedCharacteristics;
        }

        private static int ToInt(string val)
        {
            int res;
            int.TryParse(Regex.Match(val, @"\d+").Value, out res);
            return res;
        }


        private static void ParseVitalStats(ref Document doc, ref Dog dog)
        {
            var breedCharacteristics = doc.GetElementsByAttributeValue("class", "vital-stat-box").Select(
                entry =>
                {

                    var text = entry.Text().Split(':');
                    return new VitalStats
                    {
                        Title = (text?.Length == 2) ? text[0] : "",
                        Value = (text?.Length == 2) ? text[1] : ""
                    };
                }

                ).ToList();

            dog.VitalStats = breedCharacteristics;

        }

        private static void ParseMoreAbout(ref Document doc, ref Dog dog)
        {
            var moreAbout = doc.GetElementsByAttributeValue("class", "breed-data-item js-accordion-item item-expandable-content").Select(
                item =>
                {
                    var res = new MoreAbout();

                    res.Title = item.GetElementsByAttributeValue("class", "js-section-heading description-title").Text;

                    var liTags = item.GetElementsByTag("li").Select(
                        l => l.Text()).ToList();

                    var pTags = item.GetElementsByTag("p").Select(
                        p => p.Text()).ToList();

                    res.Information = liTags.Concat(pTags).ToList();


                    return res;
                }).ToList();
            dog.MoreAbout = moreAbout;
        }

    }
}
