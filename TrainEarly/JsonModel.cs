using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace TrainEarly
{
    public class Delays
    {
        private static readonly string FileName = "stats.json";
        public DateTime Created { get; private set; }
        public ICollection<int> EarlyDepartures { get; set; }

        public static Delays NewInstance()
        {
            return new Delays { EarlyDepartures = new List<int>() };
        }

        public void Save()
        {
            File.WriteAllText(FileName, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static Delays Load()
        {
            if (File.Exists(FileName))
            {
                return JsonConvert.DeserializeObject<Delays>(File.ReadAllText(FileName));
            }
            else
            {
                Delays delays = NewInstance();
                delays.Save();
                return delays;
            }
        }

        private Delays()
        {
            Created = DateTime.UtcNow;
        }
    }
}
