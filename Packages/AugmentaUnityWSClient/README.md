# Augmenta WebSocket Client for Unity
[Augmenta](https://augmenta.tech/) is a tracking technology to bring interactivity to immersive spaces. This package provides utilies to connect to an Augmenta Server using a `WebSocket Output`and to consume the received data.

## Features

This is a new Augmenta package for Unity, which is designed to receive data from Augmenta's WebSocket output. It supports:

- Tracking features (clusters IDs, positions, bounding boxes, speed, etc.)
- Point Cloud streaming
- Zone triggering and control

⚠️ This package is currently in beta and provided as is, please send [feedback](https://www.notion.so/199653bb205780a69636f50e214f62b6?pvs=21) or [contact](https://augmenta.tech/contact) us !

## Requirements

Augmenta 1.5.0b+
OR
Augmenta (new) simulator 1.5.0b+ : [Augmenta new Simulator (beta)](https://tech.docs.augmenta.tech/beta-program/augmenta-new-simulator-(beta))

## Getting started

In Unity:

- Window > Package Manager
- Click on the “+” at the top left > Install package from git URL…
- Copy the URL: https://github.com/Augmenta-tech/AugmentaUnityWebsocket.git?path=/Package/AugmentaUnityWSClient#new-structure
- On the right > Install
- In your Project, under Packages/Augmenta Websocket Unity Client you’ll find everything you need.

Check out the provided sample scene to get an idea how to make use of the package !

## Dependencies
### Augmenta SDK
This package makes use of the [Augmenta Client C# SDK](https://github.com/Augmenta-tech/AugmentaClientSDK-CS).  

### Third party
[ZstdNet](https://www.nuget.org/packages/ZstdNet)
[websocket-sharp](https://github.com/sta/websocket-sharp)

## License

This Unity plugin is open source under the MIT License (see LICENSE).  
However, it uses the proprietary Augmenta SDK, which is not open source.  
Use of the SDK is subject to the Augmenta SDK License.  
You may not redistribute the SDK itself without permission from Augmenta.
