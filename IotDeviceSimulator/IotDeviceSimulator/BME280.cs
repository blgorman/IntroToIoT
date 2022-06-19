using Newtonsoft.Json;

namespace IotDeviceSimulator
{
    public class BME280
    {
        public string TemperatureCelsius {get;set;}
        public string PressureHectoPascals {get;set;}
        public string RelativeHumidityPercent {get;set;}
        public string EstimatedAltitudeMeters {get;set;}

        public BME280(){}

        public BME280(string temp, string pressure, string humidity, string altitude)
        {
            TemperatureCelsius = temp;
            PressureHectoPascals = pressure;
            RelativeHumidityPercent = humidity;
            EstimatedAltitudeMeters = altitude;
        }

        public virtual string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}