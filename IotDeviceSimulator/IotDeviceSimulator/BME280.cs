using Newtonsoft.Json;

namespace IotDeviceSimulator
{
    //Note: the code in this simulator is based on the code found here:
    //https://github.com/MicrosoftLearning/AZ-220-Microsoft-Azure-IoT-Developer/tree/master/Allfiles/Labs/07-Device%20Message%20Routing/Starter/VibrationDevice

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

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}