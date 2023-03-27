using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace AMP.Network.Helper {
    internal class NetworkUtil {
        internal static string GetIP(string address) {
            if(!address.Equals("127.0.0.1")) {
                try {
                    IPAddress.Parse(address);
                }catch(Exception) {
                    try {
                        IPAddress[] addresslist = Dns.GetHostAddresses(address);
                        if(addresslist.Length > 0) {
                            try {
                                address = addresslist.First(addr => addr.AddressFamily == AddressFamily.InterNetwork).ToString();
                            }catch(Exception) {
                                //Log.Err($"No IPv4 could be resolved for {address}."); //TODO
                            }
                        } else {
                            //Log.Err($"No addresses found for {address}"); //TODO
                        }
                    }catch(Exception) {
                        //Log.Err($"Unable to resolve address for {address}. Check you internet connection."); //TODO
                    }
                }
            }
            return address;
        }
    }
}
