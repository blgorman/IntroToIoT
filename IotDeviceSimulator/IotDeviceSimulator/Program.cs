using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Text;

namespace IotDeviceSimulator
{
    //Note: the code in this simulator is based on the code found here:
    //https://github.com/MicrosoftLearning/AZ-220-Microsoft-Azure-IoT-Developer/tree/master/Allfiles/Labs/07-Device%20Message%20Routing/Starter/VibrationDevice

    public class Program
    {
        private static IConfigurationRoot _configuration;
        
        private const int _telemetryIntervalMilliseconds = 2000;
        private static DeviceClient _deviceClient;
        private static string _deviceConnectionString = "";

        public static async Task Main(string[] args)
        {
            BuildOptions();
            Console.WriteLine("Hello World");

            _deviceConnectionString = _configuration["Device:ConnectionString"];

            //https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-protocols
            //MQTT for single devices, AMQP for connection multiplexing
            //HTTPS for non web-socket connections
            _deviceClient = DeviceClient.CreateFromConnectionString(
                    _deviceConnectionString,
                    TransportType.Mqtt);

            //start the processing
            SendDeviceToCloudMessagesAsync();
            Console.ReadLine();
        }

        private static async void SendDeviceToCloudMessagesAsync()
        {
            // The ConveyorBeltSimulator class is used to create a
            // ConveyorBeltSimulator instance named `conveyor`. The `conveyor`
            // object is first used to capture a vibration reading which is
            // placed into a local `vibration` variable, and is then passed to
            // the two create message methods along with the `vibration` value
            // that was captured at the start of the interval.
            var conveyor = new ConveyorBeltSimulator(_telemetryIntervalMilliseconds);

            // Simulate the vibration telemetry of a conveyor belt.
            while (true)
            {
                var vibration = conveyor.ReadVibration();

                await CreateTelemetryMessage(conveyor, vibration);

                await CreateLoggingMessage(conveyor, vibration);

                await Task.Delay(_telemetryIntervalMilliseconds);
            }
        }

        // This method creates a JSON message string and uses the Message
        // class to send the message, along with additional properties. Notice
        // the sensorID property - this will be used to route the VSTel values
        // appropriately at the IoT Hub. Also notice the beltAlert property -
        // this is set to true if the conveyor belt haas stopped for more than 5
        // seconds.
        private static async Task CreateTelemetryMessage(
            ConveyorBeltSimulator conveyor,
            double vibration)
        {
            var telemetryDataPoint = new
            {
                vibration = vibration,
            };
            var telemetryMessageString =
                JsonConvert.SerializeObject(telemetryDataPoint);
            var telemetryMessage =
                new Message(Encoding.ASCII.GetBytes(telemetryMessageString));

            // Add a custom application property to the message. This is used to route the message.
            telemetryMessage.Properties.Add("sensorID", "VSTel");

            // Send an alert if the belt has been stopped for more than five seconds.
            telemetryMessage.Properties
                .Add("beltAlert", (conveyor.BeltStoppedSeconds > 5) ? "true" : "false");

            Console.WriteLine($"Telemetry data: {telemetryMessageString}");

            // Send the telemetry message.
            await _deviceClient.SendEventAsync(telemetryMessage);
            ConsoleHelper.WriteGreenMessage($"Telemetry sent {DateTime.Now.ToShortTimeString()}");
        }

        private static void BuildOptions()
        {
            _configuration = ConfigurationBuilderSingleton.ConfigurationRoot;
        }

        // This method is very similar to the CreateTelemetryMessage method.
        // Here are the key items to note:
        // * The loggingDataPoint contains more information than the telemetry
        //   object. It is common to include as much information as possible for
        //   logging purposes to assist in any fault diagnosis activities or
        //   more detailed analytics in the future.
        // * The logging message includes the sensorID property, this time set
        //   to VSLog. Again, as noted above, his will be used to route the
        //   VSLog values appropriately at the IoT Hub.
        private static async Task CreateLoggingMessage(
            ConveyorBeltSimulator conveyor,
            double vibration)
        {
            // Create the logging JSON message.
            var loggingDataPoint = new
            {
                vibration = Math.Round(vibration, 2),
                packages = conveyor.PackageCount,
                speed = conveyor.BeltSpeed.ToString(),
                temp = Math.Round(conveyor.Temperature, 2),
            };
            var loggingMessageString = JsonConvert.SerializeObject(loggingDataPoint);
            var loggingMessage = new Message(Encoding.ASCII.GetBytes(loggingMessageString));

            // Add a custom application property to the message. This is used to route the message.
            loggingMessage.Properties.Add("sensorID", "VSLog");

            // Send an alert if the belt has been stopped for more than five seconds.
            loggingMessage.Properties.Add("beltAlert", (conveyor.BeltStoppedSeconds > 5) ? "true" : "false");

            Console.WriteLine($"Log data: {loggingMessageString}");

            // Send the logging message.
            await _deviceClient.SendEventAsync(loggingMessage);
            ConsoleHelper.WriteGreenMessage("Log data sent\n");
        }
    }
}
