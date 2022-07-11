using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System;
using System.Device.I2c;
using System.Threading;
using Iot.Device.Bmxx80;
using Iot.Device.Bmxx80.PowerMode;
using System.Diagnostics;

namespace IotDeviceSimulator
{
    //Note: the code in this simulator is based on the code found here:
    //https://github.com/MicrosoftLearning/AZ-220-Microsoft-Azure-IoT-Developer/tree/master/Allfiles/Labs/07-Device%20Message%20Routing/Starter/VibrationDevice

    public class Program
    {
        private static IConfigurationRoot _configuration;
        
        private static int _telemetryIntervalMilliseconds = 2000;
        private static DeviceClient _deviceClient;
        private static string _deviceConnectionString = "";
        private static string _dpsIdScope = "";
        private static string _certificateFileName = "";
        private static string _certificatePassword = "";
        private static int _telemetryDelay = 1;
        private static int _telemetryReadForSeconds = 30;

        //NOTE: This is always the endpoint for devices in Azure IoT with DPS provisioning
        private const string GlobalDeviceEndpoint = "global.azure-devices-provisioning.net";

        public static async Task Main(string[] args)
        {
            BuildOptions();
            Console.WriteLine("Hello World");

            Console.WriteLine("How would you like to connect [1: Con Str, 2: Certificates, 3: Enviro Sensor]?");

            int userChoice;
            var success = int.TryParse(Console.ReadLine(), out userChoice);
            while (!success || userChoice < 1 || userChoice > 3)
            {
                Console.WriteLine("Bad input");
                Console.WriteLine("How would you like to connect [1: Con Str, 2: Certificates, 3: Enviro Sensor]?");
                success = int.TryParse(Console.ReadLine(), out userChoice);
            }

            switch (userChoice)
            {
                case 1:
                    UseConnectionStringDeviceClient();
                    break;
                case 2:
                    await UseCertificateDeviceClient();
                    break;
		        case 3:
                    await UseEnviroBoardOnPi();
                    break;
                default:
                    UseConnectionStringDeviceClient();
                    break;
            }

            Console.WriteLine("Program completed, press enter to end at any time");
            Console.ReadLine();
        }

        private static void UseConnectionStringDeviceClient()
        {
            Console.WriteLine("Using Connection string to write telemetry to the hub");
            _deviceConnectionString = _configuration["Device:ConnectionString"];

            //https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-protocols
            //MQTT for single devices, AMQP for connection multiplexing
            //HTTPS for non web-socket connections
            _deviceClient = DeviceClient.CreateFromConnectionString(
                    _deviceConnectionString,
                    TransportType.Mqtt);

            //start the processing
            SendDeviceToCloudMessagesAsync();
        }

        private static async Task UseEnviroBoardOnPi()
        {
            Console.WriteLine("Would you like to show each telemetry reading [y/n]?");
            var shouldShowIndivudualTelemetry = Console.ReadLine()?.StartsWith("y", StringComparison.OrdinalIgnoreCase) ?? false;
        
            _deviceConnectionString = _configuration["Device:ConnectionString"];

            _deviceClient = DeviceClient.CreateFromConnectionString(
                    _deviceConnectionString,
                    TransportType.Mqtt);

            var endReadingsAtTime = DateTime.Now.AddSeconds(_telemetryReadForSeconds);

            var i2cSettings = new I2cConnectionSettings(1, Bme280.SecondaryI2cAddress);
            using I2cDevice i2cDevice = I2cDevice.Create(i2cSettings);
            using var bme280 = new Bme280(i2cDevice);

            int measurementTime = bme280.GetMeasurementDuration();
            var command = "python";
            var script = @"/home/pi/enviro/enviroplus-python/examples/singlelight.py"; //note: make sure path is valid
            var args = $"{script}"; 

            while(DateTime.Now < endReadingsAtTime)
            {
                bme280.SetPowerMode(Bmx280PowerMode.Forced);
                Thread.Sleep(measurementTime);

                bme280.TryReadTemperature(out var tempValue);
                bme280.TryReadPressure(out var preValue);
                bme280.TryReadHumidity(out var humValue);
                bme280.TryReadAltitude(out var altValue);

                var temp = $"{tempValue.DegreesCelsius:0.#}\u00B0C";
                var humidity = $"{humValue.Percent:#.##}%";
                var pressure = $"{preValue.Hectopascals:#.##} hPa";
                var altitude = $"{altValue.Meters:#} m";

                string lightProx = string.Empty;

                using (Process process = new Process())
                {

                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName = command;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();

                    StreamReader sr = process.StandardOutput;
                    lightProx = sr.ReadToEnd();
                    process.WaitForExit();
                }

                var result = lightProx.Split('\'');
                var lux = result[3];
                var prox = result[7];

                if(shouldShowIndivudualTelemetry)
                {
                    Console.WriteLine($"Temperature: {temp}");
                    Console.WriteLine($"Pressure: {pressure}");
                    Console.WriteLine($"Relative humidity: {humidity}");
                    Console.WriteLine($"Estimated altitude: {altitude}");
                    Console.WriteLine($"Light: {lux} lux");
                    Console.WriteLine($"Proximity: {prox}");
                }

                var telemetryObject = new BME280PlusLTR559(temp, pressure, humidity, altitude, lux, prox);
                var telemetryMessage = telemetryObject.ToJson();
                var msg = new Message(Encoding.ASCII.GetBytes(telemetryMessage));
                //msg.properties.Add("DeviceId", "enviro1000");
                //msg.properties.Add("TempAlert", tempValue.DegreesCelsius > 25);
                await _deviceClient.SendEventAsync(msg);
                ConsoleHelper.WriteGreenMessage($"Telemetry sent {DateTime.Now.ToShortTimeString()}");

                Thread.Sleep(500);
            }

            

            Console.WriteLine("All telemetry read");
            
        }

