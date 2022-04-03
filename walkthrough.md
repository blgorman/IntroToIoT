# Introduction to IoT

This walkthrough can be given as a presentation or used to learn the basics of IoT

## Getting Started, the IoT Hub

The IoT Hub is the entry point into Azure for all of your IoT Data.  The Hub is the central point of connection, where either individual or gateway devices can authenticate and connect to the hub.

### Creating a hub 

You can create an IoT hub instance via any of the typical approaches.  In the next steps, an IoT Hub will be created using the Azure CLI.  You can do this from your local desktop or from the cloud shell.

```bash
rg=az-iot-demo-rg
loc=centralus
hubName=my-iot-hub-20221231xyz
sku=F1
pCount=2
az group create -g $rg -l $loc
az iot hub create --name $hubName --resource-group $rg --sku $sku --partition-count $pCount
```  

>**Note:** You can have 1 free IoT Hub per subscription.  After that you must pay.  Free hubs have all the power of  standard hubs, but they have a throttled throughput.  The default value if not specified is `S1` for the standard hub.

There are three tier levels: 
Free, Basic, and Standard.

Free is just like Standard but throttled.  Basic is limited to one-way communication [no cloud to device messages].

Provisioning a hub can take a few minutes, so be patient while it deploys.  

>**Note:** Using IoTHub requires the Resource Provider of `Microsoft.Devices`.  If this resource provider is not registered, then running from the CLI will automatically register the device.

Partition count is a required parameter when creating a free hub. The standard hub can have up to 32 partitions, although documentation will tell you that 4 is generally enough, and 4 seems to be the default.  If you try to use 4 partitions in a free tier, you'll get an error.

Query to see the hub:

```bash
az iot hub list --query "[?contains(name, '${hubName}')]" -o tsv
```  

### Examining the hub

In the portal, browse to the hub and take a look around.

Notice that there are two device blades - `Devices` and `IoT Edge`

Note that the registration of devices for each will be similar, but there is a flag that toggles the device as an edge device.

### Resources

- [Creaete an IoT hub using the Azure CLI](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-create-using-cli)

## Connecting Devices to the Hub

There are a number of ways to register devices and then connect them to your hub.  In this next part, we'll get a device set for connection.  By the end, we'll be able to simulate the device and then send telemetry to our hub for processing.

### Two Ways To Authenticate Devices

There are two ways that your devices can authenticate

1. Keys
2. Certificates

If you are using an edge gateway, all devices that connect to the gateway must also still authenticate back to the Azure Hub, even if they are only sending telemetry to an edge device.

### Connect to the hub

You can use multiple approaches to connect a device to a hub

- The portal
- Scripts such as the AZ CLI from powershell/bash
- Using a Device provisioning Service

#### Add a device from the portal

To use the portal to add a device, navigate to the `Devices` blade under device management.  

1. Create a new device using the `+ Add Device` button
1. Name the device something like `device-1000` 
1. For authentication, choose `Symmetric Key`
1. Leave the box checked for `Auto-generate` keys
1. Leave the `Connect this device to an IoT hub` enabled
1. Do not select any parent device
   
Navigate into the device and then get the Primary Key and Connection string information.

#### Add a device from the CLI

With the variables from above, execute the following commands to add a device to the IoT Hub from the azure cli.

```bash
deviceId=device-2000
az iot hub device-identity create --hub-name $hubName --device-id $deviceId
```  

Query to see the device:

```bash
az iot hub device-identity list  --hub-name $hubName --query "[?contains(deviceId, '${deviceId}')]" -o tsv
```  

>**Note**: Adding a device registration is a necessary first step to get telemetry from a device, however the device must also connect to the hub.  Therefore, remember that registration != connection.  Also note that once a device is registered, if you need to prevent the device from connecting you must ensure that the device is no longer registered.

## Create a simulator and send telemetry to the hub

In the next part, you'll use C# to create a simulator and connect to the IoT Hub via the SDK.  If you want to skip creation of the project, [just use the code from the official repo](https://github.com/blgorman/AZ-220-Microsoft-Azure-IoT-Developer/tree/master/Allfiles/Labs/07-Device%20Message%20Routing/Starter/VibrationDevice);

>**Note**: My project leverages the `appsettings.json` file and ultimately the developer secrets, which is divergent from the sample application found [here](https://github.com/blgorman/AZ-220-Microsoft-Azure-IoT-Developer/tree/master/Allfiles/Labs/07-Device%20Message%20Routing/Starter/VibrationDevice).  If you aren't sure how to set that up but want to build from scratch, review my project for the ConfigurationBuilder and the creation of it in the main Program.

## Create the project

Begin by creating a new console project.  The steps to get started follow. This will guide you to the solution.  

### Additional NuGet Packages

Note the following NuGet packages exist in the solution:

1. Microsoft.Azure.Devices.Client

### Wire up the connection for the device to the hub

It is important to note that the device ID will map to a preregistered device.  If the device is not pre-registered and you don't have a connection string to connect to, then you won't be able to connect and simulate the telemetry.

1. Begin by setting up the device simulator to connect.

    Use the following code to create global variables:

    ```c#
    private const int _telemetryIntervalMilliseconds = 2000;
    private static DeviceClient _deviceClient;
    private static string _deviceConnectionString = "";
    ```  

1. Next, add the connection string information to your `appsettings.json` file, or just set it directly in the code.

    ```c#  
    _deviceConnectionString = _configuration["Device:ConnectionString"];
    ```  

    After adding the device connection string, add the code to connect to the IoT hub:

    ```c#
    _deviceClient = DeviceClient.CreateFromConnectionString(
                    _deviceConnectionString,
                    TransportType.Mqtt);
    ```  

    Note that there are many choices for transport.  The choices include  `MQTT` and `AMQP`, both of which are binary transport protocols.  `MQTT` works fine in this scenario as each device would connect to the hub individually.  If connection multiplexing is needed, then use `AMQP`.  `HTTPS` could also be used but it needs to reconnect on every transmission, where `AMQP` and `MQTT` can establish the connection once and reuse it.


## Create telemetry

The process to create telemetry is to build some sort of data generator that sends data to the hub from the device simulator.  You could extrapolate that into your own needs. For simplicity, I made a class called `SimpleTelemetryData` that has a temperature and a fan status.  A `FanStatus` enumeration, and then a generator to simulate some data.  Again, this code is based on the original code from the AZ-220 materials.

Code for the 


### Resources

- [Raspberry PI Simulator](https://azure-samples.github.io/raspberry-pi-web-simulator/)