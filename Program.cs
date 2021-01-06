using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;

namespace MapsToTwinSyncer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var httpClient = new HttpClient();
            Console.WriteLine("Hello World!");
            // Start time
            var start = DateTime.Now;
            var datasetId = "8455dd7a-efaf-65ff-15da-3d96566369b4";
            var subkey = "--";
            
            //https://us.atlas.microsoft.com/wfs/datasets/8455dd7a-efaf-65ff-15da-3d96566369b4/collections/unit/items?api-version=1.0&subscription-key=---------------------
            var postfeatureUpdateUrlTemplate = $"https://us.atlas.microsoft.com/wfs/datasets/{datasetId}/collections/unit/items?api-version=1.0&subscription-key={subkey}";

            // retreive unit info
            var result = await ProcessFeatureCollection(httpClient, postfeatureUpdateUrlTemplate);
            // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 

            // foreach unit, find and update the corresponding  (include covering the next page)

            // Total duration
            var end = DateTime.Now - start;
        }


        private static async Task<string> ProcessFeatureCollection(HttpClient httpClient, string url)
        {
            var result = await httpClient.GetAsync(url);
            Console.WriteLine(result.StatusCode.ToString());
            if (!result.IsSuccessStatusCode)
            {
                Console.WriteLine("ERROR posting changes to Azure Maps");
            }
            else{
                Root col = JsonConvert.DeserializeObject<Root>(result.Content.ReadAsStringAsync().Result); 
                Console.WriteLine($"{col.type} {col.numberReturned.ToString()}");
            }

            return "";
        }
        
    }
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class Geometry    {
        public string type { get; set; } 
        public List<List<List<double>>> coordinates { get; set; } 
    }

    public class Properties    {
        public string originalId { get; set; } 
        public string categoryId { get; set; } 
        public bool isOpenArea { get; set; } 
        public bool isRoutable { get; set; } 
        public string routeThroughBehavior { get; set; } 
        public string levelId { get; set; } 
        public List<object> occupants { get; set; } 
        public string addressId { get; set; } 
        public string name { get; set; } 
    }

    public class Feature    {
        public string type { get; set; } 
        public Geometry geometry { get; set; } 
        public Properties properties { get; set; } 
        public string id { get; set; } 
        public string featureType { get; set; } 
    }

    public class Link    {
        public string href { get; set; } 
        public string rel { get; set; } 
    }

    public class Root    {
        public string type { get; set; } 
        public List<Feature> features { get; set; } 
        public int numberReturned { get; set; } 
        public List<Link> links { get; set; } 
    }


}
