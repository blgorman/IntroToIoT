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
az iot hub list --query "[?contains(name, '${hubName}')]" -o table
```  

### Examining the hub

In the portal, browse to the hub and take a look around.

Notice that there are two device blades - `Devices` and `IoT Edge`

Note that the registration of devices for each will be similar, but there is a flag that toggles the device as an edge device.

### Resources

- [Create an IoT hub using the Azure CLI](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-create-using-cli)

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
az iot hub device-identity list  --hub-name $hubName --query "[?contains(deviceId, '${deviceId}')]" -o table
```  

>**Note**: Adding a device registration is a necessary first step to get telemetry from a device, however the device must also connect to the hub.  Therefore, remember that registration != connection.  Also note that once a device is registered, if you need to prevent the device from connecting you must ensure that the device is no longer registered.

## Create a simulator and send telemetry to the hub

In the next part, you'll use C# to create a simulator and connect to the IoT Hub via the SDK.  If you just want the original project, [just use the code from the official repo](https://github.com/MicrosoftLearning/AZ-220-Microsoft-Azure-IoT-Developer/tree/master/Allfiles/Labs/07-Device%20Message%20Routing/Starter/VibrationDevice);

The original project is in .Net Core 3.1  The project in this repo is .Net 6.

>**Note**: My project leverages the `appsettings.json` file and ultimately the developer secrets, which is divergent from the sample application found [here](https://github.com/blgorman/AZ-220-Microsoft-Azure-IoT-Developer/tree/master/Allfiles/Labs/07-Device%20Message%20Routing/Starter/VibrationDevice).  If you aren't sure how to set that up but want to build from scratch, review my project for the ConfigurationBuilder and the creation of it in the main Program.

### Additional NuGet Packages

Note the following NuGet packages exist in the solution:

1. Microsoft.Azure.Devices.Client
2. Microsoft.Azure.Devices.Provisioning.Client
3. Microsoft.Azure.Devices.Provisioning.Transport.Amqp
4. Microsoft.Azure.Devices.Provisioning.Transport.Http
5. Microsoft.Azure.Devices.Provisioning.Transport.Mqtt

The Client device is the only thing needed to connect with a connection string.  When using certificates and the Azure Device Provisioning Service, the remaining four provisioning packages are required.

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

1. Create the root certificate

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

1. Register the root certificate

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

1. Create three simulator device certificates.

    These certificates will be used to allow the devices to auto-register to connect to your IoT Hub

    To complete this, you will need three device certs for devices named 

    `vibration-sensor-2500`
    `vibration-sensor-2600`
    `vibration-sensor-2700`

    >**NOTE:** As you create certificates, the generated cert will be named `new-device.cert.pfx` and will have a matching file `new-device.cert.pem`.  You will want to rename these files each time you are going to generate new device certificates to avoid overwriting the certificates.

    Repeat the following operations for each of the three devices, replacing `[cert-name]` with the individual device name

    ```bash
    ./certGen.sh create_device_certificate [cert-name]

    mv ~/introToIoT/certificates/certs/new-device.cert.pfx ~/introToIoTcertificates/certs/[cert-name]-device.cert.pfx
    mv ~/introToIoT/certificates/certs/new-device.cert.pem ~/introToIoT/certificates/certs/[cert-name]-device.cert.pem
    ```  

    i. e.
    ```bash  
    ./certGen.sh create_device_certificate vibration-sensor-2500

    mv ~/introToIoT/certificates/certs/new-device.cert.pfx ~/introToIoT/certificates/certs/vibration-sensor-2500-device.cert.pfx
    mv ~/introToIoT/certificates/certs/new-device.cert.pem ~/introToIoT/certificates/certs/vibration-sensor-2500-device.cert.pem
    ```
    
    Download the *.pfx files and replace the existing certificates in your project directory to map to your DPS.  Failure to do this will mean you will be trying to use invalid certificates.

    ```bash
    download vibration-sensor-2500-device.cert.pfx
    download vibration-sensor-2600-device.cert.pfx
    download vibration-sensor-2700-device.cert.pfx
    ```  

1. Create some simulated devices

    Leverage information from [this document](https://docs.microsoft.com/azure/iot-dps/quick-create-simulated-device-x509?tabs=windows&pivots=programming-language-ansi-c).

    Additionally, the simulator in this repo has the ability to use certificates baked in.  You will just need to configure a few things.

    Each device will need to be configured to use the correct DPS Id Scope and the generated certificates.

    Find the ID Scope for your `DPS` from the overview in the portal.  It should be similar to `0ne00576D06`, and is located under the service and global device endpoints and above the pricing and scale tier.   Alternatively, you can get the information with the following command:

    ```bash
    az iot dps show --name $dpsName --query properties.idScope -o tsv
    ```

    >**NOTE:** Do not forget to update the `DPSIdScope` value in the `appsettings.json` file with the value of your DPSIdScope.

1. Connect and run the devices

    Make three copies of the project directory and use terminals to start each of the three, making sure to leverage the correct certificate file by choosing the device to simulate.

    Alternatively, just start three instances with Debug -> Start new instance

1. Change the telemetry delay on the DPS devices

    Using the device twin, go to the device and change delay to 5.  This will modify the frequency in which telemetry is sent to the hub.

Final thoughts here

- Having the enrollment group means you can deactivate enrollment for all of the devices
- De-enrolling a device does NOT deregister from the hub, but it will prevent re-registering.  If a device is registered on the hub, you must also deregister it from the hub to ensure it cannot send messages.
- You can see what devices have been registered in the Enrollment group details Registration Records

## Push all data to the cold path storage

In this part, you will push data to the cold path storage.

1. Begin by creating an Azure Storage account to store the data.

    Use the following code in the azure cloud shell or from your local terminal of choice to create a new storage account and container. 

    >**Note:** The code below relies on variables set from above.  If you've stepped away and restarted the terminal or it has timed out, you will need to refresh the variables.  For that reason, they are reset in this code. If you don't need these variables, don't use them.  If you need to modify them to match something you did earlier, make sure to do so.

    ```bash
    rg=az-iot-demo-rg
    loc=centralus
    saName=iotdemostor20221231xyz
    containerName=iot-cold-path-data
    accessLevel=blob

    az storage account create --name $saName --resource-group $rg --kind BlobStorage --sku Standard_LRS --access-tier Hot --location $loc

    az storage container create --name $containerName --account-name $saName --public-access $accessLevel
    ```  

    You can alter some of these things, like not exposing to the public for instance.

    >**Note**: You can create account and container information while building the route for the cold storage from your IoT hub in the portal, however it is much cleaner to just select pre-built storage account information

1. Use the portal to build a new route for your cold-path data.

    You could likely do this through commands but it will be easier to configure the route for cold path data in the portal.

    >**Note:** For this to work, you must have completed steps above to get telemetry mapped to the IoT Hub from a simulator. Furthermore, you must have code that is adding properties to the messages as follows (this exists in the sample code from this repo as well as in the original code):

    ```c#
    ...
    telemetryMessage.Properties.Add("sensorID", "VSTel");
    ...
    loggingMessage.Properties.Add("sensorID", "VSLog");
    ...
    ```  

    Navigate to your IoT hub and open the blade for `Message Routing`

    Add a new route as follows:

    Name: `telemetryLoggingRoute`
    Endpoint: `Storage`  -> Use the `+ Add Endpoint` and select Storage to configure
        Endpoint Name: `telemetryLogEndpoint`
        Pick a Container: [Select the container created above from the appropriate account]
        Leave all the rest of the settings as-is.  If you want, you could log as JSON, but the default has been `AVRO`.  Certain Big Data Solutions may require `AVRO` format.

        Hit `Create` and allow the route to create the connection to the storage endpoint

    You are now back on the original page

    Data Source: `Device Telemetry Messages`
    Enable Route: `Enabled`
    Routing Query: `sensorID = 'VSLog'

    Save the changes

1. Ensure the cold path storage is working.

    Restart one or more of the simulators.  They should log all telemetry to the cold storage.  After a couple of minutes, navigate to the storage container and ensure you are getting data logged in the format you chose under the folder structure for the route you just created.

    Be patient, sometimes it takes 5 minutes to start showing up.  You may need to stop the simulators after a few minutes to get the first writes to show up.

## Use Stream Analytics to analyze the hot/code path data

In this part, you will analyze data from the hub into a hot path using Stream Analytics.  All data is currently going to the cold path, however you will want to be reporting data to users immediately if it is out of range.  This is where stream analytics will give you the results you are looking for.  

In order for the Stream Analytics to work, you will need a Stream Analytics job with an input and an output and a query to select data from the input.

For this demo, you'll import from the hub, select the target 'critical' data, and push that into storage.  In the real world you might push the data into Power BI or another intelligence dashboard.

1. Create a new Stream Analytics Job

    Use the portal to create a new Stream Analytics job.

    For the Job, set the following parameters:

    Job Name: `IoTTelemetryHotPath`  
    Subscription: `<your-sub>`  
    Resource Group: `<your-rg>`  
    Location: `<your-location>`  
    Hosting Environment: `Cloud`  
    Streaming Units: `1`  
    Secure all private data ...: `unchecked`  

    Click `Create`  

1. Create an input source.

    For this step, you need to set the input.  Navigate to the job in the portal, and then select the `Inputs` under `Job Topology`. Click `Add Stream input` and select `IoT Hub` from the dropdown.

    Enter the information in the pane on the right side:  

    Input Alias: `iotTelemetryInput`  
    Select IoT Hub from your subscriptions: `selected`  
    Subscription: `<your-sub>`  
    IoT Hub: `<select your hub>`  
    Consumer Group: `$Default`  
    Shared access policy name: `iothubowner`  
    Shared access policy key: ... this is autofilled  
    Endpoint: `Messaging`  
    Partition Key: Leave blank  
    Event Serialization Format: `JSON`  
    Encoding: `UTF-8`  
    Event Compression: `None`  

1. Create the storage account to save output data

    For this step, you'll create the output.  For demo purposes, output is just going to go to storage.

    To make this work, first create a new DataLake Storage account and container using the portal

    Navigate to storage accounts and select `Create`.

    Select your subscription and resource group.  Create new if necessary, but you should have a resource group for this activity you could just use, unless you want to keep this separate for some reason. 

    Set the following properties:

    Storage Account Name: `iotdlakestor20221231xyz`
    Region: `<your-region>`
    Performance: `standard`
    Redundancy: `LRS`

    Select `Advanced`.  

    Check the box for `Enable Hierarchical Namespace` to make this a data lake storage account.

    Select `Outputs` under the `Job topology`.  In the dropdown, select `Blob Storage/ADLS Gen2`  

    Select `Review And Create`, then validate, then `Create` when the validation has passed.  Optionally, you could configure other settings on the storage account first if you wanted, such as data retention and soft-delete policies.

    Once the account is created, make a new container.

    Name the container: `iot-hot-path-data`
    Public Access Level: `Blob (anonymous read access for blobs only)`  

    Click Create.

1. Create the output 

    Navigate back to the stream analytics job in the portal, and then select the `Outputs` under `Job Topology`. Click `Add Stream input` and select `IoT Hub` from the dropdown.

    Enter the information in the pane on the right side:  

    Set the following properties on the right pane:  

    Output Alias: `iotTelemetryOutput`
    Select Blob storage/ADLS Gen2 from your subscriptions: `Selected`
    Storage account: `select the data lake storage and container you created above`
    Authentication Mode: `Connection String`

    Leave the rest of the settings as-is.

    Save the output path.

1. Create the query.

    For the next part, you will create the query to select data for porting to the hot path.

    This is where the power of the Stream Analytics will come into play, as the job will be able to filter all of the telemetry and only push the critical path data to the hot path.

    Navigate to the Job topology `Query` section, and use the following query:

    ```sql
    SELECT
        *
    INTO
        iotTelemetryOutput
    FROM
        iotTelemetryInput
    WHERE vibration > 1.5 or vibration < -1.5
    ```  

1. Run the simulators and see the query working

    Start a couple of simulators to send data to the IoTHub if not already running.

    Navigate to the stream analytics job in the portal and start the job.

    Use the `Test Query` to view results

1. Navigate to the Storage account and review the output data files

    Provided some data was filtered, you should be able to navigate to storage and see the output data.


### Resources

- [Raspberry PI Simulator](https://azure-samples.github.io/raspberry-pi-web-simulator/)
- [Microsoft AZ-220 Labs](https://github.com/MicrosoftLearning/AZ-220-Microsoft-Azure-IoT-Developer)
- [Linux VM Edge GateWay Device](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FMicrosoftLearning%2FAZ-220-Microsoft-Azure-IoT-Developer%2Fmaster%2FAllfiles%2FARM%2Flab12a.json)  
- [Industry Specific Azure IoT reference Architectures](https://docs.microsoft.com/en-us/azure/architecture/reference-architectures/iot/industry-iot-hub-page)
- [Azure Marketplace for IoT Edge](https://azuremarketplace.microsoft.com/en-us/marketplace/apps/category/internet-of-things?page=1&subcategories=iot-edge-modules)
- [Pimoroni Getting Started with Enviro+](https://learn.pimoroni.com/article/getting-started-with-enviro-plus)
- [Digikey PIM 486](https://www.digikey.com/en/products/detail/pimoroni-ltd/PIM486/11205841) 
- [Digikey PIM 458](https://www.digikey.com/en/products/detail/pimoroni-ltd/PIM458/10289741) - optional upgrade for PMS5003
- [UCTRONICS Male to Female GPIO Ribbon Cable](https://www.amazon.com/dp/B07D991KMR?psc=1&ref=ppx_yo2ov_dt_b_product_details)  
- [PMS5003 Digital output Air Quality Monitoring Dust Haze Tester](https://www.amazon.com/dp/B07S3735CY?psc=1&ref=ppx_yo2ov_dt_b_product_details)  
- [One of many options for Pi](https://www.amazon.com/Raspberry-Model-2019-Quad-Bluetooth/dp/B07TD42S27/?_encoding=UTF8&pd_rd_w=B7Mk7&content-id=amzn1.sym.bbb6bbd8-d236-47cb-b42f-734cb0cacc1f&pf_rd_p=bbb6bbd8-d236-47cb-b42f-734cb0cacc1f&pf_rd_r=JCZ83YP1PV51SFW3VE8F&pd_rd_wg=Y4Sh6&pd_rd_r=1c78c805-ba73-4694-8ba1-41e391a711a3&ref_=pd_gw_ci_mcx_mi)  
- [Freenove ulitmate starter kit](https://www.amazon.com/dp/B06W54L7B5?psc=1&ref=ppx_yo2ov_dt_b_product_details)  
