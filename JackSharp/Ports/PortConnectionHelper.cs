// Helper for getting connected port information
using System;
using System.Runtime.InteropServices;
using JackSharp.ApiWrapper;

namespace JackSharp.Ports
{
    /// <summary>
    /// Helper for retrieving information about connected ports.
    /// </summary>
    public static class PortConnectionHelper
    {
        [DllImport("libjack", CallingConvention = CallingConvention.Cdecl, EntryPoint = "jack_free")]
        private static extern void jack_free(IntPtr ptr);

        /// <summary>
        /// Gets the name of the first port connected to the input of the given port.
        /// For example, if this port is "SimpleLevelMeter:audio_in_1" and it's connected
        /// to "system:capture_1", this returns "system:capture_1".
        /// </summary>
        public static unsafe string GetUpstreamConnectedPortName(Port port)
        {
            try
            {
                // Get all connections to this port
                var connections = PortApi.GetAllConnections(port._jackClient, port._port);
                
                if (connections == IntPtr.Zero)
                    return null;

                // Get the first connection (upstream port name)
                var firstConnection = Marshal.ReadIntPtr(connections);
                if (firstConnection == IntPtr.Zero)
                {
                    jack_free(connections);
                    return null;
                }

                string upstreamPortName = Marshal.PtrToStringAnsi(firstConnection);
                
                // Free the connections array
                jack_free(connections);
                
                return upstreamPortName;
            }
            catch
            {
                return null;
            }
        }
    }
}
