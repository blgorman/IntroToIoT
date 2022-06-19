using Newtonsoft.Json;

namespace IotDeviceSimulator
{
    public class BME280PlusLTR559 : BME280
    {
        public string Lux {get;set;}
        public string Proximity {get;set;}
        public BME280PlusLTR559()
                    : base(){}

        public BME280PlusLTR559(string temp, string pressure, string humidity, string altitude, string lux, string prox)
                    : base(temp, pressure, humidity, altitude)
        {
            Lux = lux;
            Proximity = prox;
        }

        public override string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}