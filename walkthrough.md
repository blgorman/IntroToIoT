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

In the next part, you'll use C# to create a simulator and connect to the IoT Hub via the SDK.  If you just want the original project, [just use the code from the official repo](https://github.com/MicrosoftLearning/AZ-220-Microsoft-Azure-IoT-Developer/tree/master/Allfiles/Labs/07-Device%20Message%20Routing/Starter/VibrationDevice);

The original project is in .Net Core 3.1  The project in this repo is .Net 6.

>**Note**: My project leverages the `appsettings.json` file and ultimately the developer secrets, which is divergent from the sample application found [here](https://github.com/blgorman/AZ-220-Microsoft-Azure-IoT-Developer/tree/master/Allfiles/Labs/07-Device%20Message%20Routing/Starter/VibrationDevice).  If you aren't sure how to set that up but want to build from scratch, review my project for the ConfigurationBuilder and the creation of it in the main Program.

### Additional NuGet Packages

Note the following NuGet packages exist in the solution:

1. Microsoft.Azure.Devices.Client

### Wire up the connection for the device to the hub

It is important to note that the device ID will map to a preregistered device.  If the device is not pre-registered and you don't have a connection string to connect to, then you won't be able to connect and simulate the telemetry.

1. Notice the setup to allow the device simulator to connect.

    ```c#
    private const int _telemetryIntervalMilliseconds = 2000;
    private static DeviceClient _deviceClient;
    private static string _deviceConnectionString = "";
    ```  

2. The connection string information is retrieved from the `appsettings.json` file

    ```c#  
    _deviceConnectionString = _configuration["Device:ConnectionString"];
    ```  

    Ideally, you should move this to the developer secrets and/or leverage Azure Key Vault.

    After reviewing the device connection string, review the code to connect to the IoT hub:

    ```c#
    _deviceClient = DeviceClient.CreateFromConnectionString(
                    _deviceConnectionString,
                    TransportType.Mqtt);
    ```  

    Note that there are many choices for transport.  The choices include  `MQTT` and `AMQP`, both of which are binary transport protocols.  `MQTT` works fine in this scenario as each device would connect to the hub individually.  If connection multiplexing is needed, then use `AMQP`.  `HTTPS` could also be used but it needs to reconnect on every transmission, where `AMQP` and `MQTT` can establish the connection once and reuse it.


## Create telemetry

The process to create telemetry is to build some sort of data generator that sends data to the hub from the device simulator.  You could extrapolate that into your own needs. For simplicity, from the AZ-220 materials we'll leverage the device simulator for a vibration device.

Code for the simulator is in my repo but again traces back to its roots in the original repository.

Once you get the connection string information set, you can then send telemetry to your hub.


## Utilize the Device Provisioning Service to allow devices to automatically connect

In this next part, we'll leverage the simulator making multiple copies, and we'll utilize certificates against the DPS to allow devices to automatically connect.

### Create the DPS at Azure

The first step is to create a Device Provisioning Service (DPS) at Azure.  This DPS will allow devices to automatically register and connect to the IoT Hub.

1. Create the DPS

    ```bash
    dpsName=my-iot-dps-20221231xyz
    az iot dps create --name $dpsName -g $rg --sku S1
    ```  

1. Create the certificates

    ```bash
    mkdir IntroToIoT
    cd IntroToIoT
    mkdir certificates
    cd certificates
    curl https://raw.githubusercontent.com/Azure/azure-iot-sdk-c/master/tools/CACertificates/certGen.sh --output certGen.sh
    curl https://raw.githubusercontent.com/Azure/azure-iot-sdk-c/master/tools/CACertificates/openssl_device_intermediate_ca.cnf --output openssl_device_intermediate_ca.cnf
    curl https://raw.githubusercontent.com/Azure/azure-iot-sdk-c/master/tools/CACertificates/openssl_root_ca.cnf --output openssl_root_ca.cnf
    chmod 700 certGen.sh
    ./certGen.sh create_root_and_intermediate
    download ~/IntroToIoTDemo/certificates/certs/azure-iot-test-only.root.ca.cert.pem
    ```  

    Download the file

1. Register the certificates 

    Navigate to your IoT DPS in the portal.  Find the `Certificates` section, and add the cert you just downloaded.  Name it `root-ca-cert` or something very similar that you can remember.  Check `Verify Automatically` and upload/save the root cert.

    You used to have to manually verify this cert, but now you can have Azure do it automatically.  If you choose to verify yourself, you can find instructions [here](https://github.com/MicrosoftLearning/AZ-220-Microsoft-Azure-IoT-Developer/blob/master/Instructions/Labs/LAB_AK_06-automatic-enrollment-of-devices-in-dps.md_)


1. Create a group enrollment in the DPS

    Now that you have a root certificate set, you can use that to allow devices to connect through a group enrollment

    Navigate to the `DPS` and find `Manage Enrollments` which is just under the Certificates section.

    Click on `Add Enrollment Group` and create the enrollment group.  Name it `my-iot-eg`

    Choose the following

    - Attestation Type: `Certificiate`
    - IoT Edge Device: `False`
    - Primary Cert: `root-ca-cert` or whatever you named your root cert for the DPS
    - Secondary Cert: `No certificate selected`
    - Assign Devices: `evenly weighted distribution`
    - IoT Hub: if not already set, select your IoT hub with owner access, save it, then check the box

    Alternatively, you can do this through the CLI:

    ```bash
    hubConnectionString=$(az iot hub connection-string show --hub-name $hubName --resource-group $rg --key-type primary --query connectionString -o tsv)
    az iot dps linked-hub create --dps-name $dpsName --resource-group $rg --connection-string $hubConnectionString
    ```

    - Select how you want ... re-provisioning: `Re-provision and migrate data`
    - Initial Device Twin State:

    ```json
    {
        "tags": {},
        "properties": {
            "desired": {
                "telemetryDelay": "1"
            }
        }
    }
    ```  

    - Enable Entry: `Enable`

    Hit Save

    You've now created the enrollment group.


1. Create some simulated devices

    Leverage information from [this document](https://docs.microsoft.com/azure/iot-dps/quick-create-simulated-device-x509?tabs=windows&pivots=programming-language-ansi-c).

    Additionally, get the solution code for the certificate simulated device



1. Connect and run the devices

    Note how they automatically connect and start sending telemetry data.

## Use Stream Analytics to analyze the hot/code path data

In this part, you will analyze data from the hub into a hot path using Stream Analytics

## Push all data to the cold path storage

In this part, you will push data to the cold path storage for analysis later in the game

## Azure IoT Edge

As we're likely out of time here, for this last part, we'll just examine the idea of an edge device.

At the end of the day, you would create a device and enroll as an edge  device.  Once the device is registered, you can deploy packages to the edge device and transfer data processing to the edge device

### Resources

- [Raspberry PI Simulator](https://azure-samples.github.io/raspberry-pi-web-simulator/)