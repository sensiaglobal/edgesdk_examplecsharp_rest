# Introduction 
A .Net application to demonstrate integrating an application with the HCC2 REST server. This demo application dynamically registers itself with the REST server using endpoints documented in the Swagger interface and a workflow described in the accompanying [SDK documentation](https://edgesdk.sensiadigital.net/sdkoverview/rest.html).  The application then proceeds to execute simple business logic by reading and writing data point values ad can read configuration parameters by polling over REST or reacting to a webhook callback.

# Overview

1. **Application Setup (RunAsync)**
      - Wait for server and create data points
      - Configure webhook/REST mode and start heartbeat
      - Wait for HCC2 provisioning and deployment
      - Initialize with first data point read

2. **Configuration Management (RunBusinessLogicAsync)**
      - Get updates via webhooks or REST polling
      - Apply period (1-60s) and restart interval (1-24h)
      - Handle statistics resets on intervals

3. **System Monitoring**
      - Read CPU, memory, temperature metrics
      - Track min/max values over time
      - Reset tracking on restarts

4. **Data Publishing Loop**
      - Write metrics to HCC2 with timestamps
      - Update heartbeat and track failures
      - Wait configured period and repeat


# Getting Started

1.	Installation process
    - Clone this repository
2.	Software dependencies
    - .Net 8
    - HCC2 SDK 2v0 or above
3.	Latest releases are available in this public GitHub site.
4.	Read [HCC2 SDK API references](https://edgesdk.sensiadigital.net/sdkoverview/rest.html)

# Build and Test
The application has been developed under WSL/Debian with Visual Studio Code, but a wholly Windows or Linux environment is also possible. A Docker environment and the ability to menderize Docker images is needed to deploy the application to the HCC2. Refer to the public SDK docs for full process details.

The default code is configured to be deployed and run within the HCC2.  Update AppConfig.cs with IP addresses that reflect your environment to test locally, or use the environment variables to define options. Environment variables override hardcoded values.

To build from the command line, from the repository root folder:

        $ dotnet build
        MSBuild version 17.9.4+90725d08d for .NET
        Determining projects to restore...
        All projects are up-to-date for restore.
        HCC2RestClient -> <local path>/edgesdk2examplecsharp/bin/Debug/net8.0/HCC2RestClient.dll

        Build succeeded.
            0 Warning(s)
            0 Error(s)

        Time Elapsed 00:00:02.58

Set the minimum environment variables within the launch.json:

      SDK2_API_URL: "http://<hcc2 IP address>:7071/api/v1"
      SDK2_CALLBACK_URL: "http://<local IP address>:8100/webhook/v1"
      SDK2_USE_WEBHOOKS: "true"


Firewall considerations:
    
- Make sure your development environment firewall allows TCP port 8100 ingress connections.
- Ensure the HCC2 firewall exposes TCP 7071 (on the Ethernet port you connect via)


        $ dotnet run
        Building...
        [0][2025-05-02T13:19:36.069Z][courseNetApp][info]Application starting...
        [0][2025-05-02T13:19:36.154Z][courseNetApp][info]Checking server status...

# Test Docker Container Locally

Use the default source code settings.

Build the Docker image:

    $ docker build -t coursenetapp:0.0.0 .

Run the Docker container:

    $ docker-compose up -f docker-compose-test.yml

# Deploy Docker Container

Use the default source code settings.

Build the Docker image:
$ docker build -t coursenetapp:0.0.0 .

## Deploy To HCC2

Rename the docker-compose.yml to docker-compose-coursenet.yml then follow the deploy process in the SDK documentation to package and send the mender file to the HCC2.

# Making Changes

## Application name

Add your own name (camel case) as an environment variable:

    environment:
      SDK2_API_URL: "http://hcc2RestServer_0:7071/api/v1"
      SDK2_CALLBACK_URL: "http://coursenetapp:8100/webhook/v1"
      SDK2_USE_WEBHOOKS: "true"
      SDK2_APP_NAME: "testNetApp"
      LOG_LEVEL: "Info"

## Different Business Logic

Add your own application's data points by editing the dictionary of points in App.RunAsync() method, namely : configPoints and generalPoints. Ensure supported data types are used.

Modify RunBusinessLogicAsync to optionally read other HCC2 data points by modifying the 'readings' collection, extract the required values and then perform any business logic needed before writing values back, making use of the valuesToWrite collection and the _generalDataPoints with a key of the data point needing to be written to.

# Example Logs

A log with webhooks enabled, LOG_LEVEL=debug:

    [0][2025-05-02T11:05:15.025Z][courseNetApp][info]Defining app...
    [0][2025-05-02T11:05:15.038Z][courseNetApp][info]App courseNetApp initialized
    [0][2025-05-02T11:05:15.039Z][courseNetApp][info]Registering config data points...
    [0][2025-05-02T11:05:15.044Z][courseNetApp][debug]Request payload for config: {"tagsList":[...]}
    [0][2025-05-02T11:05:15.059Z][courseNetApp][info]Registering app...
    [0][2025-05-02T11:05:15.397Z][courseNetApp][info]Webhook enabled, setting up webhook handling...
    [0][2025-05-02T11:05:15.406Z][courseNetApp][debug]Webhook subscription successful for topics liveValue.postvalidConfig.this.courseNetApp.0.maxminrestartperiod., liveValue.postvalidConfig.this.courseNetApp.0.configrunningperiod. on http://173.0.9.52:8100/webhook/v1/simple_message
    [0][2025-05-02T11:05:15.539Z][courseNetApp][info]Webhook server started at http://0.0.0.0:8100/webhook/v1/
    [0][2025-05-02T11:05:15.539Z][courseNetApp][info]Webhook subscriptions set up for topics: liveValue.postvalidConfig.this.courseNetApp.0.maxminrestartperiod., liveValue.postvalidConfig.this.courseNetApp.0.configrunningperiod.
    [0][2025-05-02T11:05:15.539Z][courseNetApp][info]Delaying 5 Seconds : Core Application Registration
    [0][2025-05-02T11:05:20.539Z][courseNetApp][info]Starting heartbeat...
    [0][2025-05-02T11:05:20.597Z][courseNetApp][info]Reading initial data points...
    [0][2025-05-02T11:05:20.610Z][courseNetApp][info]Initial data points read successfully, asserting a healthy heartbeat...
    [0][2025-05-02T11:05:20.618Z][courseNetApp][info]Starting business logic...
    [0][2025-05-02T11:05:20.647Z][courseNetApp][debug]Successfully wrote 10 data points
    [0][2025-05-02T11:05:20.647Z][courseNetApp][info]Run 1: CPU=14.396667, Memory=3687100, Temp=306.38785

# Web Hook Tests

Use the included bash script within Linux or use it to inspire a similar PowerShell script for a Windows environment. The script pushes random values into the configrunningperiod configuration data point to demonstrate the application reading the configuration data value in either REST polling mode or reacting to the new value in web hooks mode.

The script requires the IP address of the HCC2 and the targeted appName.  The HCC2 will need to expose the REST interface through its firewall.

./webhookTest.sh 173.0.0.41 courseNetApp

Then follow the deploy process in the SDK documentation to package and send the mender file to the HCC2.

# Contribute
Clone locally and fork to contribute updates.

# References
- [Visual Studio Code](https://github.com/Microsoft/vscode)
- [HCC2 SDK Documentation](https://edgesdk.sensiadigital.net/)
