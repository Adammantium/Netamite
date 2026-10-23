# Netamite
-----------------
A imperfect solution but the only one that worked for me

## Why?
-----------------
When discord stopped their networking solution i had to fall back to TCP/UDP. After that i wanted to add Steam networking but I was like: Lets start from scratch.
The mission: Make netcode thats platform independent, easy to expand and the game itself doesnt need to worry about what channel the data is flowing through.
One netcode, mutliple tranmission layers.
I didn't find anything that made that possible and as i wanted to have public servers without any steam stuff, that was the way to go.

## So what does it do?
-----------------
- One Netcode, no need to worry about Steam Networking or TCP/UDP (maybe more in the future, who knows?)
- Packet Handling (Encoding / Decoding / Transmission / Receiving) handled by the netcode, you just have to write what the packets should do
- Connection Handling with custom ClientData & Password
- No encryption/compression yet, but its planned

# How to use it

## Netamite Initialization
### Attach to the Netamite Logger
```C#
Netamite.Logging.Log.loggerType = Netamite.Logging.Log.LoggerType.EVENT_ONLY;
Netamite.Logging.Log.onLogMessage += (type, message) => {
    Log.Msg((Log.Type) type, message);
};
```
### Register your custom ClientData
Your custom ClientData Class needs to extend from `Netamite.Server.Data.ClientInformation`.  
Put all the additional client information you want to store in here.  
By default a Client-ID and Client-Name is stored inside the class (and some additional backend informations)
```
public class YouClientDataClass : ClientInformation {

}
```

Now tell Netamite to use your custom class instead of the built in one:  
You should do that when initializing your project.
```C#
Netamite.Netamite.SetClientinfoType(typeof(YouClientDataClass));
```

After that, dont forget to initialize the system:
```C#
Netamite.Netamite.Initialize();
```


### Define your own Packets
#### Packet Structure
If you are using custom client data, you should create your own base packet (that extends `NetPacket`) like this:
```C#
public class MyBasePacket : NetPacket {
    /// <summary>
    /// This will be called when the packet is received on the Server.
    /// </summary>
    /// <param name="server">Server Instance that is processing the  packet</param>
    /// <param name="client">Client Information that sent the Packet</param>
    /// <returns>If you return true the packet is deemed as processed</returns>
    public virtual bool ProcessServer(NetamiteServer server, YouClientDataClass client) { return false; }

    public override bool ProcessServer(NetamiteServer server, ClientInformation client) {
        return ProcessServer(server, (YouClientDataClass) client);
    }
}
```
This way you override the default `ClientInformation` class and you pass on your own.

#### Define your packet
To define your packet, you just need to define a class that extends the base `NetPacket` class or your custom one.  
Also make sure to pass in a unique identifier for your class, by default this only allows for 255 different packet types.  
Keep in mind, the first 10 (0-9) ids are used by netamite itself, so you should not override those
```C#
[PacketDefinition((byte) 10)]
public class MyCustomPacket : MyBasePacket {
```
To add attributes to the packet, you just define the variables you want to use and add a `SyncedVar` annotation to them:
```C#
[SyncedVar] public string my_string;
[SyncedVar] public float full_float;
[SyncedVar(true)] public float non_precise_float;
```
`SyncedVar` also allows for a boolean to be passed to it, if you pass in a true value, it will reduce the data that will be sent, but you will lose precision. This mostly works on floats, they will get converted to halfs before they are transmitted.  
This conversion will be handled without you having to do anything, but keep in mind that this will reduce the precision of the float.

#### How to handle packet
There are two ways to handle packets within netamite, you can either handle the packet within the packet class itself or use a global packet handler.
##### Handle them withing your packet (recommended)
The `NetPacket` class will allow you to override the `ProcessServer` and `ProcessClient` methods.
```C#
public override bool ProcessClient(NetamiteClient client) {
	return true; // Return true when you successfully processed the packet
}

public override bool ProcessServer(NetamiteServer server, ClientData client) {
	return true; // Return true when you successfully processed the packet
}
```

##### Use a Global Handler
You can attach to the `OnDataReceived` event to handle packets globally.

## Server Side Initialization
Create the server for the transmission layer you want to use, attach to the events you need and start it.

### TCP/UDP
`IPServer` listens on TCP and UDP. By default both use the same port (13698), you can change them with `TcpPort` and `UdpPort`.
```C#
NetamiteServer server = new IPServer(12345, 16); // port, max clients

server.ConnectToken = "my password";   // optional, clients need to supply the same token to join
server.ClientTimeoutDelay = 30000;     // optional, ms without a packet before a client gets kicked

server.OnStart        += (delay) => Log.Info($"Server started after {delay}ms");
server.OnStartupError += (error) => Log.Err($"Server could not start: {error}");
server.OnConnect      += (client) => Log.Info($"{client.ClientName} joined");
server.OnDisconnect   += (client, reason) => Log.Info($"{client.ClientName} left: {reason}");

// Called for every packet that doesn't handle itself in ProcessServer
server.OnDataReceived += (client, packet) => { };

server.Start();
```
Stop it again with `server.Stop()`, connected clients will get disconnected with `DisconnectReason.SERVER_CLOSED`.

