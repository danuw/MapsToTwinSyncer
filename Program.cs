using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Azure;
//using System.Text.Json;

using Microsoft.Identity.Client;
//using Microsoft.Rest;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

using Azure.DigitalTwins.Core.Serialization;
//using Azure.DigitalTwins.Core.Models;
using System.Runtime.InteropServices;

namespace MapsToTwinSyncer
{
    class Program
    {
        private static string adtInstanceUrl;
        private static string datasetId;
        private static string subkey;

        const string adtAppId = "https://digitaltwins.azure.net";

        
        private static DigitalTwinsClient adtclient;

        static async Task Main(string[] args)
        {
            var httpClient = new HttpClient();
            Console.WriteLine("Hello World!");
            // Start time
            var start = DateTime.Now;


            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                int width = Math.Min(Console.LargestWindowWidth, 150);
                int height = Math.Min(Console.LargestWindowHeight, 40);
                Console.SetWindowSize(width, height);
            }
            if(args!=null){
                Console.WriteLine("You sent some pre commands as arguments...");
            }
            try
            {
                // Read configuration data from the 
                IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("serviceConfig.json", true, true)
                    .Build();
                datasetId = config["datasetId"];
                subkey = config["subkey"];
                adtInstanceUrl = config["instanceUrl"];
                Log.Ok($"Will connect to {adtInstanceUrl}");
            } catch (Exception e)
            {
                Log.Error($"Could not read service configuration file serviceConfig.json");
                Log.Alert($"Please copy serviceConfig.json.TEMPLATE to serviceConfig.json");
                Log.Alert($"and edit to reflect your service connection settings");
                Environment.Exit(0);
            }
            
            var subquery = $"&subscription-key={subkey}";
            Log.Ok("Authenticating...");

            var credential = new DefaultAzureCredential();
            adtclient = new DigitalTwinsClient(new Uri(adtInstanceUrl), credential);
            Console.WriteLine($"Service client created – ready to go");

            //https://us.atlas.microsoft.com/wfs/datasets/8455dd7a-efaf-65ff-15da-3d96566369b4/collections/unit/items?api-version=1.0&subscription-key=---------------------
            var postfeatureUpdateUrlTemplate = $"https://us.atlas.microsoft.com/wfs/datasets/{datasetId}/collections/unit/items?api-version=1.0";

            // retreive unit info
            var result = await ProcessFeatureCollection(httpClient, adtclient, postfeatureUpdateUrlTemplate, subquery);

            // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 

            // foreach unit, find and update the corresponding  (include covering the next page)

            // Total duration
            var end = DateTime.Now - start;

            Console.WriteLine($"Processed {result.ToString()} units in {end.TotalSeconds}s");
            
        }


        private static async Task<int> ProcessFeatureCollection(HttpClient httpClient, DigitalTwinsClient adtclient, string url, string subquery)
        {
            int total= 0;
            var result = await httpClient.GetAsync(url+subquery);
            //Console.WriteLine(result.StatusCode.ToString());
            if (!result.IsSuccessStatusCode)
            {
                Console.WriteLine("ERROR posting changes to Azure Maps");
            }
            else{
                Root col = JsonConvert.DeserializeObject<Root>(result.Content.ReadAsStringAsync().Result); 
                //Console.WriteLine($"{col.type} {col.numberReturned.ToString()}");
                total = col.numberReturned;
                foreach (var feature in col.features)
                {
                    // Update Twin with the feature information

                    var firstPoint = feature.geometry.coordinates[0][0];// TODO - get the center point
                    // use first 
                    //Console.WriteLine($"{feature.properties.name} as {feature.id}, [{firstPoint[0]},{firstPoint[1]}] on {feature.properties.levelId}]");
                    var mapcenter = $"[{firstPoint[0]},{firstPoint[1]}]";
                    Console.WriteLine(mapcenter);
                    mapcenter = await GetMapCenterAsync(feature.geometry.coordinates[0]);
                    Console.WriteLine($"Room\t{feature.properties.name}\t{feature.properties.levelId}\tcontains\t\t{feature.id}\t{mapcenter}");

                    // Run a query for all twins   
                    string query = $"SELECT * FROM digitaltwins T WHERE T.$dtId = '{feature.properties.name}'";
                    AsyncPageable<BasicDigitalTwin> results = adtclient.QueryAsync<BasicDigitalTwin>(query);

                    await foreach (BasicDigitalTwin twin in results)
                    {
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(twin));
                        Console.WriteLine("---------------");
                        // Update   Update 
                        await UpdateTwinProperty(adtclient, feature.properties.name, "replace", "/MapCenter", mapcenter);
                    }
                    
                }
//return total;// TODO enable all results
                // continue if there is a next link
                foreach (var link in col.links)
                {
                    if(link.rel == "next")
                    {
                        var next = link.href;
                        //Console.WriteLine(next);
                        if(!string.IsNullOrWhiteSpace(next))
                        {
                            total += await ProcessFeatureCollection(httpClient, adtclient, next.Replace("https://atlas.microsoft.com", "https://us.atlas.microsoft.com"), subquery);// making sure we connect to the US server
                        }
                    }
                }
            }

            return total;
        }

        public static async Task UpdateTwinProperty(DigitalTwinsClient client, string twinId, string operation, string propertyPath, string val)
        {
            // Update twin property
            try
            {
                var updateTwinData = new JsonPatchDocument();
                updateTwinData.AppendReplace(propertyPath, val);
                await client.UpdateDigitalTwinAsync(twinId, updateTwinData);
            }
            catch (RequestFailedException exc)
            {
                Log.Error($"*** Error:{exc.Status}/{exc.Message}");
            }
        }
        public static async Task<string> GetMapCenterAsync(List<List<double>> points)
        {
            // Update twin property
            if (points.Count <2) throw new Exception("should have more corners");
            return GetCentralGeoCoordinate(points); // adapted from https://stackoverflow.com/questions/28315027/calculation-of-center-point-from-list-of-latitude-and-longitude-are-slightly-dif
        }
        public static string GetCentralGeoCoordinate(List<List<double>>  geoCoordinates)
        {
            double x = 0, y = 0, z = 0;
            foreach (var geoCoordinate in geoCoordinates)
            {
                var latitude = geoCoordinate[0] * Math.PI / 180;
                var longitude = geoCoordinate[1] * Math.PI / 180;

                x += Math.Cos(latitude) * Math.Cos(longitude);
                y += Math.Cos(latitude) * Math.Sin(longitude);
                z += Math.Sin(latitude);
            }
            var total = geoCoordinates.Count;
            x = x / total;
            y = y / total;
            z = z / total;
            var centralLongitude = Math.Atan2(y, x);
            var centralSquareRoot = Math.Sqrt(x * x + y * y);
            var centralLatitude = Math.Atan2(z, centralSquareRoot);
            var res = $"[{centralLatitude * 180 / Math.PI},{centralLongitude * 180 / Math.PI}]";
            Console.WriteLine(res);
            return res;
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