        private static async Task UseCertificateDeviceClient()
        {
            Console.WriteLine("Using certificate attestation via DPS enrollment group to write telemetry to the hub");
            
            //cert requires the cert file generated at Azure and the DPSIdScope on which the cert is registered
            _dpsIdScope = _configuration["Device:DPSIdScope"];
            var certificateFileName2500 = _configuration["Device:CertificateFileName2500"];
            var certificateFileName2600 = _configuration["Device:CertificateFileName2600"];
            var certificateFileName2700 = _configuration["Device:CertificateFileName2700"];
            _certificatePassword = _configuration["Device:CertificatePassword"];
            _certificateFileName = certificateFileName2500;

            //get the device to simulate
            Console.WriteLine("Which device are you simulating [1 -> 2500, 2 -> 2600, 3 -> 2700]?");
            int userChoice;
            var success = int.TryParse(Console.ReadLine(), out userChoice);
            while (!success || userChoice < 1 || userChoice > 3)
            {
                Console.WriteLine("Bad input");
                Console.WriteLine("Which device are you simulating [1 -> 2500, 2 -> 2600, 3 -> 2700]?");
                success = int.TryParse(Console.ReadLine(), out userChoice);
            }
            switch (userChoice)
            {
                case 1:
                    _certificateFileName = certificateFileName2500;
                    break;
                case 2:
                    _certificateFileName = certificateFileName2600;
                    break;
                case 3:
                    _certificateFileName = certificateFileName2700;
                    break;
                default:
                    _certificateFileName = certificateFileName2500;
                    break;
            }

            X509Certificate2 certificate = LoadProvisioningCertificate();
            using (var security = new SecurityProviderX509Certificate(certificate))
            {
                using (var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly))
                {
                    ProvisioningDeviceClient provClient =
                        ProvisioningDeviceClient.Create(GlobalDeviceEndpoint, _dpsIdScope, security, transport);

                    using (_deviceClient = await ProvisionDevice(provClient, security))
                    {
                        await _deviceClient.OpenAsync().ConfigureAwait(false);

                        // Setup device twin callbacks
                        await _deviceClient
                            .SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null)
                            .ConfigureAwait(false);

                        var twin = await _deviceClient.GetTwinAsync().ConfigureAwait(false);
                        await OnDesiredPropertyChanged(twin.Properties.Desired, null);

                        // Start reading and sending device telemetry
                        Console.WriteLine("Start reading and sending device telemetry...");
                        await SendDeviceToCloudMessagesAsync();

                        await _deviceClient.CloseAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        private static X509Certificate2 LoadProvisioningCertificate()
        {
            var certificateCollection = new X509Certificate2Collection();
            certificateCollection.Import(_certificateFileName,
                                         _certificatePassword,
                                         X509KeyStorageFlags.UserKeySet);
            X509Certificate2 certificate = null;
            foreach (X509Certificate2 element in certificateCollection)
            {
                Console.WriteLine($"Found certificate: {element?.Thumbprint} {element?.Subject}; PrivateKey: {element?.HasPrivateKey}");
                if (certificate == null && element.HasPrivateKey)
                {
                    certificate = element;
                }
                else
                {
                    element.Dispose();
                }
            }

            if (certificate == null)
            {
                throw new FileNotFoundException($"{_certificateFileName} did not contain any certificate with a private key.");
            }

            Console.WriteLine($"Using certificate {certificate.Thumbprint} {certificate.Subject}");
            return certificate;
        }

        private static async Task<DeviceClient> ProvisionDevice(
            ProvisioningDeviceClient provisioningDeviceClient,
            SecurityProviderX509Certificate security)
        {
            var result = await provisioningDeviceClient
                .RegisterAsync()
                .ConfigureAwait(false);
            Console.WriteLine($"ProvisioningClient AssignedHub: {result.AssignedHub}; DeviceID: {result.DeviceId}");
            if (result.Status != ProvisioningRegistrationStatusType.Assigned)
            {
                throw new Exception($"DeviceRegistrationResult.Status is NOT 'Assigned'");
            }

            var auth = new DeviceAuthenticationWithX509Certificate(
                result.DeviceId,
                security.GetAuthenticationCertificate());

            return DeviceClient.Create(result.AssignedHub, auth, TransportType.Amqp);
        }

        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine("Desired Twin Property Changed:");
            Console.WriteLine($"{desiredProperties.ToJson()}");

            // Read the desired Twin Properties
            if (desiredProperties.Contains("telemetryDelay"))
            {
                string desiredTelemetryDelay = desiredProperties["telemetryDelay"];
                if (desiredTelemetryDelay != null)
                {
                    _telemetryDelay = int.Parse(desiredTelemetryDelay);
                    _telemetryIntervalMilliseconds = _telemetryDelay * 1000;
                }
                // if desired telemetryDelay is null or unspecified, don't change it
            }


            // Report Twin Properties
            var reportedProperties = new TwinCollection();
            reportedProperties["telemetryDelay"] = _telemetryDelay.ToString();
            await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
            Console.WriteLine("Reported Twin Properties:");
            Console.WriteLine($"{reportedProperties.ToJson()}");
        }

        private static async Task SendDeviceToCloudMessagesAsync()
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