### Steam
`SteamServer` creates a Steam lobby that the clients join. Steam has to be initialized before (see [Steam](#steam)).
```C#
NetamiteServer server = new SteamServer(16, true); // max clients, friends only
server.Start();

// Once OnStart was called, the lobby id is available and can be shared with the clients
ulong lobbyId = ((SteamServer) server).currentLobby.LobbyId.m_SteamID;
```

### Sending packets from the server
```C#
server.SendTo(clientId, packet);                   // to a single client (also accepts a ClientInformation)
server.SendToAll(packet);                          // to every client
server.SendToAllExcept(packet, client.ClientId);   // to everyone except the given client ids
```
`SendTo...` picks the reliable or unreliable channel based on the packets `PacketDefinition`. To force a channel, use the `SendReliable...` / `SendUnreliable...` variants.  
All connected clients are available through `server.Clients`, a single one through `server.GetClientById(id)`.

## Client Side Initialization
Create the client for the transmission layer the server uses, attach to the events and connect.

### TCP/UDP
`IPClient` accepts an IP address or a hostname.
```C#
NetamiteClient client = new IPClient("127.0.0.1", 12345);
client.ClientName = "Player";

client.OnConnect         += () => Log.Info($"Connected with id {client.ClientId}");
client.OnConnectionError += (error) => Log.Err($"Could not connect: {error}");
client.OnDisconnect      += (reason) => Log.Info($"Disconnected: {reason}");

// Called for every packet that doesn't handle itself in ProcessClient
client.OnDataReceived += (packet) => { };

client.Connect("my password"); // the servers ConnectToken, leave empty if it has none
```
Connecting happens in the background, wait for `OnConnect` before sending packets.  
Disconnect with `client.Disconnect()`.

### Steam
```C#
NetamiteClient client = new SteamClient(lobbyId);
client.Connect();
```
Players that join through the Steam overlay or accept an invite are handled by `SteamIntegration.OnOverlayJoin`, which hands you an already created `SteamClient`:
```C#
SteamIntegration.OnOverlayJoin += (steamClient) => {
    // attach your events, then
    steamClient.Connect();
};
```

### Sending packets from the client
```C#
client.Send(new MyCustomPacket() { my_string = "Hello" });
```
Like on the server, `Send` picks the channel based on the `PacketDefinition`, `SendReliable` / `SendUnreliable` force one.  
Packets are reliable by default, to make one unreliable (e.g. for positions that get sent all the time) pass `false` first:
```C#
[PacketDefinition(false, (byte) 11)]
public class MyPositionPacket : MyBasePacket {
```
Some useful values while connected: `client.Ping` (ms) and `client.FixedTimestamp` (a timestamp synchronized with the server).

## Steam
Netamite.Steam uses [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET), the game needs to ship it. Initialize Steam once before creating a `SteamServer` or `SteamClient`:
```C#
SteamIntegration.OnInitialized += () => Log.Info($"Steam ready, logged in as {SteamIntegration.Username}");
SteamIntegration.OnError       += (e) => Log.Err(e);

SteamIntegration.Initialize(yourAppId);
```
By default a thread runs the Steam callbacks for you. If you call `SteamIntegration.Initialize(yourAppId, false)` instead, call `SteamIntegration.RunCallbacks()` regularly yourself (e.g. every frame).  
Call `SteamIntegration.Shutdown()` when the game closes.

## Unity
Reference `Netamite.Unity` to be able to use `Vector3`, `Quaternion` and `Color` in `SyncedVar` fields:
```C#
[SyncedVar(true)] public Vector3 position;
[SyncedVar(true)] public Quaternion rotation;
```

## Voice Chat (Unity)
`Netamite.Unity.Voice` records the microphone, encodes it, sends it to the other players and plays back what it receives. The codecs live in `Netamite.Voice.Codec`, so ship both dlls.
```C#
UnityVoiceClient voiceClient = new UnityVoiceClient(client); // uses Opus by default
voiceClient.Start();
```
Create it after the client connected, there can only be one at a time, creating a new one stops the old one.  
The server relays the voice packets on its own, it only needs `Netamite.Unity.Voice` to be loaded, so it knows the packet.

Some of the things you can configure:
```C#
voiceClient.SetInputDevice(VoiceReader.AvailableMicrophoneDevices[0]); // default microphone if not set
voiceClient.SetRecordingThreshold(0.4f);   // volume (0-1) the microphone has to reach before anything gets sent
voiceClient.SetVolume(0.8f);               // playback volume of every player
voiceClient.SetClientVolume(clientId, 1f); // playback volume of one player
voiceClient.SetClientMute(clientId, true);
voiceClient.SetProximity(true);            // 3D sound, position the players audio source with GetAudioPlayer(clientId)
voiceClient.UseMic = false;                // only listen
```
`OnTalkingStateChanged` tells you when the local player starts or stops talking, e.g. to show an indicator.

To use a different codec, pass it in. All players need to use the same one:
```C#
new UnityVoiceClient(client, new G722Codec());
```
Available codecs are `OpusCodec`, `G722Codec`, `G729Codec` and `UncompressedCodec`, or implement `AudioCodec` yourself.

# Third-Party Code
-----------------
`Netamite.Voice.Codec` contains source code from the following projects. It is compiled directly into the assembly, so no extra dependencies are needed, but the code is not my own work. All credit goes to the original authors.

- **[Concentus](https://github.com/lostromb/concentus)** by Logan Stromberg, a managed C# port of the Opus codec (libopus), used for `OpusCodec`.  
  Located in `Netamite.Voice.Codec/Concentus`, version 1.1.7. Licensed under the BSD-style license in `Netamite.Voice.Codec/Concentus/LICENSE`.
- **[SIPSorcery](https://github.com/sipsorcery-org/sipsorcery)**, its codec implementations (G.711, G.722, G.729 and the PCM resampler), used for `G722Codec` and `G729Codec`.  
  Located in `Netamite.Voice.Codec/sipsorcery`. These files are based on further projects (SpanDSP, NAudio, Jitsi) and keep their original copyright and license notices in the file headers.
