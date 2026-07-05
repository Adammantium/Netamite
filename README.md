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

## Client Side Initialization
To initialize the client you need to create the corresponding client class for your platform.
```C#

```

## Server Side Initialization
