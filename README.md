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